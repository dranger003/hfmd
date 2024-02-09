using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace hfmd
{
    internal static partial class Extensions
    {
        public static async Task ForEachAsync<TSource>(this IEnumerable<TSource> source, Func<TSource, Task> action) =>
            await Parallel.ForEachAsync(source, async (item, _) => await action(item));

        public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> action) =>
            await Task.WhenAll(source.Select(async s => await action(s)));
        public static async Task<HttpResponseMessage> PostAsStreamAsync(this HttpClient client, string requestUri, HttpContent content, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            request.Content = content;

            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        public static string Sha256Sum(this byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", String.Empty).ToLowerInvariant();
        }
    }
}
