using System.Net.Http.Json;
using System.Net;
using System;

namespace hfmd
{
    public static class HuggingFace
    {
        private enum RepoType { Model, Dataset };

        private static async Task<List<T>> SearchAsync<T>(
            RepoType repoType,
            string? author = null,
            string? search = null,
            string? sort = null,
            string? direction = null,
            bool full = false,
            bool config = false,
            CancellationToken cancellationToken = default
        )
        {
            var query = new Dictionary<string, string?>
            {
                [nameof(author)] = author,
                [nameof(search)] = search,
                [nameof(sort)] = sort,
                [nameof(direction)] = direction,
                [nameof(full)] = full ? "true" : String.Empty,
                [nameof(config)] = config ? "true" : String.Empty,
            }
                .Where(x => !String.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{x.Key}={WebUtility.UrlEncode(x.Value)}")
                .Aggregate((a, b) => $"{a}&{b}");

            var url = $"https://hf.co/api/{$"{repoType}".ToLower()}s?{query}";
            //await Console.Out.WriteLineAsync($"[{url}]");

            using var httpClient = new HttpClient();
            using var response = (await httpClient.GetAsync(url, cancellationToken)).EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<T>>() ?? new();
        }

        internal static Task<List<ModelData>> SearchModelsAsync(
            string? author = null,
            string? search = null,
            string? sort = null,
            string? direction = null,
            bool full = false,
            bool config = false,
            CancellationToken cancellationToken = default
        ) => SearchAsync<ModelData>(RepoType.Model, author, search, sort, direction, full, config, cancellationToken);

        internal static Task<List<DatasetData>> SearchDatasetsAsync(
            string? author = null,
            string? search = null,
            string? sort = null,
            string? direction = null,
            bool full = false,
            CancellationToken cancellationToken = default
        ) => SearchAsync<DatasetData>(RepoType.Dataset, author, search, sort, direction, full, false, cancellationToken);

        private static async Task<List<Entry>> FetchEntriesAsync(RepoType repoType, string? id, string? branchName = null, string? path = null, CancellationToken cancellationToken = default)
        {
            // https://hf.co/api/models/ehartford/dolphin-llama-13b/tree/main
            // https://hf.co/api/datasets/ehartford/dolphin/tree/main

            var url = $"https://hf.co/api/{$"{repoType}".ToLower()}s/{id}/tree/{branchName}{path}";
            //await Console.Out.WriteLineAsync($"[{url}]");

            using var httpClient = new HttpClient();
            using var response = (await httpClient.GetAsync(url, cancellationToken)).EnsureSuccessStatusCode();

            var entries = await response.Content.ReadFromJsonAsync<List<Entry>>() ?? new();

            await entries
                .Where(entry => entry.Type?.ToLower() == "directory")
                .SelectAsync(async entry => {
                    entry.Entries = await FetchEntriesAsync(repoType, id, branchName, $"{path}/{entry.Path}", cancellationToken);
                    return entry;
                });

            return entries;
        }

        internal static async Task<List<Entry>> FetchModelEntriesAsync(string? modelId, string? branchName = "main", string? path = null, CancellationToken cancellationToken = default) =>
            await FetchEntriesAsync(RepoType.Model, modelId, branchName, path, cancellationToken);

        internal static async Task<List<Entry>> FetchDatasetEntriesAsync(string? datasetId, string? branchName = "main", string? path = null, CancellationToken cancellationToken = default) =>
            await FetchEntriesAsync(RepoType.Dataset, datasetId, branchName, path, cancellationToken);

        private static async Task<string?> FetchCardAsync(RepoType repoType, string? id, string? branchName = null, string? path = null, CancellationToken cancellationToken = default)
        {
            // https://hf.co/ehartford/dolphin-llama-13b/resolve/main/README.md
            // https://hf.co/datasets/ehartford/dolphin/resolve/main/README.md

            try
            {
                var url = $"https://hf.co/{(repoType == RepoType.Dataset ? "datasets/" : "")}{id}/resolve/{branchName}{path}";
                //await Console.Out.WriteLineAsync($"[{url}]");

                using var httpClient = new HttpClient();
                using var response = (await httpClient.GetAsync(url, cancellationToken)).EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return null;
            }
        }

        internal static async Task<string?> FetchModelCardAsync(string? modelId, string? branchName = "main", string? path = "/README.md", CancellationToken cancellationToken = default) =>
            await FetchCardAsync(RepoType.Model, modelId, branchName, path, cancellationToken);

        internal static async Task<string?> FetchDatasetCardAsync(string? datasetId, string? branchName = "main", string? path = "/README.md", CancellationToken cancellationToken = default) =>
            await FetchCardAsync(RepoType.Dataset, datasetId, branchName, path, cancellationToken);
    }
}
