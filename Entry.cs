using System.Text.Json.Serialization;

namespace hfmd
{
    internal class EntryLfs
    {
        [JsonPropertyName("oid")]
        public string? Oid { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("pointerSize")]
        public int? PointerSize { get; set; }
    }

    internal class Entry
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("oid")]
        public string? Oid { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("lfs")]
        public EntryLfs? Lfs { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonIgnore]
        public List<Entry>? Entries { get; set; } = [];
    }
}
