using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class CommitInfo
    {
        public string Sha { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }
        public string Email { get; set; }
        public DateTimeOffset Date { get; set; }
        public int FilesChanged { get; set; }
        public int Insertions { get; set; }
        public int Deletions { get; set; }
        public string Branch { get; set; }
        public IReadOnlyList<string> ChangedFiles { get; set; }
        public CommitType CommitType { get; set; }
        public string ProjectVersion { get; set; }
    }
}
