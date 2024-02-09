using System.Text.Json.Serialization;

namespace hfmd
{
    internal class ModelDataConfig
    {
        [JsonPropertyName("architectures")]
        public List<string>? Architectures { get; set; } = [];

        [JsonPropertyName("model_type")]
        public string? ModelType { get; set; }

        [JsonPropertyName("tokenizer_config")]
        public Dictionary<string, object>? TokenizerConfig { get; set; } = [];
    }

    internal class ModelDataSibling
    {
        [JsonPropertyName("rfilename")]
        public string? Filename { get; set; }
    }

    internal class ModelData
    {
        [JsonPropertyName("_id")]
        public string? _Id { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("gated")]
        public bool? Gated { get; set; }

        [JsonPropertyName("lastModified")]
        public DateTime? LastModified { get; set; }

        [JsonPropertyName("likes")]
        public int? Likes { get; set; }

        [JsonPropertyName("private")]
        public bool? Private { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("config")]
        public List<ModelDataConfig>? Config { get; set; } = [];

        [JsonPropertyName("downloads")]
        public int? Downloads { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; } = [];

        [JsonPropertyName("pipeline_tag")]
        public string? PipelineTag { get; set; }

        [JsonPropertyName("library_name")]
        public string? LibraryName { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("modelId")]
        public string? ModelId { get; set; }

        [JsonPropertyName("siblings")]
        public List<ModelDataSibling>? Siblings { get; set; } = [];
    }
}
