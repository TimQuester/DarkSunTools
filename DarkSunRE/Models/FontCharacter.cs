using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DarkSunRE.Models
{
    public class FontCharacter
    {
        public int CharCode { get; set; }
        public int Width { get; set; }
        public byte[,] Pixels { get; set; }
        public bool IsEmpty => Width == 0 || Pixels == null;
        
        // Transliteration for the character if available
        public string Transliteration { get; set; }
        
        public string LatinCharacter => CharCode is >= 32 and <= 126 ? ((char)CharCode).ToString() : string.Empty;

        public bool IsControlCharacter => CharCode < 32 || CharCode == 127;
        
        // Pre-rendered image for display
        public ImageSource Image { get; set; }
    }
}