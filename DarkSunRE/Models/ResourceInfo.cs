using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DarkSunRE.Models
{
    public class ResourceInfo
    {
        public string Tag { get; set; }
        public int Number { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
        public ResourceType Type { get; set; }
        public string Description { get; set; }
        public ImageSource Thumbnail { get; set; }
        public bool IsModified { get; set; }
    }
}
