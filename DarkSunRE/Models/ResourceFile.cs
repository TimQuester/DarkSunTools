using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    public class ResourceFile
    {
        public FileType Type;
        public string Name;
        public string FullPath;
        public byte[] Data;
        public bool IsModified { get; set; }
        public List<ResourceInfo> Resources { get; set; }
    }
        
}
