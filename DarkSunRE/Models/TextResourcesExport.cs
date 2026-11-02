using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DarkSunRE.Models
{
    /// <summary>
    /// Single text resource entry (TEXT, MSG, MERR, ETME, SPIN, RNME, NAME)
    /// for batch export/import.
    /// </summary>
    public class TextResourceEntry
    {
        public string ContainerName { get; set; }
        public string ResourceId { get; set; } // "TAG-NUMBER"
        public string ResourceType { get; set; } // "TEXT", "NAME", etc.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string OriginalText { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TranslatedText { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<NameResourceEntryExport>? NameEntries { get; set; }
    }

    /// <summary>
    /// One NAME record with its source text, translation and binary metadata.
    /// </summary>
    public class NameResourceEntryExport
    {
        public string OriginalMainName { get; set; }
        public string TranslatedMainName { get; set; }
        public string Path { get; set; }
        public ushort Index { get; set; }
        public byte Flag { get; set; }
    }

    /// <summary>
    /// Container for all text resources (TEXT, NAME, etc.) in a project,
    /// exported to a single JSON file.
    /// </summary>
    public class TextResourcesExport
    {
        public string ProjectName { get; set; }
        public DateTime ExportDate { get; set; }
        public List<TextResourceEntry> Resources { get; set; } = new();
    }
}
