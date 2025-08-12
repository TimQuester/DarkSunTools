using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

public class Glyph
{
    public int Width { get; set; }
    public byte[,] Pixels { get; set; }
    public int CharCode { get; set; }
    public bool IsEmpty => Width == 0 || Pixels == null;
}

public class FontParser
{
    private const int Height = 9;
    private const int WidthBytes = 2;
    private const int HeaderSize = 0x347;

    public byte[] FontHeader { get; private set; }

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

    public List<Glyph> LoadGlyphsFromPngs(string directory, int totalGlyphs = 224)
    {
        var glyphs = new List<Glyph>();
        if (!Directory.Exists(directory)) return glyphs;

        // Создаем все глифы (включая пустые)
        for (int i = 0; i < totalGlyphs; i++)
        {
            string fileName = $"glyph_{i}.png";
            string filePath = Path.Combine(directory, fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    using (var bmp = new Bitmap(filePath))
                    {
                        if (bmp.Height != Height)
                        {
                            Console.WriteLine($"Warning: {fileName} has wrong height ({bmp.Height} instead of {Height})");
                            continue;
                        }

                        var pixels = new byte[Height, bmp.Width];
                        for (int y = 0; y < Height; y++)
                        {
                            for (int x = 0; x < bmp.Width; x++)
                            {
                                Color color = bmp.GetPixel(x, y);
                                pixels[y, x] = color.R;
                            }
                        }

                        glyphs.Add(new Glyph
                        {
                            CharCode = i,
                            Width = bmp.Width,
                            Pixels = pixels
                        });
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading {fileName}: {ex.Message}");
                }
            }

            // Добавляем пустой глиф, если файл не найден или ошибка
            glyphs.Add(new Glyph
            {
                CharCode = i,
                Width = 0,
                Pixels = new byte[Height, 0]
            });
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

        foreach (var glyph in glyphs.Where(g => !g.IsEmpty))
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
                bmp.MakeTransparent(Color.Black);
                bmp.Save(filename, ImageFormat.Png);
            }
        }
    }

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

            writer.Write((byte)0);

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

            // 2. Сохранение только непустых глифов в PNG
            parser.SaveGlyphs(originalGlyphs, "original_glyphs");
            Console.WriteLine($"Saved {originalGlyphs.Count(g => !g.IsEmpty)} original glyphs to PNG");

            // 3. Загрузка глифов из PNG (с автоматическим созданием пустых для пропущенных)
            var modifiedGlyphs = parser.LoadGlyphsFromPngs("modified_glyphs");
            Console.WriteLine($"Loaded {modifiedGlyphs.Count(g => !g.IsEmpty)} modified glyphs from PNG");

            // 4. Сборка модифицированного шрифта
            if (modifiedGlyphs.Count > 0)
            {
                byte[] newFontData = parser.BuildFontDump(modifiedGlyphs);
                File.WriteAllBytes("font_rebuilt.bin", newFontData);
                Console.WriteLine($"Rebuilt font saved to font_rebuilt.bin ({newFontData.Length} bytes)");
            }

            // 5. Проверка целостности
            Console.WriteLine("\nFirst 10 glyphs status:");
            for (int i = 0; i < 10; i++)
            {
                var g = modifiedGlyphs[i];
                Console.WriteLine($"Glyph {i}: {(g.IsEmpty ? "Empty" : $"Width={g.Width}")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}