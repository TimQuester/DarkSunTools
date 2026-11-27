using DarkSunRE.Models;
using DSunTools.Common.Font;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DarkSunRE.Services
{
    /// <summary>
    /// Rebuilds the FONT chunk from the original font plus every glyph PNG in
    /// the project's Glyphs\Overrides folder. Generation uses this even when
    /// the font editor was never opened, so new glyphs are not lost.
    /// </summary>
    public static class FontGenerationService
    {
        public static bool HasGlyphOverrides(Project project)
        {
            string directory = GetOverridesPath(project);
            return !string.IsNullOrEmpty(directory) &&
                   Directory.Exists(directory) &&
                   Directory.EnumerateFiles(directory, "glyph_*.png").Any();
        }

        public static byte[] BuildWithOverrides(Project project, byte[] baseFontData)
        {
            if (baseFontData == null || baseFontData.Length == 0)
                throw new InvalidDataException("The original FONT chunk is empty.");

            string directory = GetOverridesPath(project);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return baseFontData;

            FontResource font = FontResourceCodec.Parse(baseFontData);
            var glyphsByCode = new Dictionary<int, FontGlyph>();
            foreach (FontGlyph glyph in font.Glyphs)
                glyphsByCode[glyph.CharacterCode] = glyph;

            foreach (string path in Directory.EnumerateFiles(directory, "glyph_*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("glyph_", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(name.Substring("glyph_".Length), out int code) ||
                    code < 0 || code >= FontResourceCodec.GlyphCount)
                {
                    continue;
                }

                FontGlyph glyph = glyphsByCode[code];
                (ushort width, byte[,] pixels) = LoadGlyph(path);
                glyph.Width = width;
                glyph.Pixels = pixels;
            }

            return FontResourceCodec.Build(font);
        }

        private static (ushort Width, byte[,] Pixels) LoadGlyph(string path)
        {
            BitmapDecoder decoder = BitmapDecoder.Create(
                new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException($"{Path.GetFileName(path)} contains no image frame.");

            BitmapSource source = decoder.Frames[0];
            if (source.PixelHeight != FontResourceCodec.GlyphHeight ||
                source.PixelWidth < 1 || source.PixelWidth > ushort.MaxValue)
            {
                throw new InvalidDataException(
                    $"{Path.GetFileName(path)} must be 1..{ushort.MaxValue} pixels wide and " +
                    $"{FontResourceCodec.GlyphHeight} pixels high.");
            }

            var grayscale = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
            int width = grayscale.PixelWidth;
            byte[] flat = new byte[checked(width * FontResourceCodec.GlyphHeight)];
            grayscale.CopyPixels(flat, width, 0);

            var pixels = new byte[FontResourceCodec.GlyphHeight, width];
            for (int y = 0; y < FontResourceCodec.GlyphHeight; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[y, x] = flat[y * width + x];
            }

            return ((ushort)width, pixels);
        }

        private static string GetOverridesPath(Project project)
        {
            if (string.IsNullOrWhiteSpace(project?.JsonFolderPath))
                return string.Empty;

            return Path.GetFullPath(Path.Combine(project.JsonFolderPath, "..", "Glyphs", "Overrides"));
        }
    }
}
