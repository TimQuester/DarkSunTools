using System;
using System.Collections.Generic;
using System.IO;

namespace DSunTools.Common.Image
{
    public sealed class AcfSegmentInfo
    {
        public AcfSegmentInfo(int index, int frameCount, int byteSize)
        {
            Index = index;
            FrameCount = frameCount;
            ByteSize = byteSize;
        }

        public int Index { get; }
        public int FrameCount { get; }
        public int ByteSize { get; }
    }

    /// <summary>
    /// Reads and updates the op-01 stream segment registrations in an ACF
    /// script. Each registration mirrors one BMA bitmap container:
    /// (segment index, frame count, container byte size). Imported frames
    /// change the container sizes, so those u16 fields must be patched or
    /// the engine seeks to the wrong offsets after the first frame.
    /// </summary>
    public static class AcfSegmentPatcher
    {
        public static IReadOnlyList<AcfSegmentInfo> ReadSegments(byte[] acfData)
        {
            if (acfData == null) throw new ArgumentNullException(nameof(acfData));

            var result = new List<AcfSegmentInfo>();
            int wordCount = acfData.Length / 2;
            int position = 0;

            while (position < wordCount)
            {
                int instruction = ReadWord(acfData, position);
                int opcode = instruction >> 8;
                int totalWords = (instruction & 0xFF) / 2;
                if (totalWords < 1 || position + totalWords > wordCount)
                    break;

                if (opcode == 0x01 && totalWords >= 4)
                {
                    result.Add(new AcfSegmentInfo(
                        ReadWord(acfData, position + 1),
                        ReadWord(acfData, position + 2),
                        ReadWord(acfData, position + 3)));
                }

                position += totalWords;
            }

            return result;
        }

        /// <summary>
        /// Replace every op-01 registration with the supplied segment list.
        /// This is used when a BMA container had to be split to stay within
        /// the engine's 64 KB segment limit. The new registrations are
        /// inserted at the position of the first old op-01 instruction, so
        /// the pre-scan still sees them and the remaining script is kept.
        /// </summary>
        public static byte[] RebuildSegments(
            byte[] acfData,
            IReadOnlyList<AcfSegmentInfo> segments)
        {
            if (acfData == null) throw new ArgumentNullException(nameof(acfData));
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            if (segments.Count > 64)
            {
                throw new InvalidDataException(
                    $"BMA needs {segments.Count} stream segments, but the ACF segment table holds at most 64.");
            }

            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            int wordCount = acfData.Length / 2;
            int position = 0;
            bool inserted = false;

            while (position < wordCount)
            {
                int instruction = ReadWord(acfData, position);
                int opcode = instruction >> 8;
                int totalWords = (instruction & 0xFF) / 2;
                if (totalWords < 1 || position + totalWords > wordCount)
                    break;

                if (opcode == 0x01)
                {
                    if (!inserted)
                    {
                        WriteSegments(writer, segments);
                        inserted = true;
                    }

                    position += totalWords;
                    continue;
                }

                for (int i = 0; i < totalWords; i++)
                    writer.Write((ushort)ReadWord(acfData, position + i));
                position += totalWords;
            }

            if (!inserted)
                throw new InvalidDataException("ACF has no op-01 stream segment registrations to replace.");

            // Preserve an odd trailing byte if the chunk had one.
            if ((acfData.Length & 1) != 0)
                writer.Write(acfData[acfData.Length - 1]);

            return output.ToArray();
        }

        private static void WriteSegments(BinaryWriter writer, IReadOnlyList<AcfSegmentInfo> segments)
        {
            foreach (AcfSegmentInfo segment in segments)
            {
                if (segment.Index < 0 || segment.Index > 0x100)
                    throw new InvalidDataException($"ACF segment index {segment.Index} is out of range.");
                if (segment.FrameCount < 0 || segment.FrameCount > ushort.MaxValue)
                    throw new InvalidDataException($"ACF segment frame count {segment.FrameCount} is out of range.");
                if (segment.ByteSize < 0 || segment.ByteSize > ushort.MaxValue)
                    throw new InvalidDataException($"ACF segment size {segment.ByteSize} is out of range.");

                writer.Write((ushort)((0x01 << 8) | (4 * 2)));
                writer.Write((ushort)segment.Index);
                writer.Write((ushort)segment.FrameCount);
                writer.Write((ushort)segment.ByteSize);
            }
        }

        /// <summary>
        /// Set the byte-size operand of every op-01 registration, in order.
        /// Returns a patched copy; the original bytes are not modified.
        /// </summary>
        public static byte[] UpdateSegmentSizes(byte[] acfData, IReadOnlyList<int> containerSizes)
        {
            if (acfData == null) throw new ArgumentNullException(nameof(acfData));
            if (containerSizes == null) throw new ArgumentNullException(nameof(containerSizes));

            var result = (byte[])acfData.Clone();
            int wordCount = result.Length / 2;
            int position = 0;
            int segmentIndex = 0;

            while (position < wordCount)
            {
                int instruction = ReadWord(result, position);
                int opcode = instruction >> 8;
                int totalWords = (instruction & 0xFF) / 2;
                if (totalWords < 1 || position + totalWords > wordCount)
                    break;

                if (opcode == 0x01 && totalWords >= 4)
                {
                    if (segmentIndex >= containerSizes.Count)
                    {
                        throw new InvalidDataException(
                            $"ACF has more stream segments than the {containerSizes.Count} BMA containers.");
                    }

                    int size = containerSizes[segmentIndex];
                    if (size < 0 || size > ushort.MaxValue)
                    {
                        throw new InvalidDataException(
                            $"BMA container {segmentIndex} size {size} does not fit the ACF u16 field.");
                    }

                    WriteWord(result, position + 3, size);
                    segmentIndex++;
                }

                position += totalWords;
            }

            if (segmentIndex != containerSizes.Count)
            {
                throw new InvalidDataException(
                    $"ACF registers {segmentIndex} stream segments, but the BMA has {containerSizes.Count} containers.");
            }

            return result;
        }

        private static int ReadWord(byte[] data, int wordIndex)
        {
            int offset = wordIndex * 2;
            if (offset < 0 || offset + 2 > data.Length)
                throw new InvalidDataException("ACF read past the end of the chunk.");

            return data[offset] | (data[offset + 1] << 8);
        }

        private static void WriteWord(byte[] data, int wordIndex, int value)
        {
            int offset = wordIndex * 2;
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
