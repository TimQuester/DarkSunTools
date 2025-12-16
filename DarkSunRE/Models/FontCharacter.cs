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
        
        // Latin character representation (if not a control character)
        // For display purposes, we offset the character code by 32 to match visual representation
        public string LatinCharacter
        {
            get
            {
                int displayCharCode = CharCode + 32;
                if (displayCharCode >= 32 && displayCharCode <= 126)
                {
                    return ((char)displayCharCode).ToString();
                }
                return string.Empty;
            }
        }
        
        // Check if this is a control character
        // For display purposes, we check against the visual character code (offset by 32)
        public bool IsControlCharacter => (CharCode + 32) < 32 || (CharCode + 32) == 127;
        
        // Pre-rendered image for display
        public ImageSource Image { get; set; }
    }
}