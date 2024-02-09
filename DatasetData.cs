using System.Text.Json.Serialization;

namespace hfmd
{
    internal class DatasetDataCardData
    {
        [JsonPropertyName("license")]
        public string? License { get; set; }

        [JsonPropertyName("task_categories")]
        public List<string>? TaskCategories { get; set; } = [];
    }

    internal class DatasetData
    {
        [JsonPropertyName("_id")]
        public string? _Id { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("cardData")]
        public DatasetDataCardData? CardData { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("disabled")]
        public bool? Disabled { get; set; }

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

        [JsonPropertyName("downloads")]
        public int? Downloads { get; set; }

        [JsonPropertyName("paperswithcode_id")]
        public string? PapersWithCodeId { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; } = [];

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }
    }
}
