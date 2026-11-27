using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace DSunTools.Common.Image
{
    /// <summary>
    /// Encodes indexed frames into the standard Dark Sun bitmap container
    /// (u32 size, u16 frame_count, u32 offsets[]) using the row-based DS1
    /// RLE frame codec. This is the format used by BMP/ICON/PORT/TILE/WALL
    /// and by the non-BMA stills.
    /// </summary>
    public static class BitmapContainerWriter
    {
        private const byte EndOfFrame = 0xFF;
        private const int MaxRunPixels = 128;
        private const byte LastRunFlag = 0x80;
        private const byte Column256Flag = 0x01;

        public static byte[] Write(
            IReadOnlyList<byte[]> frames,
            int width,
            int height,
            IReadOnlyList<BitArray?>? alphaMasks = null)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
            if (alphaMasks != null && alphaMasks.Count != frames.Count)
                throw new ArgumentException("Every frame needs an alpha mask.", nameof(alphaMasks));
            if (width <= 0 || width > 0x1FF || height <= 0 || height >= 0xFF)
                throw new ArgumentOutOfRangeException(nameof(width), "Bitmap dimensions are outside the row-based format limits.");

            var bodies = new List<byte[]>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                byte[] frame = frames[i];
                if (frame == null || frame.Length != width * height)
                    throw new ArgumentException($"Every frame must be exactly {width * height} pixels.", nameof(frames));

                BitArray? mask = alphaMasks?[i];
                if (mask != null && mask.Length < frame.Length)
                    throw new ArgumentException("Alpha mask is smaller than the frame.", nameof(alphaMasks));

                bodies.Add(EncodeFrame(frame, width, height, mask));
            }

            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);

            int headerSize = 6 + frames.Count * 4;
            int totalSize = headerSize;
            foreach (byte[] body in bodies)
                totalSize = checked(totalSize + body.Length);

            writer.Write((uint)totalSize);
            writer.Write((ushort)frames.Count);

            int offset = headerSize;
            foreach (byte[] body in bodies)
            {
                writer.Write((uint)offset);
                offset = checked(offset + body.Length);
            }

            foreach (byte[] body in bodies)
                writer.Write(body);

            return output.ToArray();
        }

        private static byte[] EncodeFrame(byte[] pixels, int width, int height, BitArray? alphaMask)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)width);
            writer.Write((ushort)height);

            var spans = new List<(int Start, int Length)>();
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                spans.Clear();

                if (alphaMask == null)
                {
                    spans.Add((0, width));
                }
                else
                {
                    int x = 0;
                    while (x < width)
                    {
                        while (x < width && !alphaMask[rowStart + x])
                            x++;
                        int start = x;
                        while (x < width && alphaMask[rowStart + x])
                            x++;
                        if (x > start)
                            spans.Add((start, x - start));
                    }
                }

                if (spans.Count == 0)
                    continue;

                writer.Write((byte)y);
                for (int spanIndex = 0; spanIndex < spans.Count; spanIndex++)
                {
                    int spanStart = spans[spanIndex].Start;
                    int remaining = spans[spanIndex].Length;
                    while (remaining > 0)
                    {
                        int chunk = Math.Min(MaxRunPixels, remaining);
                        bool lastChunk = spanIndex == spans.Count - 1 && remaining == chunk;
                        byte[] compressed = EncodeRle(pixels, rowStart + spanStart, chunk);

                        int startX = spanStart;
                        byte flags = (byte)(lastChunk ? LastRunFlag : 0);
                        if (startX >= 256)
                        {
                            flags |= Column256Flag;
                            startX -= 256;
                        }

                        writer.Write((byte)startX);
                        writer.Write(flags);
                        writer.Write((byte)chunk);
                        writer.Write((byte)compressed.Length);
                        writer.Write(compressed);

                        spanStart += chunk;
                        remaining -= chunk;
                    }
                }
            }

            writer.Write(EndOfFrame);
            return stream.ToArray();
        }

        private static byte[] EncodeRle(byte[] pixels, int offset, int length)
        {
            var output = new List<byte>(length + 1);
            int position = 0;

            while (position < length)
            {
                int runLength = 1;
                while (position + runLength < length &&
                       pixels[offset + position + runLength] == pixels[offset + position] &&
                       runLength < 128)
                {
                    runLength++;
                }

                if (runLength >= 3)
                {
                    output.Add((byte)((runLength - 1) * 2 + 1));
                    output.Add(pixels[offset + position]);
                    position += runLength;
                    continue;
                }

                int literalStart = position;
                int literalLength = 0;
                while (position < length && literalLength < 128)
                {
                    if (position + 2 < length &&
                        pixels[offset + position] == pixels[offset + position + 1] &&
                        pixels[offset + position] == pixels[offset + position + 2])
                    {
                        break;
                    }
                    position++;
                    literalLength++;
                }

                output.Add((byte)((literalLength - 1) * 2));
                for (int i = 0; i < literalLength; i++)
                    output.Add(pixels[offset + literalStart + i]);
            }

            return output.ToArray();
        }
    }
}
