using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    public class ProjectCreationData
    {
        public string ProjectName { get; set; }
        public string OriginalGamePath { get; set; }
        public string ModifiedGamePath { get; set; }
        public string GitRepositoryPath { get; set; }
        public ProjectType GameType { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Version { get; set; }
    }
}
