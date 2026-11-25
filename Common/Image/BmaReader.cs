using System;
using System.Collections.Generic;
using System.IO;

namespace DSunTools.Common.Image
{
    /// <summary>
    /// A sequence of full-screen frames decoded from a DS1 BMA chunk.
    /// BMA is a delta codec: each frame is applied to the persistent
    /// canvas of the previous frame, exactly like the game's cinematic
    /// player does.
    /// </summary>
    public sealed class BmaContainerInfo
    {
        public BmaContainerInfo(int offset, int size, int frameCount)
        {
            Offset = offset;
            Size = size;
            FrameCount = frameCount;
        }

        public int Offset { get; }
        public int Size { get; }
        public int FrameCount { get; }
    }

    public sealed class BmaAnimation
    {
        public BmaAnimation(
            int width,
            int height,
            IReadOnlyList<ImageFrame> frames,
            IReadOnlyList<BmaContainerInfo> containers)
        {
            Width = width;
            Height = height;
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            Containers = containers ?? throw new ArgumentNullException(nameof(containers));
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<ImageFrame> Frames { get; }

        /// <summary>
        /// Container boundaries as stored in the chunk. ACF op-01 segment
        /// registrations mirror this list, so imports that preserve the
        /// layout can keep the cinematic script valid.
        /// </summary>
        public IReadOnlyList<BmaContainerInfo> Containers { get; }
    }

    /// <summary>
    /// Decoder for DS1's BMA cinematic frames. BMA shares the standard
    /// bitmap-container header (u32 size, u16 frame_count, u32 offsets[])
    /// but its frame bodies are row-addressed delta records with the DS1
    /// RLE payload. The format is documented in OpenDS's
    /// docs/reference/cinematics-ds1.md.
    /// </summary>
    public static class BmaReader
    {
        private const byte EndOfFrame = 0xFF;
        private const int LastRecordFlag = 0x8000;
        private const int XMask = 0x7FFF;

        /// <summary>
        /// Decode all containers and frames in a BMA chunk. The canvas
        /// starts black and persists across containers, matching the
        /// engine. Frames are returned as full 320x200 images so a viewer
        /// does not need to compose deltas itself.
        /// </summary>
        public static BmaAnimation Read(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < 6)
                throw new InvalidDataException("BMA chunk is too small for a container header.");

            var frames = new List<ImageFrame>();
            var containers = new List<BmaContainerInfo>();
            byte[] canvas = null;
            int width = 0;
            int height = 0;

            int containerOffset = 0;
            while (containerOffset + 6 <= data.Length)
            {
                int containerSize = ReadInt32(data, containerOffset);
                if (containerSize <= 0 || containerOffset + containerSize > data.Length)
                {
                    throw new InvalidDataException(
                        $"BMA container at 0x{containerOffset:X} has invalid size {containerSize}.");
                }

                int containerEnd = containerOffset + containerSize;
                int frameCount = ReadUInt16(data, containerOffset + 4);
                containers.Add(new BmaContainerInfo(containerOffset, containerSize, frameCount));
                int headerSize = 6 + frameCount * 4;
                if (headerSize > containerSize)
                {
                    throw new InvalidDataException(
                        $"BMA container at 0x{containerOffset:X} has {frameCount} frames but only {containerSize} bytes.");
                }

                for (int i = 0; i < frameCount; i++)
                {
                    int frameRelativeOffset = ReadInt32(data, containerOffset + 6 + i * 4);
                    if (frameRelativeOffset < headerSize || frameRelativeOffset >= containerSize)
                    {
                        throw new InvalidDataException(
                            $"BMA frame {i} in container 0x{containerOffset:X} has invalid offset {frameRelativeOffset}.");
                    }

                    int frameOffset = containerOffset + frameRelativeOffset;
                    if (canvas == null)
                    {
                        width = ReadUInt16(data, frameOffset);
                        height = ReadUInt16(data, frameOffset + 2);
                        if (width <= 0 || height <= 0)
                            throw new InvalidDataException($"BMA frame has invalid dimensions {width}x{height}.");
                        canvas = new byte[width * height];
                    }

                    DecodeFrame(data, frameOffset, containerEnd, canvas, width, height);
                    frames.Add(CreateFullFrame(canvas, width, height));
                }

                containerOffset = containerEnd;
            }

            if (frames.Count == 0)
                throw new InvalidDataException("BMA chunk contains no frames.");

            return new BmaAnimation(width, height, frames, containers);
        }

        private static void DecodeFrame(byte[] data, int frameOffset, int containerEnd, byte[] canvas, int width, int height)
        {
            int position = frameOffset;
            int frameWidth = ReadUInt16(data, position);
            int frameHeight = ReadUInt16(data, position + 2);
            position += 4;

            if (frameWidth != width || frameHeight != height)
            {
                throw new InvalidDataException(
                    $"BMA frame dimensions changed from {width}x{height} to {frameWidth}x{frameHeight}.");
            }

            while (position < containerEnd)
            {
                byte row = data[position++];
                if (row == EndOfFrame)
                    return;

                if (row >= height)
                    throw new InvalidDataException($"BMA row {row} is outside a {height}-row frame.");

                while (true)
                {
                    if (position + 4 > containerEnd)
                        throw new InvalidDataException("BMA record is truncated.");

                    int xword = ReadUInt16(data, position);
                    position += 2;
                    bool last = (xword & LastRecordFlag) != 0;
                    int x = xword & XMask;

                    int span = data[position++];
                    int count = data[position++];
                    if (position + count > containerEnd)
                        throw new InvalidDataException("BMA RLE payload runs past the container.");

                    byte[] pixels = DecodeRowRle(data, position, count, span);
                    position += count;

                    int rowStart = row * width;
                    int copyLength = Math.Min(span, width - x);
                    for (int i = 0; i < copyLength; i++)
                        canvas[rowStart + x + i] = pixels[i];

                    if (last)
                        break;
                }
            }
        }

        private static byte[] DecodeRowRle(byte[] data, int offset, int count, int expectedLength)
        {
            var result = new byte[expectedLength];
            int source = offset;
            int sourceEnd = offset + count;
            int destination = 0;

            while (source < sourceEnd && destination < expectedLength)
            {
                int code = data[source++];
                int runLength = code / 2 + 1;

                if ((code & 1) == 0)
                {
                    if (source + runLength > sourceEnd)
                        throw new InvalidDataException("BMA literal run exceeds its RLE payload.");

                    int copy = Math.Min(runLength, expectedLength - destination);
                    Array.Copy(data, source, result, destination, copy);
                    source += runLength;
                    destination += copy;
                }
                else
                {
                    if (source >= sourceEnd)
                        throw new InvalidDataException("BMA repeat run has no value byte.");

                    byte value = data[source++];
                    int copy = Math.Min(runLength, expectedLength - destination);
                    for (int i = 0; i < copy; i++)
                        result[destination++] = value;

                    if (copy < runLength)
                        break;
                }
            }

            if (destination != expectedLength)
            {
                throw new InvalidDataException(
                    $"BMA RLE decoded {destination} pixels but the span is {expectedLength}.");
            }

            return result;
        }

        private static ImageFrame CreateFullFrame(byte[] canvas, int width, int height)
        {
            var rows = new SortedDictionary<int, List<PixelRun>>();
            for (int y = 0; y < height; y++)
            {
                var row = new byte[width];
                Array.Copy(canvas, y * width, row, 0, width);
                rows[y] = new List<PixelRun> { new PixelRun(0, row) };
            }

            return new ImageFrame(width, height, rows);
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new InvalidDataException("BMA read past the end of the chunk.");

            return data[offset]
                   | (data[offset + 1] << 8)
                   | (data[offset + 2] << 16)
                   | (data[offset + 3] << 24);
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            if (offset < 0 || offset + 2 > data.Length)
                throw new InvalidDataException("BMA read past the end of the chunk.");

            return data[offset] | (data[offset + 1] << 8);
        }
    }
}
