using System;
using System.Collections.Generic;
using System.IO;

namespace DSunTools.Common.Image
{
    /// <summary>
    /// Encodes indexed frames back into a DS1 BMA chunk. Frames are written
    /// as deltas against the previous canvas, using the same row/record/RLE
    /// primitives as <see cref="BmaReader"/>; unchanged pixels are omitted.
    /// </summary>
    public static class BmaWriter
    {
        private const int LastRecordFlag = 0x8000;
        private const int MaxSpanPerRecord = 128;
        private const byte EndOfFrame = 0xFF;

        public static byte[] Write(IReadOnlyList<byte[]> frames, int width, int height)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "BMA dimensions must be positive.");
            if (frames.Count == 0)
                throw new ArgumentException("At least one frame is required.", nameof(frames));

            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);

            var previous = new byte[width * height];
            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                byte[] frame = frames[frameIndex];
                if (frame == null || frame.Length != previous.Length)
                    throw new ArgumentException(
                        $"Every BMA frame must be exactly {width * height} pixels.", nameof(frames));

                // The first frame is a full keyframe: the engine canvas may
                // still contain the previous cinematic's picture.
                byte[] body = EncodeFrame(frame, previous, width, height, forceAll: frameIndex == 0);
                int containerSize = 10 + body.Length;

                writer.Write((uint)containerSize);
                writer.Write((ushort)1);
                writer.Write((uint)10);
                writer.Write(body);

                Buffer.BlockCopy(frame, 0, previous, 0, previous.Length);
            }

            return output.ToArray();
        }

        private static byte[] EncodeFrame(byte[] current, byte[] previous, int width, int height, bool forceAll)
        {
            using var frameStream = new MemoryStream();
            using var writer = new BinaryWriter(frameStream);

            writer.Write((ushort)width);
            writer.Write((ushort)height);

            var rowChunks = new List<(int X, int Span)>();
            for (int y = 0; y < height; y++)
            {
                rowChunks.Clear();
                int rowStart = y * width;
                int x = 0;

                while (x < width)
                {
                    if (!forceAll && current[rowStart + x] == previous[rowStart + x])
                    {
                        x++;
                        continue;
                    }

                    int spanStart = x;
                    while (x < width &&
                           (forceAll || current[rowStart + x] != previous[rowStart + x]))
                    {
                        x++;
                    }

                    int remaining = x - spanStart;
                    int offset = spanStart;
                    while (remaining > 0)
                    {
                        int chunk = Math.Min(MaxSpanPerRecord, remaining);
                        rowChunks.Add((offset, chunk));
                        offset += chunk;
                        remaining -= chunk;
                    }
                }

                if (rowChunks.Count == 0)
                    continue;

                writer.Write((byte)y);
                for (int i = 0; i < rowChunks.Count; i++)
                {
                    var (chunkX, chunkSpan) = rowChunks[i];
                    bool last = i == rowChunks.Count - 1;
                    writer.Write((ushort)(chunkX | (last ? LastRecordFlag : 0)));
                    writer.Write((byte)chunkSpan);

                    byte[] rle = EncodeRowRle(current, rowStart + chunkX, chunkSpan);
                    if (rle.Length > byte.MaxValue)
                        throw new InvalidDataException("BMA RLE record exceeds 255 bytes.");

                    writer.Write((byte)rle.Length);
                    writer.Write(rle);
                }
            }

            writer.Write(EndOfFrame);
            return frameStream.ToArray();
        }

        /// <summary>
        /// DS1 RLE: even code = literal run of code/2 + 1 bytes, odd code =
        /// repeat the following byte code/2 + 1 times.
        /// </summary>
        private static byte[] EncodeRowRle(byte[] pixels, int offset, int length)
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
                    output.Add((byte)(2 * (runLength - 1) + 1));
                    output.Add(pixels[offset + position]);
                    position += runLength;
                    continue;
                }

                int literalStart = position;
                int literalLength = 0;
                while (position < length && literalLength < 128)
                {
                    int ahead = 1;
                    while (position + ahead < length &&
                           pixels[offset + position + ahead] == pixels[offset + position] &&
                           ahead < 128)
                    {
                        ahead++;
                    }

                    if (ahead >= 3)
                        break;

                    position++;
                    literalLength++;
                }

                if (literalLength == 0)
                {
                    // A two-byte run still needs to be emitted as literals.
                    literalLength = Math.Min(2, length - position);
                    position += literalLength;
                }

                output.Add((byte)(2 * (literalLength - 1)));
                for (int i = 0; i < literalLength; i++)
                    output.Add(pixels[offset + literalStart + i]);
            }

            return output.ToArray();
        }
    }
}
