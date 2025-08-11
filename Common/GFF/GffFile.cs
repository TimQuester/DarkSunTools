using DSunTools.Common.Gff;
using DSunTools.Common.GFF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSunTools.Common.Gff
{
    public class GffFile : IGffReader
    {
        public class NoSuchResourceInGffException : Exception
        {
            public string Tag { get; }
            public int ResourceNumber { get; }

            public NoSuchResourceInGffException(string tag, int resourceNumber)
                : base($"no resource {tag}-{resourceNumber} in GFF file")
            {
                Tag = tag;
                ResourceNumber = resourceNumber;
            }
        }

        private class SecondaryTableDescriptor
        {
            public int SecondaryTableIndex { get; }
            public int ResourceNumberingOffset { get; }

            public SecondaryTableDescriptor(int secondaryTableIndex, int resourceNumberingOffset)
            {
                SecondaryTableIndex = secondaryTableIndex;
                ResourceNumberingOffset = resourceNumberingOffset;
            }
        }

        public static GffFile Create(byte[] gffFileBytes)
        {
            var tablesByTag = CreateTables(gffFileBytes);
            return new GffFile(gffFileBytes, tablesByTag);
        }

        //private static Dictionary<string, GffiTable> CreateTables(byte[] bytes)
        //{
        //    var buffer = new ReadOnlySpan<byte>(bytes);
        //    int indexStart = BitConverter.ToInt32(buffer.Slice(12));
        //    buffer = buffer.Slice(indexStart);

        //    // Skip two int values
        //    buffer = buffer.Slice(8);

        //    var tablesByTag = new Dictionary<string, GffiTable>();
        //    var secondaryTableDescriptorForTag = new Dictionary<string, SecondaryTableDescriptor>();

        //    int numberOfTags = BitConverter.ToInt16(buffer);
        //    buffer = buffer.Slice(2);

        //    for (int iTag = 0; iTag < numberOfTags; iTag++)
        //    {
        //        string tag = ReadTag(ref buffer);
        //        if (tablesByTag.ContainsKey(tag))
        //        {
        //            throw new InvalidOperationException("Encountered a second table for tag " + tag);
        //        }

        //        int numberOfChunksIfPrimary = BitConverter.ToInt32(buffer);
        //        buffer = buffer.Slice(4);

        //        if (numberOfChunksIfPrimary > 0)
        //        {
        //            int tableStartPosition = indexStart + (bytes.Length - buffer.Length) - 4;
        //            var table = PrimaryGffiTable.Create(bytes, tableStartPosition);
        //            tablesByTag.Add(tag, table);
        //            buffer = buffer.Slice(table.GetTotalSize() - 4);
        //        }
        //        else
        //        {
        //            buffer = buffer.Slice(4); // Skip one int

        //            int secondaryTableIndex = BitConverter.ToInt32(buffer);
        //            buffer = buffer.Slice(4);

        //            int resourceNumberingOffset = indexStart + (bytes.Length - buffer.Length);
        //            secondaryTableDescriptorForTag.Add(tag,
        //                new SecondaryTableDescriptor(secondaryTableIndex, resourceNumberingOffset));

        //            int numberSegmentCount = BitConverter.ToInt32(buffer);
        //            buffer = buffer.Slice(4);

        //            for (int i = 0; i < numberSegmentCount; i++)
        //            {
        //                buffer = buffer.Slice(8); // Skip two ints
        //            }
        //        }
        //    }

        //    var gffiTable = tablesByTag["GFFI"];
        //    foreach (var tagAndDescriptor in secondaryTableDescriptorForTag)
        //    {
        //        var descriptor = tagAndDescriptor.Value;
        //        int secondaryTableOffset = gffiTable.GetOffset(descriptor.SecondaryTableIndex);
        //        tablesByTag.Add(tagAndDescriptor.Key, SecondaryGffiTable.Create(
        //            bytes, secondaryTableOffset, descriptor.ResourceNumberingOffset));
        //    }

        //    return tablesByTag;
        //}

        private static Dictionary<string, GffiTable> CreateTables(byte[] bytes)
        {
            // Чтение 32-битного значения с little-endian порядком
            int indexStart = bytes[12] | (bytes[13] << 8) | (bytes[14] << 16) | (bytes[15] << 24);

            ReadOnlySpan<byte> buffer = bytes.AsSpan(indexStart);

            // Пропускаем два int значения (8 байт)
            buffer = buffer.Slice(8);

            // Чтение 16-битного значения с little-endian порядком
            int numberOfTags = buffer[0] | (buffer[1] << 8);
            buffer = buffer.Slice(2);

            var tablesByTag = new Dictionary<string, GffiTable>();
            var secondaryTableDescriptorForTag = new Dictionary<string, SecondaryTableDescriptor>();

            for (int iTag = 0; iTag < numberOfTags; iTag++)
            {
                string tag = ReadTag(ref buffer);
                if (tablesByTag.ContainsKey(tag))
                    throw new InvalidOperationException("Encountered a second table for tag " + tag);

                // Чтение 32-битного значения с little-endian порядком
                int numberOfChunksIfPrimary = buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
                buffer = buffer.Slice(4);

                if (numberOfChunksIfPrimary > 0)
                {
                    int tableStartPosition = indexStart + (bytes.Length - buffer.Length) - 4;
                    var table = PrimaryGffiTable.Create(bytes, tableStartPosition);
                    tablesByTag.Add(tag, table);
                    buffer = buffer.Slice(table.GetTotalSize() - 4);
                }
                else
                {
                    buffer = buffer.Slice(4); // Пропускаем один int

                    // Чтение 32-битного значения с little-endian порядком
                    int secondaryTableIndex = buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
                    buffer = buffer.Slice(4);

                    int resourceNumberingOffset = indexStart + (bytes.Length - buffer.Length);
                    secondaryTableDescriptorForTag.Add(tag,
                        new SecondaryTableDescriptor(secondaryTableIndex, resourceNumberingOffset));

                    // Чтение 32-битного значения с little-endian порядком
                    int numberSegmentCount = buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
                    buffer = buffer.Slice(4);

                    for (int i = 0; i < numberSegmentCount; i++)
                    {
                        buffer = buffer.Slice(8); // Пропускаем два int
                    }
                }
            }

            var gffiTable = tablesByTag["GFFI"];
            foreach (var tagAndDescriptor in secondaryTableDescriptorForTag)
            {
                var descriptor = tagAndDescriptor.Value;
                int secondaryTableOffset = gffiTable.GetOffset(descriptor.SecondaryTableIndex);
                tablesByTag.Add(tagAndDescriptor.Key, SecondaryGffiTable.Create(
                    bytes, secondaryTableOffset, descriptor.ResourceNumberingOffset));
            }

            return tablesByTag;
        }

        private static string ReadTag(ref ReadOnlySpan<byte> buffer)
        {
            string tag = Encoding.ASCII.GetString(buffer.Slice(0, 4));
            buffer = buffer.Slice(4);
            return tag;
        }

        private readonly byte[] bytes;
        private readonly Dictionary<string, GffiTable> tablesByTag;

        private GffFile(byte[] gffFileBytes, Dictionary<string, GffiTable> tablesByTag)
        {
            this.bytes = gffFileBytes;
            this.tablesByTag = tablesByTag;
        }

        public List<ResourceDescriptor> DescribeResources()
        {
            var descriptors = new List<ResourceDescriptor>();

            foreach (var tagAndTable in tablesByTag)
            {
                string tag = tagAndTable.Key;
                var table = tagAndTable.Value;
                for (int i = 0; i < table.GetEntryCount(); i++)
                {
                    descriptors.Add(new ResourceDescriptor(
                        tag, table.GetResourceNumber(i), table.GetOffset(i), table.GetSize(i)));
                }
            }

            descriptors.Sort((r1, r2) => r1.Offset.CompareTo(r2.Offset));
            return descriptors;
        }

        public bool HasResource(string tag, int resourceNumber)
        {
            return tablesByTag.TryGetValue(tag, out var table) &&
                   table.GetIndexForResourceNumber(resourceNumber).HasValue;
        }

        public byte[] GetResourceData(string tag, int resourceNumber)
        {
            if (!tablesByTag.TryGetValue(tag, out var table))
            {
                throw new NoSuchResourceInGffException(tag, resourceNumber);
            }

            var index = table.GetIndexForResourceNumber(resourceNumber);
            if (!index.HasValue)
            {
                throw new NoSuchResourceInGffException(tag, resourceNumber);
            }

            byte[] data = new byte[table.GetSize(index.Value)];
            Array.Copy(bytes, table.GetOffset(index.Value), data, 0, data.Length);
            return data;
        }

        public byte[] ReplaceResource(string tag, int resourceNumber, byte[] replacement)
        {
            if (!tablesByTag.TryGetValue(tag, out var table))
            {
                throw new NoSuchResourceInGffException(tag, resourceNumber);
            }

            var index = table.GetIndexForResourceNumber(resourceNumber);
            if (!index.HasValue)
            {
                throw new NoSuchResourceInGffException(tag, resourceNumber);
            }

            int newSize = replacement.Length;
            int newOffset;
            if (newSize <= table.GetSize(index.Value))
            {
                // The replacement fits in the old space.
                newOffset = table.GetOffset(index.Value);
            }
            else
            {
                // The (larger) replacement must be appended to the end of the file.
                newOffset = bytes.Length;
            }

            table.SetOffset(index.Value, newOffset);
            table.SetSize(index.Value, newSize);

            byte[] bytesWithReplacement;
            if (newOffset < bytes.Length)
            {
                bytesWithReplacement = (byte[])bytes.Clone();
            }
            else
            {
                bytesWithReplacement = new byte[bytes.Length + newSize];
                Array.Copy(bytes, 0, bytesWithReplacement, 0, bytes.Length);
            }

            Array.Copy(replacement, 0, bytesWithReplacement, newOffset, newSize);
            return bytesWithReplacement;
        }
    }
}