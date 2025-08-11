using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace DSunTools.Common.Image
{
    public class ImageFrame
    {
        public int Width { get; }
        public int Height { get; }
        private readonly Lazy<BitArray> alphaMaskSupplier;
        private readonly Lazy<byte[]> pixelsSupplier;
        private readonly SortedDictionary<int, List<PixelRun>> runsByRow;

        public ImageFrame(int width, int height, SortedDictionary<int, List<PixelRun>> runsByRow)
        {
            Width = width;
            Height = height;
            this.runsByRow = runsByRow ?? throw new ArgumentNullException(nameof(runsByRow));

            alphaMaskSupplier = new Lazy<BitArray>(ComposeAlphaBitmask);
            pixelsSupplier = new Lazy<byte[]>(ComposePixels);
        }

        public BitArray GetAlphaMask() => alphaMaskSupplier.Value;
        public byte[] GetPixels() => pixelsSupplier.Value;

        private BitArray ComposeAlphaBitmask()
        {
            var alphaBitmask = new BitArray(Width * Height);

            foreach (var yAndRuns in runsByRow)
            {
                int rowStart = yAndRuns.Key * Width;
                foreach (var run in yAndRuns.Value)
                {
                    int runStart = rowStart + run.StartX;
                    for (int i = 0; i < run.Length; i++)
                    {
                        alphaBitmask.Set(runStart + i, true);
                    }
                }
            }

            return alphaBitmask;
        }

        private byte[] ComposePixels()
        {
            byte[] pixels = new byte[Height * Width];

            foreach (var yAndRuns in runsByRow)
            {
                int rowStart = yAndRuns.Key * Width;
                foreach (var run in yAndRuns.Value)
                {
                    run.WriteTo(pixels, rowStart + run.StartX);
                }
            }

            return pixels;
        }
    }
}