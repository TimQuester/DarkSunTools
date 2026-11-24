using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DSunTools.Common.Image;
using MediaColor = System.Windows.Media.Color;

namespace DarkSunRE.Services
{
    /// <summary>
    /// PNG import/export for indexed animation frames. The PNG carries the
    /// 256-entry palette, so an exported frame round-trips without losing
    /// color indices.
    /// </summary>
    public static class AnimationPngCodec
    {
        public sealed class ImportedFrame
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public byte[] Pixels { get; init; }
            public IReadOnlyList<MediaColor> Palette { get; init; }
        }

        public sealed class PaletteRemapResult
        {
            public byte[] Pixels { get; init; }
            public int ExactPixels { get; init; }
            public int NearestPixels { get; init; }
        }

        public static void ExportFrame(
            string path,
            byte[] indexedPixels,
            int width,
            int height,
            Palette palette)
        {
            if (indexedPixels == null) throw new ArgumentNullException(nameof(indexedPixels));
            if (indexedPixels.Length != width * height)
                throw new ArgumentException("Pixel buffer does not match frame dimensions.", nameof(indexedPixels));

            BitmapPalette bitmapPalette = ToBitmapPalette(palette);
            BitmapSource bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Indexed8,
                bitmapPalette,
                indexedPixels,
                width);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
        }

        public static ImportedFrame ImportFrame(string path)
        {
            using FileStream stream = File.OpenRead(path);
            var decoder = new PngBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("PNG contains no frames.");

            BitmapFrame frame = decoder.Frames[0];
            if (frame.Format != PixelFormats.Indexed8)
            {
                throw new InvalidDataException(
                    $"PNG must be an 8-bit indexed image; got {frame.Format}.");
            }

            // Image editors often optimize an indexed PNG to only the colors
            // actually used (for example 38 instead of 256). The indices are
            // still 8-bit; BMA itself stores no palette, so a shorter PNG
            // palette is valid as long as it does not exceed 256 entries.
            if (frame.Palette == null || frame.Palette.Colors.Count == 0 || frame.Palette.Colors.Count > 256)
            {
                throw new InvalidDataException(
                    $"PNG must contain an indexed palette of 1..256 entries; " +
                    $"got {frame.Palette?.Colors.Count ?? 0} entries.");
            }

            int stride = (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8;
            var pixels = new byte[stride * frame.PixelHeight];
            frame.CopyPixels(pixels, stride, 0);

            return new ImportedFrame
            {
                Width = frame.PixelWidth,
                Height = frame.PixelHeight,
                Pixels = pixels,
                Palette = frame.Palette.Colors.ToList()
            };
        }

        /// <summary>
        /// Convert indices from the PNG's own palette to indices in the
        /// palette the game will use (ACF/PAL). Editors often reorder or
        /// shrink the PNG palette, so the numeric index cannot be trusted;
        /// the visible RGB color can. Exact colors are preferred, otherwise
        /// the nearest target color is used.
        /// </summary>
        public static PaletteRemapResult RemapToPalette(ImportedFrame frame, Palette targetPalette)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (targetPalette == null) throw new ArgumentNullException(nameof(targetPalette));

            var targetByColor = new Dictionary<int, byte>(256);
            for (int i = 0; i < 256; i++)
            {
                var color = targetPalette.GetColor(i);
                int key = (color.Red << 16) | (color.Green << 8) | color.Blue;
                if (!targetByColor.ContainsKey(key))
                    targetByColor[key] = (byte)i;
            }

            var cache = new Dictionary<int, (byte Index, bool Exact)>();
            var result = new byte[frame.Pixels.Length];
            int exact = 0;
            int nearest = 0;

            for (int i = 0; i < frame.Pixels.Length; i++)
            {
                int sourceIndex = frame.Pixels[i];
                if (sourceIndex < 0 || sourceIndex >= frame.Palette.Count)
                {
                    throw new InvalidDataException(
                        $"PNG pixel {i} uses palette index {sourceIndex}, " +
                        $"but the PNG palette has only {frame.Palette.Count} entries.");
                }

                MediaColor sourceColor = frame.Palette[sourceIndex];
                int colorKey = (sourceColor.R << 16) | (sourceColor.G << 8) | sourceColor.B;

                if (!cache.TryGetValue(colorKey, out var mapped))
                {
                    if (targetByColor.TryGetValue(colorKey, out byte targetIndex))
                        mapped = (targetIndex, true);
                    else
                        mapped = (FindNearestColor(targetPalette, sourceColor), false);

                    cache[colorKey] = mapped;
                }

                if (mapped.Exact)
                    exact++;
                else
                    nearest++;

                result[i] = mapped.Index;
            }

            return new PaletteRemapResult
            {
                Pixels = result,
                ExactPixels = exact,
                NearestPixels = nearest
            };
        }

        private static byte FindNearestColor(Palette palette, MediaColor source)
        {
            int bestIndex = 0;
            long bestDistance = long.MaxValue;

            for (int i = 0; i < 256; i++)
            {
                var candidate = palette.GetColor(i);
                long red = candidate.Red - source.R;
                long green = candidate.Green - source.G;
                long blue = candidate.Blue - source.B;
                long distance = red * red + green * green + blue * blue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                    if (distance == 0)
                        break;
                }
            }

            return (byte)bestIndex;
        }

        private static BitmapPalette ToBitmapPalette(Palette palette)
        {
            var colors = new List<MediaColor>(256);
            for (int i = 0; i < 256; i++)
            {
                if (palette == null)
                {
                    colors.Add(MediaColor.FromRgb((byte)i, (byte)i, (byte)i));
                    continue;
                }

                var color = palette.GetColor(i);
                colors.Add(MediaColor.FromRgb((byte)color.Red, (byte)color.Green, (byte)color.Blue));
            }

            return new BitmapPalette(colors);
        }
    }
}
