using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    public class GffFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public GffFile GffFile { get; set; }
        public List<ResourceInfo> Resources { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsModified { get; set; }
        public string SHA256Hash { get; set; }
    }
}
