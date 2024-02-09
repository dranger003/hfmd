using System.Text.Json.Serialization;

namespace hfmd
{
    internal class Sibling
    {
        [JsonPropertyName("rfilename")]
        public string? Rfilename { get; set; }
    }

    internal class ModelData
    {
        [JsonPropertyName("_id")]
        public string? _Id { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }

        [JsonPropertyName("likes")]
        public int Likes { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("config")]
        public Dictionary<string, object> Config { get; set; } = new();

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("pipeline_tag")]
        public string? PipelineTag { get; set; }

        [JsonPropertyName("library_name")]
        public string? LibraryName { get; set; }

        [JsonPropertyName("siblings")]
        public List<Sibling> Siblings { get; set; } = new();

        [JsonPropertyName("modelId")]
        public string? ModelId { get; set; }
    }
}
