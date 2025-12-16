using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class FontParser
    {
        private const int Height = 9;
        private const int WidthBytes = 2;
        private const int HeaderSize = 0x347;

        public byte[] FontHeader { get; private set; }

        public List<FontCharacter> ParseFontData(byte[] data)
        {
            FontHeader = new byte[HeaderSize];
            Array.Copy(data, 0, FontHeader, 0, Math.Min(HeaderSize, data.Length));

            var characters = new List<FontCharacter>();
            int offset = HeaderSize;
            int charCode = 0;

            while (offset + WidthBytes < data.Length)
            {
                int width = data[offset + 1];
                int dataSize = width * Height;
                int dataStart = offset + WidthBytes;
                int dataEnd = dataStart + dataSize;

                if (dataEnd > data.Length) break;

                var characterData = new byte[dataSize];
                Array.Copy(data, dataStart, characterData, 0, dataSize);

                characters.Add(new FontCharacter
                {
                    CharCode = charCode,
                    Width = width > 0 ? width - 1 : 0, // Adjust width to account for removed column
                    Pixels = DecodePixels(characterData, width)
                });

                offset = dataEnd;
                charCode++;
            }

            return characters;
        }

        private byte[,] DecodePixels(byte[] data, int width)
        {
            // If width is greater than 0, we skip the first column of pixels (offset issue)
            int displayWidth = width > 0 ? width - 1 : 0;
            var pixels = new byte[Height, displayWidth];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < displayWidth; x++)
                {
                    // Skip first column (x+1) to compensate for the offset
                    int index = y * width + (x + 1);
                    pixels[y, x] = data[index];
                }
            }

            return pixels;
        }

        public byte[] BuildFontData(List<FontCharacter> characters)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // 1. Write header
                writer.Write(FontHeader);

                // 2. Sort characters by CharCode
                var sortedCharacters = characters.OrderBy(c => c.CharCode).ToList();

                // 3. Write characters
                foreach (var character in sortedCharacters)
                {
                    writer.Write((byte)0); // First width byte
                    writer.Write((byte)character.Width); // Second width byte

                    // Write pixel data
                    for (int y = 0; y < Height; y++)
                    {
                        for (int x = 0; x < character.Width; x++)
                        {
                            writer.Write(character.Pixels[y, x]);
                        }
                    }
                }

                writer.Write((byte)0);

                return ms.ToArray();
            }
        }
    }
}