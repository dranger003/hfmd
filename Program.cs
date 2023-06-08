using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Spectre.Console;

if (args.Length < 1)
{
    Console.WriteLine(
        $"""
        Usage: {Path.GetFileName(Assembly.GetExecutingAssembly().Location)} [model-name] <path> <branch>
            [model-name]    As copied from Hugging Face (i.e. owner/model)
            <path>          Optional destination path to write file(s) to (default: .)
            <branch>        Optional branch name to download from (default: main)
        """
    );

    return;
}

var name = args[0];
var match = Regex.Match(name, "^(?<Owner>.+)\\/(?<Model>.+)$");

if (!match.Success)
{
    Console.WriteLine($"Error: Invalid model name ({name}).");
    return;
}

var path = Path.Combine(Path.GetFullPath(args.Length > 1 ? args[1] : "."), match.Groups["Owner"].Value, match.Groups["Model"].Value);
var branch = args.Length > 2 ? args[1] : "main";

Console.WriteLine($"Saving to [{path}].");

if (!Directory.Exists(path))
    Directory.CreateDirectory(path);

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

using var httpClient = new HttpClient();
var fileEntries = await Fetch(httpClient, name, branch);

await AnsiConsole.Progress()
    .AutoClear(false)
    .HideCompleted(false)
    .Columns(new ProgressColumn[]
    {
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn(),
        new SpinnerColumn(),
    })
    .StartAsync(async ctx =>
    {
        await Task.WhenAll(
            fileEntries.Select(async fileEntry =>
            {
                var task = ctx.AddTask(fileEntry.Path, new ProgressTaskSettings { AutoStart = true });
                await Download(httpClient, task, fileEntry.Path);
            })
        );
    });

async Task<List<FileEntry>> Fetch(HttpClient httpClient, string name, string branch)
{
    using var response = await httpClient.GetAsync($"https://hf.co/api/models/{name}/tree/{branch}", cancellationTokenSource.Token);
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token);
    var fileEntries = JsonSerializer.Deserialize<List<FileEntry>>(content) ?? new();
    return fileEntries.Where(fileEntry => fileEntry.Type == "file").ToList();
}

async Task Download(HttpClient httpClient, ProgressTask task, string fileEntryPath)
{
    try
    {
        using var response = await httpClient.GetAsync(
            $"https://hf.co/{name}/resolve/{branch}/{fileEntryPath}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationTokenSource.Token
        );

        response.EnsureSuccessStatusCode();

        task.MaxValue(response.Content.Headers.ContentLength ?? 0);
        task.StartTask();

        AnsiConsole.MarkupLine($"[u]{fileEntryPath}[/] ({task.MaxValue} byte(s))");

        var bufferSize = 8192;
        var buffer = new byte[bufferSize];

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationTokenSource.Token);
        using var fileStream = new FileStream(Path.Combine(path, fileEntryPath), FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);

        while (true)
        {
            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
            if (read == 0)
            {
                AnsiConsole.MarkupLine($"[u]{fileEntryPath}[/] [green]100%[/]");
                break;
            }

            task.Increment(read);
            await fileStream.WriteAsync(buffer, 0, read, cancellationTokenSource.Token);
        }
    }
    catch (TaskCanceledException)
    {
        AnsiConsole.MarkupLine($"[orange]Info:[/] cancelled.");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {ex}");
    }
}

class FileEntry
{
    [JsonPropertyName("type")] public string Type { get; set; } = String.Empty;
    [JsonPropertyName("oid")] public string Oid { get; set; } = String.Empty;
    [JsonPropertyName("size")] public long Size { get; set; } = 0;
    [JsonPropertyName("path")] public string Path { get; set; } = String.Empty;
}

static class Extensions
{
    public static async Task ForEachAsync<TSource>(this IEnumerable<TSource> source, Func<TSource, Task> action) =>
        await Parallel.ForEachAsync(source, async (item, _) => await action(item));

    public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> action) =>
        await Task.WhenAll(source.Select(async s => await action(s)));
}
