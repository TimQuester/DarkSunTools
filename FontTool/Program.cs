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
}

public class FontParser
{
    public const int Height = 9;
    public const int WidthBytes = 2;
    public const int HeaderSize = 0x347;

    public byte[] FontHeader { get; private set; }

    // Загрузка шрифта из бинарного файла
    public List<Glyph> ParseFontDump(byte[] data)
    {
        FontHeader = new byte[HeaderSize];
        Array.Copy(data, 0, FontHeader, 0, HeaderSize);

        var glyphs = new List<Glyph>();
        int offset = HeaderSize;
        int charCode = 0;

        while (offset + WidthBytes < data.Length)
        {
            int width = data[offset + 1];
            int dataSize = width * Height;
            int dataStart = offset + WidthBytes;
            int dataEnd = dataStart + dataSize;

            if (dataEnd > data.Length) break;

            var glyphData = new byte[dataSize];
            Array.Copy(data, dataStart, glyphData, 0, dataSize);

            glyphs.Add(new Glyph
            {
                CharCode = charCode++,
                Width = width,
                Pixels = DecodePixels(glyphData, width)
            });

            offset = dataEnd;
        }

        return glyphs;
    }

    // Загрузка глифов из PNG файлов
    public List<Glyph> LoadGlyphsFromPngs(string directory)
    {
        var glyphs = new List<Glyph>();
        var files = Directory.GetFiles(directory, "glyph_*.png")
                            .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Substring(6),
                                     System.Globalization.NumberStyles.HexNumber));

        foreach (var file in files)
        {
            using (var bmp = new Bitmap(file))
            {
                var pixels = new byte[Height, bmp.Width];

                for (int y = 0; y < Height && y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color color = bmp.GetPixel(x, y);
                        pixels[y, x] = color.R; // Используем красный канал (для grayscale R=G=B)
                    }
                }

                int charCode = int.Parse(Path.GetFileNameWithoutExtension(file).Substring(6),
                                       System.Globalization.NumberStyles.HexNumber);

                glyphs.Add(new Glyph
                {
                    CharCode = charCode,
                    Width = bmp.Width,
                    Pixels = pixels
                });
            }
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

    // Сохранение глифов в PNG
    public void SaveGlyphs(List<Glyph> glyphs, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var glyph in glyphs)
        {
            if (glyph.Width > 0)
            {
                string filename = Path.Combine(outputDir, $"glyph_{glyph.CharCode}.png");
                using (var bmp = new Bitmap(glyph.Width, Height, PixelFormat.Format8bppIndexed))
                {
                    // Настраиваем палитру в градациях серого
                    ColorPalette palette = bmp.Palette;
                    for (int i = 0; i < 256; i++)
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    bmp.Palette = palette;

                    var bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                                ImageLockMode.WriteOnly,
                                                bmp.PixelFormat);

                    unsafe
                    {
                        byte* ptr = (byte*)bitmapData.Scan0;
                        for (int y = 0; y < Height; y++)
                        {
                            for (int x = 0; x < glyph.Width; x++)
                            {
                                ptr[y * bitmapData.Stride + x] = glyph.Pixels[y, x];
                            }
                        }
                    }

                    bmp.UnlockBits(bitmapData);
                    bmp.Save(filename, ImageFormat.Png);
                }
            }
            else
            {

            }
        }
    }

    // Сборка шрифта обратно в бинарный формат
    public byte[] BuildFontDump(List<Glyph> glyphs)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 1. Записываем заголовок
            writer.Write(FontHeader);

            // 2. Сортируем глифы по CharCode
            var sortedGlyphs = glyphs.OrderBy(g => g.CharCode).ToList();

            // 3. Записываем глифы
            foreach (var glyph in sortedGlyphs)
            {
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

            return ms.ToArray();
        }
    }
}

class Program
{
    static void Main()
    {
        try
        {
            var parser = new FontParser();

            // 1. Чтение оригинального шрифта
            byte[] originalFont = File.ReadAllBytes("font_dump.bin");
            var originalGlyphs = parser.ParseFontDump(originalFont);

            // 2. Сохранение оригинальных глифов в PNG
            parser.SaveGlyphs(originalGlyphs, "original_glyphs");
            Console.WriteLine($"Saved {originalGlyphs.Count} original glyphs to PNG");

            // 3. Загрузка модифицированных глифов из PNG
            var modifiedGlyphs = parser.LoadGlyphsFromPngs("modified_glyphs");
            Console.WriteLine($"Loaded {modifiedGlyphs.Count} modified glyphs from PNG");

            // 4. Сборка модифицированного шрифта
            if (modifiedGlyphs.Count > 0)
            {
                byte[] newFontData = parser.BuildFontDump(modifiedGlyphs);
                File.WriteAllBytes("font_rebuilt.bin", newFontData);
                Console.WriteLine($"Rebuilt font saved to font_rebuilt.bin");
            }

            // 5. Создание нового шрифта из PNG (пример)
            var newGlyphs = new List<Glyph>();
            for (int i = 0; i < 256; i++)
            {
                string pngPath = $"new_glyphs/glyph_{i}.png";
                if (File.Exists(pngPath))
                {
                    using (var bmp = new Bitmap(pngPath))
                    {
                        var pixels = new byte[FontParser.Height, bmp.Width];
                        for (int y = 0; y < FontParser.Height; y++)
                        {
                            for (int x = 0; x < bmp.Width; x++)
                            {
                                Color color = bmp.GetPixel(x, y);
                                pixels[y, x] = color.R;
                            }
                        }

                        newGlyphs.Add(new Glyph
                        {
                            CharCode = i,
                            Width = bmp.Width,
                            Pixels = pixels
                        });
                    }
                }
            }

            if (newGlyphs.Count > 0)
            {
                byte[] newFont = parser.BuildFontDump(newGlyphs);
                File.WriteAllBytes("new_font.bin", newFont);
                Console.WriteLine($"Created new font with {newGlyphs.Count} glyphs");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}