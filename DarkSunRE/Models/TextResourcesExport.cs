using System;
using System.Collections.Generic;

namespace DarkSunRE.Models
{
    /// <summary>
    /// Single text resource entry (TEXT, MSG, MERR, ETME, SPIN, RNME, NAME)
    /// for batch export/import.
    /// </summary>
    public class TextResourceEntry
    {
        public string ResourceId { get; set; } // "TAG-NUMBER"
        public string ResourceType { get; set; } // "TEXT", "NAME", etc.
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
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
