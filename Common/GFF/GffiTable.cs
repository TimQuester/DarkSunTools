using System;
using System.Collections.Concurrent;
using System.IO;

namespace DSunTools.Common.Gff
{
    public abstract class GffiTable
    {
        protected readonly byte[] Buffer;
        private readonly int StartPosition;
        private readonly int EntryCount;
        private readonly ConcurrentDictionary<int, OptionalInt> IndexForResourceNumber;

        protected GffiTable(byte[] buffer, int startPosition, int entryCount)
        {
            this.Buffer = buffer;
            this.StartPosition = startPosition;
            this.EntryCount = entryCount;
            this.IndexForResourceNumber = new ConcurrentDictionary<int, OptionalInt>();
        }

        public int GetStartPosition() => StartPosition;

        public int GetTotalSize() => 4 + EntryCount * EntrySize();

        public int GetEntryCount() => EntryCount;

        public abstract int GetResourceNumber(int index);

        public OptionalInt GetIndexForResourceNumber(int resourceNumber)
        {
            return IndexForResourceNumber.GetOrAdd(resourceNumber, number =>
            {
                for (int i = 0; i < EntryCount; i++)
                {
                    if (GetResourceNumber(i) == number) // Используем параметр лямбды 'number'
                    {
                        return new OptionalInt(i);
                    }
                }
                return OptionalInt.Empty;
            });
        }

        public int GetOffset(int index)
        {
            if (index >= EntryCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            int position = EntryPosition(index) + OffsetOffset();
            return BitConverter.ToInt32(Buffer, position);
        }

        public void SetOffset(int index, int offset)
        {
            if (index >= EntryCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            int position = EntryPosition(index) + OffsetOffset();
            byte[] bytes = BitConverter.GetBytes(offset);
            Array.Copy(bytes, 0, Buffer, position, 4);
        }

        public int GetSize(int index)
        {
            if (index >= EntryCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            int position = EntryPosition(index) + SizeOffset();
            return BitConverter.ToInt32(Buffer, position);
        }

        public void SetSize(int index, int size)
        {
            if (index >= EntryCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            int position = EntryPosition(index) + SizeOffset();
            byte[] bytes = BitConverter.GetBytes(size);
            Array.Copy(bytes, 0, Buffer, position, 4);
        }

        protected abstract int EntrySize();
        protected abstract int OffsetOffset();
        protected abstract int SizeOffset();

        private int EntryPosition(int index) => StartPosition + 4 + index * EntrySize();
    }

    // Аналог Optional<Integer> в Java
    public struct OptionalInt
    {
        public static readonly OptionalInt Empty = new OptionalInt { HasValue = false };

        public bool HasValue { get; private set; }
        public int Value { get; private set; }

        public OptionalInt(int value)
        {
            HasValue = true;
            Value = value;
        }

        public static implicit operator OptionalInt(int value) => new OptionalInt(value);
    }
}