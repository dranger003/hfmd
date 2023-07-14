using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Spectre.Console;

internal class Program
{
    private static async Task Main(string[] args)
    {
        //args = new[] { "TheBloke/WizardLM-30B-fp16", @"C:\LLM_MODELS\" };
        //args = new[] { "TheBloke/tulu-30B-fp16", @"C:\LLM_MODELS\" };
        //args = new[] { "TheBloke/guanaco-65B-HF", @"C:\LLM_MODELS\" };

        if (!ValidateArgs(args, out var modelName, out var savePath, out var branchName))
            return;

        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var entries = await Fetch(modelName, branchName, cancellationTokenSource.Token);
        var maxPathLength = entries.Max(x => x.Path.Length);

        var fileEntries = entries
            .Select(
                x => new
                {
                    x.Path,
                    x.Oid,
                    x.Type,
                    x.Size,
                    Info = GetFileEntryInfo(savePath, x),
                }
            )
            .Select(
                x => new
                {
                    x.Path,
                    x.Oid,
                    x.Type,
                    x.Size,
                    x.Info,
                    Text = $"{x.Path}{new String(' ', maxPathLength + 4 - x.Path.Length)}(Type={x.Type}, Exists={x.Info.Exists}, ExistingSize={x.Info.ExistingSize}, PartExists={x.Info.PartExists}, PartExistingSize={x.Info.PartExistingSize})",
                }
            )
            .ToList();

        var fileEntriesByText = fileEntries.ToDictionary(k => k.Text);

        var selectedFileEntries = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title($"Saving to [[{savePath}]].\nSelect file(s) to download:")
                .NotRequired()
                .PageSize(20)
                .MoreChoicesText("[grey](Move up and down to reveal more entries)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle an entry, [green]<enter>[/] to accept)[/]")
                .AddChoices(fileEntries.Select(fileEntry => fileEntry.Text))
        ).Select(text => fileEntriesByText[text]).ToList();

        var table = new Table();
        table.AddColumns(new[] { "Type", "Oid", "Size", "Path" });
        selectedFileEntries.ForEach(fileEntry => table.AddRow(fileEntry.Type, fileEntry.Oid, $"{fileEntry.Size}", fileEntry.Path));
        AnsiConsole.Write(table);

        //{
        //    var fileEntry = selectedFileEntries.First();
        //    var task = new ProgressTask(0, fileEntry.Text, 0.0D);
        //    await Download(task, modelName, branchName, savePath, fileEntry.Path, fileEntry.Size, cancellationTokenSource.Token);
        //}

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new TransferSpeedColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                await Task.WhenAll(selectedFileEntries.Select(async fileEntry =>
                {
                    var task = ctx.AddTask(fileEntry.Path, new ProgressTaskSettings { AutoStart = true });
                    await Download(task, modelName, branchName, savePath, fileEntry.Path, fileEntry.Size, cancellationTokenSource.Token);
                }));
            });
    }

    private static bool ValidateArgs(string[] args, out string modelName, out string savePath, out string branchName)
    {
        var result = true;

        modelName = String.Empty;
        savePath = String.Empty;
        branchName = String.Empty;

        if (args.Length > 1)
        {
            modelName = args[0];
            var match = Regex.Match(modelName, "^(?<Owner>.+)\\/(?<Model>.+)$");

            if (!match.Success)
            {
                AnsiConsole.WriteLine($"[red]Error[/]: Invalid model name ({modelName}).");
                result = false;
            }
            else
            {
                savePath = $"{Path.Combine(Path.GetFullPath(args[1]), match.Groups["Owner"].Value, match.Groups["Model"].Value)}{Path.DirectorySeparatorChar}";
                branchName = args.Length > 2 ? args[2] : "main";
            }
        }
        else
        {
            result = false;
        }

        if (!result)
        {
            Console.WriteLine(
                $"""
                Usage: {Path.GetFileName(Assembly.GetExecutingAssembly().Location)} [model-name] <path> <branch>
                    [model-name]    As copied from Hugging Face (i.e. owner/model)
                    <path>          Optional destination path to write file(s) to (default: .)
                    <branch>        Optional branch name to download from (default: main)
                """
            );
        }

        return result;
    }

    private static FileEntryInfo GetFileEntryInfo(string path, FileEntry fileEntry)
    {
        var fileEntryInfo = new FileEntryInfo();

        {
            var fileInfo = new FileInfo(Path.Combine(path, fileEntry.Path));
            fileEntryInfo.Exists = fileInfo.Exists;
            fileEntryInfo.ExistingSize = fileInfo.Exists ? fileInfo.Length : 0L;
        }
        {
            var fileInfo = new FileInfo(Path.Combine(path, $"{fileEntry.Path}.part"));
            fileEntryInfo.PartExists = fileInfo.Exists;
            fileEntryInfo.PartExistingSize = fileInfo.Exists ? fileInfo.Length : 0L;
        }

        return fileEntryInfo;
    }

    private static async Task<List<FileEntry>> Fetch(string name, string branch, CancellationToken token)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync($"https://hf.co/api/models/{name}/tree/{branch}", token);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(token);
        var fileEntries = JsonSerializer.Deserialize<List<FileEntry>>(content) ?? new();
        return fileEntries.Where(fileEntry => fileEntry.Type == "file").ToList();
    }

    private static async Task Download(ProgressTask task, string name, string branch, string path, string fileEntryPath, long size, CancellationToken token)
    {
        try
        {
            using var httpClient = new HttpClient();

            var output = Path.Combine(path, fileEntryPath);
            var outputPart = $"{output}.part";

            if (File.Exists(output))
            {
                task.MaxValue(size);
                task.Value(size);
                return;
            }

            var resumeOffset = 0L;
            if (File.Exists(outputPart))
                resumeOffset = new FileInfo(outputPart).Length;
            else
                using (var _ = File.Create(outputPart)) { }

            using var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://hf.co/{name}/resolve/{branch}/{fileEntryPath}"),
            };

            if (resumeOffset > 0)
                request.Headers.Range = new RangeHeaderValue(resumeOffset, null);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            response.EnsureSuccessStatusCode();

            var length = response.Content.Headers.ContentLength;
            if (length == null)
                throw new Exception($"ContentLength = {length}");

            task.MaxValue(length.Value);
            task.StartTask();
            task.Value(resumeOffset);

            var bufferSize = 16384;
            var buffer = new byte[bufferSize];

            {
                using var contentStream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream = new FileStream(outputPart, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize, true);

                while (true)
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0)
                        break;

                    task.Increment(read);

                    await fileStream.WriteAsync(buffer, 0, read, token);
                    await fileStream.FlushAsync(token);
                }
            }

            File.Move(outputPart, output);
        }
        catch (TaskCanceledException)
        {
            //AnsiConsole.MarkupLine($"[yellow]Info:[/] Download cancelled ({fileEntryPath}).");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex}");
        }
    }
}

class FileEntry
{
    [JsonPropertyName("type")] public string Type { get; set; } = String.Empty;
    [JsonPropertyName("oid")] public string Oid { get; set; } = String.Empty;
    [JsonPropertyName("size")] public long Size { get; set; } = 0;
    [JsonPropertyName("path")] public string Path { get; set; } = String.Empty;
    [JsonIgnore] public long ResumeOffset { get; set; } = 0;
}

class FileEntryInfo
{
    public bool Exists { get; set; } = false;
    public long ExistingSize { get; set; } = 0L;
    public bool PartExists { get; set; } = false;
    public long PartExistingSize { get; set; } = 0L;
}

static class Extensions
{
    public static async Task ForEachAsync<TSource>(this IEnumerable<TSource> source, Func<TSource, Task> action) =>
        await Parallel.ForEachAsync(source, async (item, _) => await action(item));

    public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> action) =>
        await Task.WhenAll(source.Select(async s => await action(s)));
}
