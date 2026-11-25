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
        private const int MaxSpanPerRecord = 255;
        private const byte EndOfFrame = 0xFF;

        public static byte[] Write(IReadOnlyList<byte[]> frames, int width, int height)
        {
            return Write(frames, width, height, null);
        }

        /// <summary>
        /// Encode frames into BMA containers. When
        /// <paramref name="containerFrameCounts"/> is supplied, the original
        /// container/frame grouping is preserved. ACF op-01 segment records
        /// mirror this grouping, so preserving it is required for an edited
        /// cinematic to play correctly in the game.
        /// </summary>
        public static byte[] Write(
            IReadOnlyList<byte[]> frames,
            int width,
            int height,
            IReadOnlyList<int>? containerFrameCounts)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "BMA dimensions must be positive.");
            if (frames.Count == 0)
                throw new ArgumentException("At least one frame is required.", nameof(frames));

            var groups = containerFrameCounts ?? SingleFrameContainers(frames.Count);
            int groupedFrames = 0;
            foreach (int count in groups)
            {
                if (count <= 0)
                    throw new ArgumentException("Container frame counts must be positive.", nameof(containerFrameCounts));
                groupedFrames += count;
            }

            if (groupedFrames != frames.Count)
            {
                throw new ArgumentException(
                    $"Container layout describes {groupedFrames} frames, but {frames.Count} were supplied.",
                    nameof(containerFrameCounts));
            }

            var previous = new byte[width * height];
            var bodies = new List<byte[]>(frames.Count);
            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                byte[] frame = frames[frameIndex];
                if (frame == null || frame.Length != previous.Length)
                {
                    throw new ArgumentException(
                        $"Every BMA frame must be exactly {width * height} pixels.", nameof(frames));
                }

                // The first frame is a full keyframe: the engine canvas may
                // still contain the previous cinematic's picture.
                bodies.Add(EncodeFrame(
                    frame,
                    previous,
                    width,
                    height,
                    forceAll: frameIndex == 0));
                Buffer.BlockCopy(frame, 0, previous, 0, previous.Length);
            }

            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);

            int currentFrame = 0;
            foreach (int frameCount in groups)
            {
                if (frameCount > ushort.MaxValue)
                    throw new InvalidDataException("A BMA container cannot hold more than 65535 frames.");

                int groupEnd = currentFrame + frameCount;
                while (currentFrame < groupEnd)
                {
                    int containerStart = currentFrame;
                    int containerSize = 6; // u32 size + u16 frame count

                    while (currentFrame < groupEnd)
                    {
                        int nextSize = containerSize + 4 + bodies[currentFrame].Length;
                        if (nextSize > ushort.MaxValue && currentFrame > containerStart)
                            break;

                        containerSize = nextSize;
                        currentFrame++;
                    }

                    if (containerSize > ushort.MaxValue)
                    {
                        throw new InvalidDataException(
                            $"A single BMA frame body plus its container header is " +
                            $"{containerSize} bytes; the ACF segment size field supports " +
                            "at most 65535 bytes.");
                    }

                    WriteContainer(writer, bodies, containerStart, currentFrame - containerStart);
                }
            }

            return output.ToArray();
        }

        private static void WriteContainer(
            BinaryWriter writer,
            IReadOnlyList<byte[]> bodies,
            int startFrame,
            int frameCount)
        {
            int headerSize = 6 + frameCount * 4;
            int containerSize = headerSize;
            for (int i = 0; i < frameCount; i++)
                containerSize += bodies[startFrame + i].Length;

            if (containerSize > ushort.MaxValue)
            {
                throw new InvalidDataException(
                    $"Encoded BMA container is {containerSize} bytes; " +
                    "the ACF segment size field supports at most 65535 bytes.");
            }

            writer.Write((uint)containerSize);
            writer.Write((ushort)frameCount);

            int frameOffset = headerSize;
            for (int i = 0; i < frameCount; i++)
            {
                writer.Write((uint)frameOffset);
                frameOffset += bodies[startFrame + i].Length;
            }

            for (int i = 0; i < frameCount; i++)
                writer.Write(bodies[startFrame + i]);
        }

        private static IReadOnlyList<int> SingleFrameContainers(int frameCount)
        {
            var result = new int[frameCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = 1;
            return result;
        }

        private static byte[] EncodeFrame(byte[] current, byte[] previous, int width, int height, bool forceAll)
        {
            using var frameStream = new MemoryStream();
            using var writer = new BinaryWriter(frameStream);

            writer.Write((ushort)width);
            writer.Write((ushort)height);

            var rowChunks = new List<(int X, int Span, byte[] Rle)>();
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
                        byte[] rle = EncodeRowRle(current, rowStart + offset, chunk);

                        // A literal-heavy span can exceed the one-byte RLE
                        // length. Shrink the record until it fits.
                        while (rle.Length > byte.MaxValue && chunk > 1)
                        {
                            chunk--;
                            rle = EncodeRowRle(current, rowStart + offset, chunk);
                        }

                        if (rle.Length > byte.MaxValue)
                            throw new InvalidDataException("BMA RLE record exceeds 255 bytes.");

                        rowChunks.Add((offset, chunk, rle));
                        offset += chunk;
                        remaining -= chunk;
                    }
                }

                if (rowChunks.Count == 0)
                    continue;

                writer.Write((byte)y);
                for (int i = 0; i < rowChunks.Count; i++)
                {
                    var (chunkX, chunkSpan, rle) = rowChunks[i];
                    bool last = i == rowChunks.Count - 1;
                    writer.Write((ushort)(chunkX | (last ? LastRecordFlag : 0)));
                    writer.Write((byte)chunkSpan);
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
