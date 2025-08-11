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
    private const int Height = 9; // Фиксированная высота 9 пикселей
    private const int WidthBytes = 2; // 2 байта на ширину

    public List<Glyph> ParseFontDump(byte[] data)
    {
        var glyphs = new List<Glyph>();
        int offset = 0x347; // Начало первого символа
        int charCode = 0;

        while (offset + WidthBytes < data.Length)
        {
            // Читаем ширину (2 байта, используем второй байт)
            int width = data[offset + 1];
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

            offset = dataEnd; // Переходим к следующему символу
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

    public void SaveGlyphs(List<Glyph> glyphs, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var glyph in glyphs)
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

class Program
{
    static void Main()
    {
        try
        {
            byte[] data = File.ReadAllBytes("C:\\Users\\quest\\source\\repos\\dsun_music\\gff-tool\\target\\extracted\\FONT-100 ");
            var parser = new FontParser();
            var glyphs = parser.ParseFontDump(data);

            Console.WriteLine($"Total glyphs parsed: {glyphs.Count}");
            parser.SaveGlyphs(glyphs, "output_glyphs");

            // Детальная диагностика первых символов
            Console.WriteLine("\nFirst 10 glyphs:");
            for (int i = 0; i < 10 && i < glyphs.Count; i++)
            {
                var g = glyphs[i];
                Console.WriteLine($"Glyph {g.CharCode} at 0x{g.Offset:X4}: Width={g.Width}");

                Console.WriteLine("First 3 rows:");
                for (int y = 0; y < 3; y++)
                {
                    Console.Write("    ");
                    for (int x = 0; x < g.Width; x++)
                    {
                        Console.Write(g.Pixels[y, x].ToString("X2") + " ");
                    }
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}