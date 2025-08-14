using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    public class GPLString
    {
        public int StringNumber { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public string OriginalString { get; set; }
        public string NewString { get; set; }
        public byte[] OriginalBytes { get; set; }
        public byte[] NewBytes { get; set; }
    }
}
