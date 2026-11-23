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
                    $"PNG must be an 8-bit indexed image (256 colors); got {frame.Format}.");
            }

            if (frame.Palette == null || frame.Palette.Colors.Count != 256)
            {
                throw new InvalidDataException(
                    $"PNG must contain a 256-entry palette; got {frame.Palette?.Colors.Count ?? 0} entries.");
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
