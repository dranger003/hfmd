using System.Net.Mime;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace hfmd
{
    internal static class GptModel
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        private enum ChatCompletionRole { System, User, Assistant, Function }

        private class UnixTimeJsonConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).DateTime.ToLocalTime();
            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteNumberValue(value.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds);
        }

        private class ChatCompletionMessage
        {
            [JsonPropertyName("role")] public ChatCompletionRole Role { get; set; }
            [JsonPropertyName("content")] public string? Content { get; set; }
            [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
            [JsonPropertyName("function_call"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public object? FunctionCall { get; set; }
        }

        private class ChatCompletionRequest
        {
            [JsonPropertyName("model")] public string? Model { get; set; }
            [JsonPropertyName("messages")] public IList<ChatCompletionMessage>? Messages { get; set; }
            [JsonPropertyName("functions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IList<object>? Functions { get; set; }
            [JsonPropertyName("function_call"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public object? FunctionCall { get; set; }
            [JsonPropertyName("temperature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Temperature { get; set; }
            [JsonPropertyName("top_p"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? TopP { get; set; }
            [JsonPropertyName("n"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? N { get; set; }
            [JsonPropertyName("stream"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Stream { get; set; }
            [JsonPropertyName("stop"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? Stop { get; set; }
            [JsonPropertyName("max_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? MaxTokens { get; set; }
            [JsonPropertyName("presence_penalty"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? PresencePenalty { get; set; }
            [JsonPropertyName("frequency_penalty"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? FrequencyPenalty { get; set; }
            [JsonPropertyName("logit_bias"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IDictionary<int, int>? LogitBias { get; set; }
            [JsonPropertyName("user"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? User { get; set; }
        }

        private class ChatCompletionDelta
        {
            [JsonPropertyName("role")] public string? Role { get; set; }
            [JsonPropertyName("content")] public string? Content { get; set; }
            [JsonPropertyName("function_call"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public object? FunctionCall { get; set; }
        }

        private class ChatCompletionChoice
        {
            [JsonPropertyName("index")] public int? Index { get; set; }
            [JsonPropertyName("finish_reason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FinishReason { get; set; }
            [JsonPropertyName("delta")] public ChatCompletionDelta? Delta { get; set; } = new();
        }

        private class ChatCompletionResponse
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("object")] public string? Object { get; set; }
            [JsonPropertyName("created"), JsonConverter(typeof(UnixTimeJsonConverter))] public DateTime Created { get; set; }
            [JsonPropertyName("model")] public string? Model { get; set; }
            [JsonPropertyName("choices")] public List<ChatCompletionChoice>? Choices { get; set; }
        }

        public static async IAsyncEnumerable<string?> PromptAsync(string instructions, IList<string> prompts, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatCompletionMessage>() { new ChatCompletionMessage { Role = ChatCompletionRole.System, Content = $"{instructions}" } };
            messages.AddRange(prompts.Select((prompt, index) => new ChatCompletionMessage { Role = index % 2 == 0 ? ChatCompletionRole.User : ChatCompletionRole.Assistant, Content = prompt }));

            var options = new JsonSerializerOptions() { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            var json = JsonSerializer.Serialize(new ChatCompletionRequest { Model = "gpt-4", Messages = messages, Stream = true }, options);
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{config["OPENAI_KEY"]}");

            using var response = await httpClient.PostAsStreamAsync($"{config["OPENAI_URL"]}", content, cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken) ?? String.Empty;
                if (String.IsNullOrEmpty(line))
                    continue;

                var data = Regex.Match(line, @"(?<=data:\s).*").Value;
                if (data == "[DONE]")
                    continue;

                var result = JsonSerializer.Deserialize<ChatCompletionResponse>(data);
                if (!String.IsNullOrEmpty(result?.Choices?[0].Delta?.Role) && String.IsNullOrEmpty(result?.Choices?[0].Delta?.Content))
                    continue;

                if (!String.IsNullOrEmpty(result?.Choices?[0].FinishReason))
                    continue;

                yield return result?.Choices?[0].Delta?.Content;
            }
        }

        public static IAsyncEnumerable<string?> PromptAsync(string instructions, string prompt, CancellationToken cancellationToken = default) =>
            PromptAsync(instructions, new[] { prompt }, cancellationToken);

        public static IAsyncEnumerable<string?> PromptAsync(string prompt, CancellationToken cancellationToken = default) =>
            PromptAsync("You are a helpful assistant.", new[] { prompt }, cancellationToken);
    }
}
