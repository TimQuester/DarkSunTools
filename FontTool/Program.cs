using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

public class Glyph
{
    public int Width { get; set; }
    public byte[,] Pixels { get; set; }
    public int CharCode { get; set; }
    public int Offset { get; set; }
}

public class FontParser
{
    public const int Height = 9;
    public const int WidthBytes = 2;
    public const int HeaderSize = 0x347; // Начало данных шрифта

    // Заголовок шрифта (первые 0x346 байт)
    public byte[] FontHeader { get; private set; }

    public List<Glyph> ParseFontDump(byte[] data)
    {
        // Сохраняем заголовок
        FontHeader = new byte[HeaderSize];
        Array.Copy(data, 0, FontHeader, 0, HeaderSize);

        var glyphs = new List<Glyph>();
        int offset = HeaderSize;
        int charCode = 0;

        while (offset + WidthBytes < data.Length)
        {
            int width = data[offset + 1]; // Ширина из второго байта
            int dataSize = width * Height;
            int dataStart = offset + WidthBytes;
            int dataEnd = dataStart + dataSize;

            if (dataEnd > data.Length)
            {
                Console.WriteLine($"Warning: Not enough data for glyph {charCode} at 0x{offset:X4}");
                break;
            }

            var glyphData = new byte[dataSize];
            Array.Copy(data, dataStart, glyphData, 0, dataSize);

            glyphs.Add(new Glyph
            {
                CharCode = charCode++,
                Width = width,
                Pixels = DecodePixels(glyphData, width),
                Offset = offset
            });

            offset = dataEnd;
        }

        return glyphs;
    }

    private byte[,] DecodePixels(byte[] data, int width)
    {
        var pixels = new byte[Height, width];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                pixels[y, x] = data[index];
            }
        }

        return pixels;
    }

    public byte[] BuildFontDump(List<Glyph> glyphs)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 1. Записываем заголовок
            writer.Write(FontHeader);

            // 2. Записываем глифы
            foreach (var glyph in glyphs)
            {
                // Записываем ширину (2 байта)
                writer.Write((byte)0); // Первый байт ширины
                writer.Write((byte)glyph.Width); // Второй байт ширины

                // Записываем данные пикселей
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < glyph.Width; x++)
                    {
                        writer.Write(glyph.Pixels[y, x]);
                    }
                }
            }

            writer.Write((byte)0); //Не хватает одного байта

            return ms.ToArray();
        }
    }

    public void SaveGlyphs(List<Glyph> glyphs, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var glyph in glyphs)
        {
            if (glyph.Width > 0)
            {
                string filename = Path.Combine(outputDir, $"glyph_{glyph.CharCode}.png");
                using (var bmp = new Bitmap(glyph.Width, Height))
                {
                    for (int y = 0; y < Height; y++)
                    {
                        for (int x = 0; x < glyph.Width; x++)
                        {
                            byte value = glyph.Pixels[y, x];
                            bmp.SetPixel(x, y, Color.FromArgb(value, value, value));
                        }
                    }
                    bmp.Save(filename, ImageFormat.Png);
                }
            }
        }
    }
}

class Program
{
    static void Main()
    {
        try
        {
            // 1. Чтение и парсинг шрифта
            byte[] fontData = File.ReadAllBytes("C:\\Users\\quest\\source\\repos\\dsun_music\\gff-tool\\target\\extracted\\FONT-100 ");
            var parser = new FontParser();
            var glyphs = parser.ParseFontDump(fontData);

            Console.WriteLine($"Total glyphs parsed: {glyphs.Count}");
            parser.SaveGlyphs(glyphs, "output_glyphs");

            // 2. Сборка обратно в бинарный формат
            byte[] newFontData = parser.BuildFontDump(glyphs);
            File.WriteAllBytes("font_modified.bin", newFontData);
            Console.WriteLine($"Modified font saved to font_modified.bin");

            // Диагностика
            Console.WriteLine("\nFirst 5 glyphs info:");
            for (int i = 0; i < 5 && i < glyphs.Count; i++)
            {
                var g = glyphs[i];
                Console.WriteLine($"Glyph {g.CharCode}: Width={g.Width}, " +
                                 $"Data size={g.Width * FontParser.Height} bytes, " +
                                 $"Offset=0x{g.Offset:X4}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}