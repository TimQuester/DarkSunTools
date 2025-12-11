using System;
using System.Collections.Generic;

namespace DarkSunRE.Models
{
    public class ProjectCreationData
    {
        public string ProjectName { get; set; }
        public string OriginalGamePath { get; set; }
        public string ModifiedGamePath { get; set; }
        public string JsonFolderPath { get; set; }
        public ProjectType GameType { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<TransliterationRule> TransliterationRules { get; set; } = new();
    }
}