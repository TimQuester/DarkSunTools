using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using DSunTools.Common.Font;
using DSunTools.Common.Gff;
using DSunTools.Common.Gpl;

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
    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--generate-cyrillic")
                return GenerateCyrillicFont(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--scan-text-bytes")
                return ScanTextByteUsage(args[1]);
            if (args.Length > 0)
            {
                Console.Error.WriteLine("Usage: FontTool --generate-cyrillic <font.bin> <output-directory>");
                Console.Error.WriteLine("       FontTool --scan-text-bytes <GFF-file-or-directory>");
                return 2;
            }

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
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int GenerateCyrillicFont(string sourcePath, string outputDirectory)
    {
        const string uppercase = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        const string lowercase = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        const string latinLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        const string extraGlyphSlots = "%[]^`{|}~@\"+#/";
        const string aliasedPunctuation = "@\"+#/";
        if (uppercase.Length != 33 || lowercase.Length != uppercase.Length ||
            latinLetters.Length != 52 || extraGlyphSlots.Length != 14 || extraGlyphSlots.Distinct().Count() != extraGlyphSlots.Length)
            throw new InvalidOperationException("The Cyrillic font requires 33 upper/lower pairs and 14 unique extra ASCII slots.");

        var mappings = new List<(int Code, string Category, char Character)>(66);
        for (int i = 0; i < uppercase.Length; i++)
        {
            int upperCode = i < 26 ? latinLetters[i] : extraGlyphSlots[i - 26];
            int lowerCode = i < 26 ? latinLetters[26 + i] : extraGlyphSlots[7 + i - 26];
            mappings.Add((upperCode, "upper", uppercase[i]));
            mappings.Add((lowerCode, "lower", lowercase[i]));
        }
        if (mappings.Select(mapping => mapping.Code).Distinct().Count() != 66)
            throw new InvalidOperationException("Cyrillic glyph byte assignments must be unique.");

        FontResource source = FontResourceCodec.Parse(File.ReadAllBytes(sourcePath));
        var glyphs = source.Glyphs.ToArray();
        using var font = new Font("Arial", 8.5f, FontStyle.Regular, GraphicsUnit.Pixel);

        foreach (var mapping in mappings)
            glyphs[mapping.Code] = RenderGlyph(mapping.Code, mapping.Character, font);
        for (int i = 0; i < aliasedPunctuation.Length; i++)
            glyphs[0xC2 + i] = RenderGlyph(0xC2 + i, aliasedPunctuation[i], font);

        Directory.CreateDirectory(outputDirectory);
        string fontPath = Path.Combine(outputDirectory, "FONT_cyrillic.bin");
        byte[] generatedFont = FontResourceCodec.Build(new FontResource
        {
            Header = source.Header,
            Glyphs = glyphs.ToList()
        });
        FontResource verification = FontResourceCodec.Parse(generatedFont);
        if (!generatedFont.SequenceEqual(FontResourceCodec.Build(verification)) ||
            !mappings.All(mapping => HasInk(verification.Glyphs[mapping.Code])) ||
            !Enumerable.Range(0, aliasedPunctuation.Length).All(index => HasInk(verification.Glyphs[0xC2 + index])))
            throw new InvalidDataException("Generated FONT failed its glyph or byte-for-byte round-trip validation.");
        File.WriteAllBytes(fontPath, generatedFont);

        string previewPath = Path.Combine(outputDirectory, "cyrillic_preview.png");
        SavePreview(verification, mappings, previewPath);

        string csvPath = Path.Combine(outputDirectory, "cyrillic_mapping.csv");
        using (var writer = new StreamWriter(csvPath, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("ByteHex,ByteDecimal,Category,Character,ProjectRule");
            foreach (var mapping in mappings)
                WriteMappingRow(writer, mapping.Code, mapping.Category, mapping.Character);
            for (int i = 0; i < aliasedPunctuation.Length; i++)
                writer.WriteLine($"0x{0xC2 + i:X2},{0xC2 + i},preserved symbol,{EscapeCsv(aliasedPunctuation[i].ToString())},0x{0xC2 + i:X2}={aliasedPunctuation[i]}");
        }

        string asciiRulesPath = Path.Combine(outputDirectory, "cyrillic_rules_gpl.txt");
        string[] asciiRules = mappings.Select(mapping => $"{(char)mapping.Code}={mapping.Character}").ToArray();
        File.WriteAllText(asciiRulesPath, string.Join(";", asciiRules), new UTF8Encoding(true));

        string combinedRulesPath = Path.Combine(outputDirectory, "cyrillic_rules.txt");
        File.WriteAllText(combinedRulesPath, string.Join(";", asciiRules.Concat(
            Enumerable.Range(0, aliasedPunctuation.Length).Select(index => $"0x{0xC2 + index:X2}={aliasedPunctuation[index]}"))), new UTF8Encoding(true));

        string readmePath = Path.Combine(outputDirectory, "README.txt");
        File.WriteAllText(readmePath,
            "FONT_cyrillic.bin is a FONT resource chunk, built from the supplied game's FONT resource.\r\n" +
            "The 66 Russian uppercase/lowercase glyphs use printable ASCII slots for compatibility with 7-bit GPL strings.\r\n" +
            "The repurposed ASCII slots are A-Z, a-z, % [ ] ^ ` { | } ~ @ \" # + /. Those Latin letters/symbols change.\r\n" +
            "Use cyrillic_rules.txt in Project Settings. It also maps five punctuation aliases at bytes 0xC2-0xC6 for TEXT/NAME.\r\n" +
            "Export and re-import TEXT/NAME resources for these five symbols to be moved to their high-byte aliases.\r\n" +
            "GPL strings are 7-bit packed; high-byte punctuation aliases are filtered from GPL encoding.\r\n" +
            "cyrillic_rules_gpl.txt contains only the 7-bit Russian glyph mappings.\r\n" +
            "Original glyphs 0x80-0xC1 are preserved because the installed game data uses some of those byte values.\r\n" +
            "Corpus scan found five uses of repurposed punctuation in parsed DS1/DS2 GPL strings (#, +, /); inspect these.\r\n" +
            "This is a prototype: five low-frequency punctuation glyphs in GPL cannot be preserved by high-byte aliases.\r\n" +
            "Replace FONT-100 in each game's RESOURCE.GFF with this chunk. Use a matching game font version.\r\n" +
            "The source game font must be the same format/version as the target game.\r\n",
            new UTF8Encoding(true));

        Console.WriteLine($"Created {fontPath} ({new FileInfo(fontPath).Length} bytes), byte-for-byte round-trip verified.");
        Console.WriteLine($"Created {previewPath}, {csvPath}, {asciiRulesPath}, {combinedRulesPath}, and {readmePath}");
        Console.WriteLine("Generated and verified 66 ASCII Russian glyphs and five punctuation aliases at 0xC2-0xC6; original glyphs 0x80-0xC1 were preserved.");
        return 0;
    }

    private static void WriteMappingRow(TextWriter writer, int code, string letterCase, char character)
    {
        writer.WriteLine($"0x{code:X2},{code},{letterCase},{character},{(char)code}={character}");
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool HasInk(FontGlyph glyph)
    {
        for (int y = 0; y < glyph.Pixels.GetLength(0); y++)
        {
            for (int x = 0; x < glyph.Pixels.GetLength(1); x++)
            {
                if (glyph.Pixels[y, x] != 0)
                    return true;
            }
        }
        return false;
    }

    private static void SavePreview(FontResource font, IReadOnlyList<(int Code, string Category, char Character)> mappings, string path)
    {
        const int columns = 11;
        const int cellWidth = 52;
        const int cellHeight = 42;
        using var bitmap = new Bitmap(columns * cellWidth, 6 * cellHeight, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        using var labelFont = new Font("Arial", 8, FontStyle.Regular, GraphicsUnit.Pixel);
        using var labelBrush = new SolidBrush(Color.Black);

        for (int i = 0; i < mappings.Count; i++)
        {
            var mapping = mappings[i];
            FontGlyph glyph = font.Glyphs[mapping.Code];
            int cellX = (i % columns) * cellWidth;
            int cellY = (i / columns) * cellHeight;
            for (int y = 0; y < glyph.Pixels.GetLength(0); y++)
            {
                for (int x = 0; x < glyph.Pixels.GetLength(1); x++)
                {
                    if (glyph.Pixels[y, x] != 0)
                        graphics.FillRectangle(Brushes.Black, cellX + 4 + x * 3, cellY + 2 + y * 3, 3, 3);
                }
            }

            graphics.DrawString($"{mapping.Character} 0x{mapping.Code:X2}", labelFont, labelBrush, cellX + 2, cellY + 31);
        }

        bitmap.Save(path, ImageFormat.Png);
    }

    private static FontGlyph RenderGlyph(int code, char character, Font font)
    {
        using var bitmap = new Bitmap(32, 16, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.DrawString(character.ToString(), font, Brushes.White, new PointF(1, -2));
        }

        int minX = bitmap.Width;
        int minY = bitmap.Height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).R == 0)
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            throw new InvalidDataException($"System font did not produce pixels for U+{(int)character:X4}.");

        int sourceWidth = maxX - minX + 1;
        int sourceHeight = maxY - minY + 1;
        int width = sourceWidth + 1;
        var pixels = new byte[FontResourceCodec.GlyphHeight, width];
        int targetTop = Math.Max(0, (FontResourceCodec.GlyphHeight - sourceHeight) / 2);
        for (int y = 0; y < Math.Min(sourceHeight, FontResourceCodec.GlyphHeight); y++)
        {
            for (int x = 0; x < sourceWidth; x++)
            {
                if (bitmap.GetPixel(minX + x, minY + y).R > 0)
                    pixels[targetTop + y, x] = 0xFE;
            }
        }

        return new FontGlyph { CharacterCode = code, Width = checked((ushort)width), Pixels = pixels };
    }

    private static int ScanTextByteUsage(string inputPath)
    {
        string[] paths = File.Exists(inputPath)
            ? new[] { inputPath }
            : Directory.Exists(inputPath)
                ? Directory.GetFiles(inputPath, "*.gff", SearchOption.AllDirectories)
                : throw new FileNotFoundException("Input GFF file or directory was not found.", inputPath);
        var counts = new long[256];
        var textCounts = new long[256];
        var scriptCounts = new long[256];
        var textTags = new HashSet<string>(StringComparer.Ordinal) { "TEXT", "MSG ", "MERR", "ETME", "SPIN", "RNME", "NAME" };
        var scriptTags = new HashSet<string>(StringComparer.Ordinal) { "GPL ", "MAS " };
        var failures = new List<string>();
        var unalignedScripts = new List<string>();
        int resourceCount = 0;

        foreach (string path in paths)
        {
            try
            {
                GffFile gff = GffFile.Create(File.ReadAllBytes(path));
                foreach (ResourceDescriptor resource in gff.DescribeResources().Where(item => textTags.Contains(item.Tag) || scriptTags.Contains(item.Tag)))
                {
                    resourceCount++;
                    byte[] data = gff.GetResourceData(resource.Tag, resource.Number);
                    if (textTags.Contains(resource.Tag))
                    {
                        foreach (byte value in data)
                        {
                            counts[value]++;
                            textCounts[value]++;
                        }
                    }
                    else
                    {
                        GplParseResult parsed = GplChunkParser.Parse(data);
                        foreach (char character in parsed.Strings.SelectMany(span => span.Text))
                        {
                            if (character < counts.Length)
                            {
                                counts[character]++;
                                scriptCounts[character]++;
                            }
                        }
                        if (!parsed.Aligned)
                            unalignedScripts.Add($"{Path.GetFileName(path)}:{resource.Tag}-{resource.Number}");
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (failures.Count == paths.Length)
            throw new InvalidDataException("No valid GFF containers could be scanned. " + string.Join("; ", failures.Take(3)));

        Console.WriteLine($"Scanned {resourceCount} text/name resources in {paths.Length - failures.Count} valid GFF file(s).");
        if (failures.Count > 0)
            Console.WriteLine($"Skipped {failures.Count} invalid/non-container GFF file(s): {string.Join(", ", failures.Select(Path.GetFileName))}");
        if (unalignedScripts.Count > 0)
            Console.WriteLine($"Warning: {unalignedScripts.Count} GPL/MAS chunks were only partially parsed; byte counts may omit their later strings.");
        var punctuation = Enumerable.Range(0x21, 0x5E)
            .Select(code => (char)code)
            .Where(character => !char.IsLetterOrDigit(character));
        Console.WriteLine("Printable punctuation usage (character:TEXT/NAME,GPL/MAS): " +
            string.Join(" ", punctuation.Select(character => $"'{character}':{textCounts[character]}/{scriptCounts[character]}")));
        Console.WriteLine("Unused punctuation candidates in TEXT/NAME/GPL/MAS strings: " +
            string.Join("", punctuation.Where(character => counts[character] == 0)));
        Console.WriteLine("Proposed punctuation alias byte counts (0xC2-0xC6): " +
            string.Join(", ", Enumerable.Range(0xC2, 5).Select(code => $"{code:X2}:{counts[code]}")));
        return 0;
    }
}