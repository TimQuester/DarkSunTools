using System;
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

        public static byte[] Write(IReadOnlyList<byte[]> frames, int width, int height)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
            if (width <= 0 || width > 0x1FF || height <= 0 || height >= 0xFF)
                throw new ArgumentOutOfRangeException(nameof(width), "Bitmap dimensions are outside the row-based format limits.");

            var bodies = new List<byte[]>(frames.Count);
            foreach (byte[] frame in frames)
            {
                if (frame == null || frame.Length != width * height)
                    throw new ArgumentException($"Every frame must be exactly {width * height} pixels.", nameof(frames));
                bodies.Add(EncodeFrame(frame, width, height));
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

        private static byte[] EncodeFrame(byte[] pixels, int width, int height)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)width);
            writer.Write((ushort)height);

            for (int y = 0; y < height; y++)
            {
                writer.Write((byte)y);
                int rowStart = y * width;
                int x = 0;
                while (x < width)
                {
                    int span = Math.Min(MaxRunPixels, width - x);
                    byte[] compressed = EncodeRle(pixels, rowStart + x, span);
                    bool last = x + span >= width;
                    int startX = x;
                    byte flags = (byte)(last ? LastRunFlag : 0);
                    if (startX >= 256)
                    {
                        flags |= Column256Flag;
                        startX -= 256;
                    }

                    writer.Write((byte)startX);
                    writer.Write(flags);
                    writer.Write((byte)span);
                    writer.Write((byte)compressed.Length);
                    writer.Write(compressed);
                    x += span;
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
