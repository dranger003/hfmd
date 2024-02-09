using System.Net;
using System.Net.Http.Json;

namespace hfmd
{
    public static class HuggingFace
    {
        private enum RepoType { Model, Dataset };

        private static async Task<List<T>> SearchAsync<T>(
            RepoType repoType,
            string? search = null,
            string? author = null,
            string? filter = null,
            string? sort = null,
            string? direction = null,
            int? limit = null,
            bool? full = null,
            bool? config = null,
            CancellationToken cancellationToken = default
        )
        {
            var query = new Dictionary<string, string?>
            {
                [nameof(search)] = search,
                [nameof(author)] = author,
                [nameof(filter)] = filter,
                [nameof(sort)] = sort,
                [nameof(direction)] = direction,
                [nameof(limit)] = limit.HasValue ? $"{limit}" : String.Empty,
                [nameof(full)] = (full ?? false) ? "true" : String.Empty,
                [nameof(config)] = (config ?? false) ? "true" : String.Empty,
            }
                .Where(x => !String.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{x.Key}={WebUtility.UrlEncode(x.Value)}")
                .Aggregate((a, b) => $"{a}&{b}");

            var url = $"https://hf.co/api/{$"{repoType}".ToLower()}s?{query}";
            //await Console.Out.WriteLineAsync($"[{url}]");

            using var httpClient = new HttpClient();
            using var response = (await httpClient.GetAsync(url, cancellationToken)).EnsureSuccessStatusCode();

            //var json = await response.Content.ReadAsStringAsync(cancellationToken);
            //Console.WriteLine(json);
            //return JsonSerializer.Deserialize<List<T>>(json) ?? [];

            return await response.Content.ReadFromJsonAsync<List<T>>() ?? new();
        }

        /// <summary>
        /// SearchModelsAsync (https://huggingface.co/docs/hub/en/api#get-apimodels)
        /// </summary>
        /// <param name="search">Filter based on substrings for repos and their usernames, such as resnet or microsoft</param>
        /// <param name="author">Filter models by an author or organization, such as huggingface or microsoft</param>
        /// <param name="filter">Filter based on tags, such as text-classification or spacy</param>
        /// <param name="sort">Property to use when sorting, such as downloads or author</param>
        /// <param name="direction">Direction in which to sort, such as -1 for descending, and anything else for ascending</param>
        /// <param name="limit">Limit the number of models fetched</param>
        /// <param name="full">Whether to fetch most model data, such as all tags, the files, etc.</param>
        /// <param name="config">Whether to also fetch the repo config</param>
        internal static Task<List<ModelData>> SearchModelsAsync(
            string? search = null,
            string? author = null,
            string? filter = null,
            string? sort = null,
            string? direction = null,
            int? limit = null,
            bool? full = null,
            bool? config = null,
            CancellationToken cancellationToken = default
        ) => SearchAsync<ModelData>(RepoType.Model, search, author, filter, sort, direction, limit, full, config, cancellationToken);

        /// <summary>
        /// SearchDatasetsAsync (https://huggingface.co/docs/hub/en/api#get-apidatasets)
        /// </summary>
        /// <param name="search">Filter based on substrings for repos and their usernames, such as pets or microsoft</param>
        /// <param name="author">Filter datasets by an author or organization, such as huggingface or microsoft</param>
        /// <param name="filter">Filter based on tags, such as task_categories:text-classification or languages:en</param>
        /// <param name="sort">Property to use when sorting, such as downloads or author</param>
        /// <param name="direction">Direction in which to sort, such as -1 for descending, and anything else for ascending</param>
        /// <param name="limit">Limit the number of datasets fetched</param>
        /// <param name="full">Whether to fetch most dataset data, such as all tags, the files, etc.</param>
        internal static Task<List<DatasetData>> SearchDatasetsAsync(
            string? search = null,
            string? author = null,
            string? filter = null,
            string? sort = null,
            string? direction = null,
            int? limit = null,
            bool? full = false,
            CancellationToken cancellationToken = default
        ) => SearchAsync<DatasetData>(RepoType.Dataset, search, author, filter, sort, direction, limit, full, null, cancellationToken);

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
                .SelectAsync(async entry =>
                {
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
