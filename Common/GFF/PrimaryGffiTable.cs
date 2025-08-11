using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;

namespace DSunTools.Common.Gff
{
    public class PrimaryGffiTable : GffiTable
    {
        private const int ENTRY_SIZE = 12;
        private const int OFFSET_OFFSET = 4;
        private const int SIZE_OFFSET = 8;

        private readonly List<int> resourceNumbers;

        public static PrimaryGffiTable Create(byte[] bytes, int startPosition)
        {
            int entryCount = BitConverter.ToInt32(bytes, startPosition);

            var resourceNumbers = ReadResourceNumbers(bytes, startPosition, entryCount);

            return new PrimaryGffiTable(bytes, startPosition, entryCount, resourceNumbers);
        }

        private static List<int> ReadResourceNumbers(
            byte[] buffer, int tableStartPosition, int entryCount)
        {
            var resourceNumbers = new List<int>();
            for (int i = 0; i < entryCount; i++)
            {
                int position = tableStartPosition + 4 + i * ENTRY_SIZE;
                resourceNumbers.Add(BitConverter.ToInt32(buffer, position));
            }
            return resourceNumbers;
        }

        private PrimaryGffiTable(
            byte[] buffer, int startPosition, int entryCount, List<int> resourceNumbers)
            : base(buffer, startPosition, entryCount)
        {
            this.resourceNumbers = resourceNumbers;
        }

        protected override int EntrySize() => ENTRY_SIZE;

        protected override int OffsetOffset() => OFFSET_OFFSET;

        protected override int SizeOffset() => SIZE_OFFSET;

        public override int GetResourceNumber(int index) => resourceNumbers[index];
    }
}