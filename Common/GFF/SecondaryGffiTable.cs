using System;
using System.Collections.Generic;

namespace DSunTools.Common.Gff
{
    public class SecondaryGffiTable : GffiTable
    {
        private readonly SortedDictionary<int, int> resourceNumbersByIndex;

        public static SecondaryGffiTable Create(
            byte[] bytes, int tableOffset, int resourceNumberingOffset)
        {
            int entryCount = BitConverter.ToInt32(bytes, tableOffset);

            var resourceNumbersByIndex = ReadResourceNumbering(bytes, resourceNumberingOffset);

            return new SecondaryGffiTable(bytes, tableOffset, entryCount, resourceNumbersByIndex);
        }

        private static SortedDictionary<int, int> ReadResourceNumbering(
            byte[] buffer, int resourceNumberingOffset)
        {
            var resourceNumbersByIndex = new SortedDictionary<int, int>();

            int position = resourceNumberingOffset;
            int segmentCount = BitConverter.ToInt32(buffer, position);
            position += 4;

            int segmentStartIndex = 0;
            for (int i = 0; i < segmentCount; i++)
            {
                int segmentStartResourceCount = BitConverter.ToInt32(buffer, position);
                position += 4;

                resourceNumbersByIndex.Add(segmentStartIndex, segmentStartResourceCount);

                int segmentResourceCount = BitConverter.ToInt32(buffer, position);
                position += 4;

                segmentStartIndex += segmentResourceCount;
            }

            return resourceNumbersByIndex;
        }

        private SecondaryGffiTable(
            byte[] buffer,
            int tableOffset,
            int entryCount,
            SortedDictionary<int, int> resourceNumbersByIndex)
            : base(buffer, tableOffset, entryCount)
        {
            this.resourceNumbersByIndex = resourceNumbersByIndex;
        }

        public override int GetResourceNumber(int index)
        {
            // Эмуляция NavigableMap.floorEntry()
            int floorKey = 0;
            foreach (var key in resourceNumbersByIndex.Keys)
            {
                if (key > index) break;
                floorKey = key;
            }

            var segmentStartResourceNumber = resourceNumbersByIndex[floorKey];
            return segmentStartResourceNumber + (index - floorKey);
        }

        protected override int EntrySize() => 8;

        protected override int OffsetOffset() => 0;

        protected override int SizeOffset() => 4;
    }
}