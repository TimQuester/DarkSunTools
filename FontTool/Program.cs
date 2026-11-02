using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using DSunTools.Common.Font;

public class Glyph
{
    public int Width { get; set; }
    public required byte[,] Pixels { get; set; }
    public int CharCode { get; set; }
    public bool IsEmpty => Width == 0 || Pixels == null;
}

public class FontParser
{
    private FontResource _fontResource;

    public List<Glyph> ParseFontDump(byte[] data)
    {
        _fontResource = FontResourceCodec.Parse(data);
        return _fontResource.Glyphs.Select(glyph => new Glyph
        {
            CharCode = glyph.CharacterCode,
            Width = glyph.Width,
            Pixels = glyph.Pixels
        }).ToList();
    }

    public List<Glyph> LoadGlyphsFromPngs(string directory, IReadOnlyList<Glyph> originalGlyphs)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Glyph directory not found: {directory}");
        if (originalGlyphs == null || originalGlyphs.Count != FontResourceCodec.GlyphCount)
            throw new InvalidDataException("Exactly 256 original glyphs are required as the PNG import baseline.");

        var glyphs = new List<Glyph>(FontResourceCodec.GlyphCount);
        for (int i = 0; i < FontResourceCodec.GlyphCount; i++)
        {
            string filePath = Path.Combine(directory, $"glyph_{i}.png");
            if (!File.Exists(filePath))
            {
                glyphs.Add(originalGlyphs[i]);
                continue;
            }

            using var bmp = new Bitmap(filePath);
            if (bmp.Height != FontResourceCodec.GlyphHeight || bmp.Width > ushort.MaxValue)
                throw new InvalidDataException($"{Path.GetFileName(filePath)} must be 1..65535 pixels wide and {FontResourceCodec.GlyphHeight} pixels high.");

            var pixels = new byte[FontResourceCodec.GlyphHeight, bmp.Width];
            for (int y = 0; y < FontResourceCodec.GlyphHeight; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                    pixels[y, x] = bmp.GetPixel(x, y).R;
            }

            glyphs.Add(new Glyph { CharCode = i, Width = bmp.Width, Pixels = pixels });
        }

        return glyphs;
    }

    public void SaveGlyphs(List<Glyph> glyphs, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var glyph in glyphs.Where(g => !g.IsEmpty))
        {
            string filename = Path.Combine(outputDir, $"glyph_{glyph.CharCode}.png");
            using (var bmp = new Bitmap(glyph.Width, FontResourceCodec.GlyphHeight, PixelFormat.Format8bppIndexed))
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
                    for (int y = 0; y < FontResourceCodec.GlyphHeight; y++)
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
        if (_fontResource == null)
            throw new InvalidOperationException("FONT data must be parsed before it can be rebuilt.");

        return FontResourceCodec.Build(new FontResource
        {
            Header = _fontResource.Header,
            Glyphs = glyphs.Select(glyph => new FontGlyph
            {
                CharacterCode = glyph.CharCode,
                Width = checked((ushort)glyph.Width),
                Pixels = glyph.Pixels
            }).ToList()
        });
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

            // Missing PNGs keep their original glyphs; importing only replaces explicitly edited files.
            var modifiedGlyphs = parser.LoadGlyphsFromPngs("modified_glyphs", originalGlyphs);
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