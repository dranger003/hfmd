using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Reflection.PortableExecutable;
using System;
using System.ComponentModel.DataAnnotations;

namespace hfmd
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            {
                var cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) => cancellationTokenSource.Cancel(!(e.Cancel = true));

                //{
                //    var instructions = "You are a helpful assistant, and you respond exclusively using markdown tables.";
                //    //var prompt = "What are the planets of the solar system in order from the Sun? Include other details like the distance from the Sun in AU, how many moons, circumference, etc.";
                //    var prompts = new[]
                //    {
                //        "What is 1+1?",
                //        "2",
                //        "What is 2+2?",
                //    };

                //    await foreach (var token in GptModel.PromptAsync(instructions, prompts, cancellationTokenSource.Token))
                //    {
                //        await Console.Out.WriteAsync(token);
                //    }
                //}

                //{
                //    var models = await HuggingFace.SearchModelsAsync(author: "TheBloke", search: "GGML", full: true, config: true, cancellationToken: cancellationTokenSource.Token);

                //    await models
                //        .OrderByDescending(x => x.LastModified)
                //        .Take(1)
                //        .ForEachAsync(async model => {
                //            await Console.Out.WriteLineAsync($"[{model.Id}][{model.LastModified}][{model.Siblings.Count}]");
                //            //await WriteEntriesAsync(await HuggingFace.FetchModelEntriesAsync(model.Id), 4);

                //            var card = await HuggingFace.FetchModelCardAsync(model.Id) ?? String.Empty;
                //            //var html = Markdig.Markdown.ToHtml(card);
                //            await Console.Out.WriteLineAsync(card);

                //            //var fileName = Path.GetFullPath("README.html");
                //            //await Console.Out.WriteLineAsync($"[{fileName}]");
                //            //await File.WriteAllTextAsync(fileName, html);
                //            //Process.Start(new ProcessStartInfo { FileName = fileName, UseShellExecute = true });
                //        });
                //}

                //{
                //    var authors = new[]
                //    {
                //        "Open-Orca",
                //        "OpenAssistant",
                //        "ehartford",
                //    };

                //    var datasets = await HuggingFace.SearchDatasetsAsync(author: authors[0], full: true, cancellationToken: cancellationTokenSource.Token);

                //    await datasets
                //        .OrderByDescending(x => x.LastModified)
                //        .Take(1)
                //        .ForEachAsync(async dataset => {
                //            await Console.Out.WriteLineAsync($"[{dataset.Id}][{dataset.LastModified}]");
                //            await WriteEntriesAsync(await HuggingFace.FetchDatasetEntriesAsync(dataset.Id), 4);
                //            //await Console.Out.WriteLineAsync(await HuggingFace.FetchDatasetCardAsync(dataset.Id));
                //        });
                //}

                {
                    var id = "Open-Orca/OpenOrca";
                    var path = "3_5M-GPT3_5-Augmented.parquet";
                    var branch = "main";
                    var size = 3090560834L;

                    var chunkCount = 4;
                    var chunkSize = (long)(size / (double)chunkCount);
                    var chunkRemainder = size % chunkCount;

                    var url = $"https://hf.co/datasets/{id}/resolve/{branch}/{path}";

                    var ranges = Enumerable.Range(0, 4)
                        .Select(
                            i =>
                            {
                                var from = i * chunkSize;
                                var to = from + chunkSize - 1 + (i < chunkCount - 1 ? 0 : chunkRemainder);
                                return (From: from, To: to, Length: to - from + 1);
                            }
                        )
                        .ToList();

                    //var total = ranges
                    //    .Select(x => x.Length)
                    //    .Aggregate((x, y) => x + y);

                    var tasks = ranges
                        .Select(
                            (x, i) => {
                                var fileInfo = new FileInfo($"{path}.part{i:00}");
                                return DownloadFile(
                                    url,
                                    fileInfo.Name,
                                    x.From,
                                    x.To,
                                    //progress: (read, total) => Console.Write($"\r{new String(' ', 100)}\r{read}/{total} [{read / (double)total * 100:0.0} %]"),
                                    cancellationToken: cancellationTokenSource.Token
                                );
                            }
                        )
                        .ToList();

                    await Task.WhenAll(tasks);

                    await Console.Out.WriteLineAsync();
                    if (cancellationTokenSource.IsCancellationRequested)
                        await Console.Out.WriteLineAsync($"Cancelled.");
                }
            }
        }

        private static async Task WriteEntriesAsync(List<Entry> entries, int? indent = null)
        {
            foreach (var entry in entries)
            {
                var length = entry.Size / (double)1048576;
                await Console.Out.WriteLineAsync($"{new String(' ', indent ?? 0)}[{entry.Type}][{entry.Oid}][{entry.Size}][{length:0.00} MiB][{entry.Path}]");
                if (entry.Type == "directory")
                    await WriteEntriesAsync(entry.Entries, (indent ?? 1) * 2);
            }
        }

        private static async Task DownloadFile(string url, string path, long from = 0, long? to = null, Action<long, long>? progress = null, CancellationToken cancellationToken = default)
        {
            using var httpClient = new HttpClient();

            using var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
            };

            if (from > 0)
                request.Headers.Range = new RangeHeaderValue(from, to);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var length = response.Content.Headers.ContentLength ?? 0;
            var totalLength = from + length;

            //if (request.Headers.Range?.Ranges != null)
            //    await request.Headers.Range.Ranges.ForEachAsync(async range => await Console.Out.WriteLineAsync($"ContentRange = {range.From}-{range.To} [{totalLength}]"));

            //await Console.Out.WriteLineAsync($"StatusCode = {response.StatusCode}");
            //await Console.Out.WriteLineAsync($"ContentLength = {length}");

            //if (from >= totalLength)
            //    return;

            var bufferSize = 1024 * 1024;
            var buffer = new byte[bufferSize];

            var totalRead = from;

            await Console.Out.WriteLineAsync($"[{path}][{from}-{to}][{to - from}][{length}][{totalLength}]");

            try
            {
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize, true);

                while (true)
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0)
                        break;

                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);

                    totalRead += read;

                    if (progress != null && totalRead != totalLength)
                        progress(totalRead, totalLength);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
            { }

            if (progress != null)
                progress(totalRead, totalLength);
        }
    }
}
