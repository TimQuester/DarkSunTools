using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DarkSunRE.Models
{
    public class GameDefinition
    {
        public ProjectType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Developer { get; set; }
        public string Publisher { get; set; }
        public int ReleaseYear { get; set; }
        public string DefaultExecutable { get; set; }
        public string[] SupportedFileFormats { get; set; }
        public string IconResourcePath { get; set; }
        public string[] RequiredFiles { get; set; }
        public string[] OptionalFiles { get; set; }
        public Dictionary<string, string> FileDescriptions { get; set; }
        public VersionInfo[] KnownVersions { get; set; }
    }

    public class VersionInfo
    {
        public string Version { get; set; }
        public long ExecutableSize { get; set; }
        public string ExecutableHash { get; set; }
        public string Description { get; set; }
    }
}
