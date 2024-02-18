using System.Net.Http.Headers;
using System.Text.RegularExpressions;

using Spectre.Console;

namespace hfmd
{
    internal class Program
    {
        // https://huggingface.co/spaces/enzostvs/hub-api-playground

        private static async Task Main(string[] args)
        {
            // hfmd.exe ikawrakow/mixtral-instruct-8x7b-quantized-gguf C:\LLM_MODELS\
            // hfmd.exe dataset:ikawrakow/validation-datasets-for-llama.cpp C:\LLM_MODELS\

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => cancellationTokenSource.Cancel(!(e.Cancel = true));

            var match = Regex.Match(args[0], @"(?i)^(?:(?<Dataset>dataset:))?(?<Owner>.+)\/(?<Name>.+)$");
            var id = $"{match.Groups["Owner"]}/{match.Groups["Name"].Value}";
            var savePath = $"{Path.Combine(Path.GetFullPath(args[1]), match.Groups["Owner"].Value, match.Groups["Name"].Value)}{Path.DirectorySeparatorChar}";
            var branchName = args.Length > 2 ? args[2] : null;
            var isDataset = match.Groups["Dataset"].Value == "dataset:";

            var entries = await HuggingFace.FetchEntriesAsync(!isDataset ? HuggingFace.RepoType.Model : HuggingFace.RepoType.Dataset, id, cancellationToken: cancellationTokenSource.Token);
            var maxPathLength = entries.Max(x => x.Path?.Length) ?? 0;

            entries.ForEach(entry => entry.Id = $"{entry.Path}{new String(' ', maxPathLength + 4 - (entry.Path?.Length ?? 0))}(Type={entry.Type})");
            var entriesByText = entries.ToDictionary(k => k.Id ?? $"{Guid.NewGuid}");

            var selectedEntries = new List<Entry>();

            if (isDataset)
            {
                selectedEntries.AddRange(
                    AnsiConsole.Prompt(
                        new MultiSelectionPrompt<string>()
                            .Title($"Saving to [[{savePath}]].\nSelect file(s) to download:")
                            .NotRequired()
                            .PageSize(Math.Min(entries.Count, Console.WindowHeight - 6))
                            .MoreChoicesText("[grey](Move up and down to reveal more entries)[/]")
                            .InstructionsText("[grey](Press [blue]<space>[/] to toggle an entry, [green]<enter>[/] to accept)[/]")
                            .AddChoiceGroup(
                                "Files",
                                entries
                                    .Where(entry => !Path.GetFileName(entry.Path ?? String.Empty).StartsWith("."))
                                    .Select(entry => entry.Id ?? String.Empty)
                                    .ToList()
                            )
                            .AddChoiceGroup(
                                "Files (dot)",
                                entries
                                    .Where(entry => Path.GetFileName(entry.Path ?? String.Empty).StartsWith("."))
                                    .Select(entry => entry.Id ?? String.Empty)
                                    .ToList()
                            )
                    ).Select(text => entriesByText[text])
                    .ToList()
                );
            }
            else
            {
                selectedEntries.AddRange(
                    AnsiConsole.Prompt(
                        new MultiSelectionPrompt<string>()
                            .Title($"Saving to [[{savePath}]].\nSelect file(s) to download:")
                            .NotRequired()
                            .PageSize(Math.Min(entries.Count, Console.WindowHeight - 6))
                            .MoreChoicesText("[grey](Move up and down to reveal more entries)[/]")
                            .InstructionsText("[grey](Press [blue]<space>[/] to toggle an entry, [green]<enter>[/] to accept)[/]")
                            .AddChoiceGroup(
                                "Files",
                                entries
                                    .Where(entry => !Path.GetFileName(entry.Path ?? String.Empty).StartsWith("."))
                                    .Where(entry => Path.GetExtension(entry.Path) != ".safetensors" && entry.Path != "model.safetensors.index.json")
                                    .Where(entry => Path.GetExtension(entry.Path) != ".bin" && entry.Path != "pytorch_model.bin.index.json")
                                    .Select(entry => entry.Id ?? String.Empty)
                                    .ToList()
                            )
                            .AddChoiceGroup(
                                "Safetensors",
                                entries
                                    .Where(entry => Path.GetExtension(entry.Path) == ".safetensors" || entry.Path == "model.safetensors.index.json")
                                    .Select(entry => entry.Id ?? String.Empty)
                                    .ToList()
                            )
                            .AddChoiceGroup(
                                "PyTorch",
                                entries
                                    .Where(entry => Path.GetExtension(entry.Path) == ".bin" || entry.Path == "pytorch_model.bin.index.json")
                                    .Select(entry => entry.Id ?? String.Empty)
                                    .ToList()
                            )
                            .AddChoiceGroup(
                                "Files (dot)",
                                entries
                                    .Where(entry => Path.GetFileName(entry.Path ?? String.Empty).StartsWith("."))
                                    .Select(entry => entry.Id ?? String.Empty)
                                    .ToList()
                            )
                    ).Select(text => entriesByText[text])
                    .ToList()
                );
            }

            var table = new Table();
            table.AddColumns(["Type", "SHA256", "Size", "Path"]);
            selectedEntries.ForEach(fileEntry => table.AddRow(fileEntry.Type ?? String.Empty, fileEntry.Oid ?? String.Empty, $"{fileEntry.Size}", fileEntry.Path ?? String.Empty));
            AnsiConsole.Write(table);

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    [
                        new TaskDescriptionColumn(),
                            new ProgressBarColumn(),
                            new PercentageColumn(),
                            new RemainingTimeColumn(),
                            new TransferSpeedColumn(),
                            new SpinnerColumn(),
                    ]
                )
                .StartAsync(async ctx =>
                {
                    await Task.WhenAll(selectedEntries.Select(async fileEntry =>
                    {
                        var task = ctx.AddTask(fileEntry.Path ?? String.Empty, new ProgressTaskSettings { AutoStart = true });
                        await Download(task, !isDataset ? id : $"datasets/{id}", branchName ?? "main", savePath, fileEntry.Path ?? String.Empty, fileEntry.Size ?? 0, cancellationTokenSource.Token);
                    }));
                });
        }

        private static async Task Download(ProgressTask task, string id, string branch, string path, string fileEntryPath, long size, CancellationToken token)
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
                    RequestUri = new Uri($"https://hf.co/{id}/resolve/{branch}/{fileEntryPath}"),
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
}
