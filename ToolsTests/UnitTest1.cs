using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using DSunTools.Common;

namespace Dsun.Common.Tests
{
    public class BitChomperTest
    {
        [Fact]
        public void TestGetOffsets()
        {
            byte[] bytes = new byte[] { 0b00110011, 0b01111110 };
            var chomper = new BitChomper(bytes, 0, 0, Endianness.BigEndian);

            Assert.Equal(0b001, chomper.Chomp(3));
            Assert.Equal(0, chomper.ByteOffset);
            Assert.Equal(3, chomper.BitOffset);

            Assert.Equal(0b100110, chomper.Chomp(6));
            Assert.Equal(1, chomper.ByteOffset);
            Assert.Equal(1, chomper.BitOffset);

            Assert.Equal(0b1111110, chomper.Chomp(7));
            Assert.Equal(2, chomper.ByteOffset);
            Assert.Equal(0, chomper.BitOffset);
        }

        [Fact]
        public void TestVaryingChompSize()
        {
            byte[] bytes = new byte[] {
                0b0_11_0101_1, 0b0111101_1, 0b110_1101_1, 0b011_01110
            };

            var chomper = new BitChomper(bytes, 0, 0, Endianness.BigEndian);

            Assert.Equal(0b0, chomper.Chomp(1));
            Assert.Equal(0b11, chomper.Chomp(2));
            Assert.Equal(0b0101, chomper.Chomp(4));
            Assert.Equal(0b10111101, chomper.Chomp(8));
            Assert.Equal(0b1110110110110111, chomper.Chomp(16));
        }

        [Fact]
        public void TestInvalidBitsPerChomp()
        {
            byte[] bytes = new byte[10];

            // Должен бросить исключение при попытке прочитать 0 бит
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BitChomper(bytes, 0, 0, Endianness.LittleEndian).Chomp(0));

            // Чтение 16 бит должно быть допустимо (максимальный разрешенный размер)
            // В Java это assertDoesNotThrow, в C# используем Record.Exception
            var exception16 = Record.Exception(() =>
                new BitChomper(bytes, 0, 0, Endianness.LittleEndian).Chomp(16));
            Assert.Null(exception16); // Не должно быть исключения

            // Должен бросить исключение при попытке прочитать 17 бит (превышение лимита)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BitChomper(bytes, 0, 0, Endianness.LittleEndian).Chomp(17));
        }

        [Fact]
        public void TestEmptyArray()
        {
            Assert.False(new BitChomper(new byte[] { }, 0, 0, Endianness.LittleEndian).HasRemaining(1));
        }

        [Fact]
        public void TestStartingOffsets()
        {
            byte[] bytes = new byte[] { 0x20, 0x04, 0x60 };

            Func<int, int, BitChomper> chomperWithOffsets = (byteOffset, bitOffset) =>
                new BitChomper(bytes, byteOffset, bitOffset, Endianness.LittleEndian);

            Assert.Equal(0x2, chomperWithOffsets(0, 4).Chomp(4));
            Assert.Equal(0x4, chomperWithOffsets(1, 0).Chomp(4));
            Assert.Equal(0x6, chomperWithOffsets(2, 4).Chomp(4));
        }

        [Fact]
        public void TestInvalidStartingBitOffset()
        {
            // Должен бросить исключение при отрицательном смещении
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BitChomper(new byte[2], 0, -1, Endianness.BigEndian));

            // Должен работать без исключения при смещении 0
            var exception0 = Record.Exception(() =>
                new BitChomper(new byte[2], 0, 0, Endianness.BigEndian));
            Assert.Null(exception0);

            // Должен работать без исключения при смещении 7
            var exception7 = Record.Exception(() =>
                new BitChomper(new byte[2], 0, 7, Endianness.BigEndian));
            Assert.Null(exception7);

            // Должен бросить исключение при смещении 8
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BitChomper(new byte[2], 0, 8, Endianness.BigEndian));
        }

        [Fact]
        public void TestHasRemaining()
        {
            var chomper = new BitChomper(new byte[] { 0, 0 }, 0, 0, Endianness.LittleEndian);
            int chompSize = 6;

            Assert.True(chomper.HasRemaining(chompSize));
            chomper.Chomp(chompSize);
            Assert.True(chomper.HasRemaining(chompSize));
            chomper.Chomp(chompSize);
            Assert.False(chomper.HasRemaining(chompSize));
        }

        [Fact]
        public void TestBigEndian4Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.BigEndian, 4, new[] { 0x2, 0x0, 0x0, 0x0, 0x0, 0x4 });
        }

        [Fact]
        public void TestBigEndian8Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.BigEndian, 8, new[] { 0x20, 0x00, 0x04 });
        }

        [Fact]
        public void TestBigEndian12Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.BigEndian, 12, new[] { 0x200, 0x004 });
        }

        [Fact]
        public void TestBigEndian16Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.BigEndian, 16, new[] { 0x2000 });
        }

        [Fact]
        public void TestLittleEndian4Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.LittleEndian, 4, new[] { 0x0, 0x2, 0x0, 0x0, 0x4, 0x0 });
        }

        [Fact]
        public void TestLittleEndian8Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.LittleEndian, 8, new[] { 0x20, 0x00, 0x04 });
        }

        [Fact]
        public void TestLittleEndian12Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.LittleEndian, 12, new[] { 0x020, 0x040 });
        }

        [Fact]
        public void TestLittleEndian16Bit()
        {
            byte[] bytes = new byte[] { 0x20, 0x00, 0x04 };
            AssertChomps(bytes, Endianness.LittleEndian, 16, new[] { 0x0020 });
        }

        private void AssertChomps(
            byte[] bytes, Endianness endianness, int bitsPerChomp, IEnumerable<int> expectedChomps)
        {
            var chomper = new BitChomper(bytes, 0, 0, endianness);
            var chomps = new List<int>();

            while (chomper.HasRemaining(bitsPerChomp))
            {
                chomps.Add(chomper.Chomp(bitsPerChomp));
            }

            Assert.Equal(expectedChomps, chomps);
            Assert.False(chomper.HasRemaining(bitsPerChomp));
        }
    }
}