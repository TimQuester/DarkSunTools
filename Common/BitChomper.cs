using System;

namespace DSunTools.Common
{
    public class BitChomper
    {
        private readonly byte[] array;
        private readonly Endianness byteOrder;
        private int byteOffset;
        private int bitOffset;

        public BitChomper(
            byte[] array,
            int startingByteOffset,
            int startingBitOffset,
            Endianness byteOrder)
        {
            this.array = array ?? throw new ArgumentNullException(nameof(array));
            this.byteOrder = byteOrder;

            if (startingByteOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(startingByteOffset));
            this.byteOffset = startingByteOffset;

            if (startingBitOffset < 0 || startingBitOffset >= 8)
                throw new ArgumentOutOfRangeException(nameof(startingBitOffset));
            this.bitOffset = startingBitOffset;
        }

        public bool HasRemaining(int numberOfBits)
        {
            return (array.Length * 8) - (byteOffset * 8 + bitOffset) >= numberOfBits;
        }

        public int ByteOffset => byteOffset;

        public int BitOffset => bitOffset;

        public int Chomp(int numberOfBits)
        {
            if (numberOfBits <= 0 || numberOfBits > 16)
                throw new ArgumentOutOfRangeException(nameof(numberOfBits));

            int value = 0;
            int bitsFilled = 0;

            while (bitsFilled < numberOfBits)
            {
                int bitsNeeded = numberOfBits - bitsFilled;
                int currentByte = array[byteOffset];

                int bitsFromCurrentByte = Math.Min(bitsNeeded, 8 - bitOffset);

                int maskShift;
                int valueShift;

                if (byteOrder == Endianness.LittleEndian)
                {
                    maskShift = bitOffset;
                    valueShift = bitsFilled;
                }
                else
                {
                    maskShift = 8 - bitOffset - bitsFromCurrentByte;
                    valueShift = bitsNeeded - bitsFromCurrentByte;
                }

                int mask = ((1 << bitsFromCurrentByte) - 1) << maskShift;
                int valueFromByte = (currentByte & mask) >> maskShift;
                value |= valueFromByte << valueShift;

                bitsFilled += bitsFromCurrentByte;
                bitOffset += bitsFromCurrentByte;

                if (bitOffset == 8)
                {
                    byteOffset++;
                    bitOffset = 0;
                }
            }

            return value;
        }
    }

    public enum Endianness
    {
        LittleEndian,
        BigEndian
    }
}