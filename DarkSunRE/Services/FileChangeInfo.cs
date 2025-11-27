using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class FileChangeInfo
    {
        public string FilePath { get; set; }
        public FileChangeStatus Status { get; set; }
        public int Insertions { get; set; }
        public int Deletions { get; set; }
        public string Patch { get; set; }
    }
}
