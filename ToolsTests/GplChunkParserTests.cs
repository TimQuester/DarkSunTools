using System;
using DSunTools.Common.Gpl;
using Xunit;

namespace Dsun.Common.Tests
{
    public class GplChunkParserTests
    {
        // 4F 00 00  92 05 91 A4 18   -> gpl print string 0, "Hi"
        private static readonly byte[] PrintHi =
        {
            0x4F, 0x00, 0x00, 0x92, 0x05, 0x91, 0xA4, 0x18,
        };

        [Fact]
        public void FindsImmediateString_WithoutFalsePositives()
        {
            GplParseResult result = GplChunkParser.Parse(PrintHi);

            Assert.True(result.Aligned, result.Error);
            Assert.Single(result.Strings);

            GplStringSpan span = result.Strings[0];
            Assert.Equal(0x4F, span.Opcode);
            Assert.Equal(4, span.SubTypeOffset);
            Assert.Equal(5, span.ContentStart);
            Assert.Equal(3, span.ContentLength);
            Assert.Equal(2, span.CharCount);
            Assert.Equal("Hi", span.Text);
        }

        [Fact]
        public void ByteIncOpcodeIsNotAString()
        {
            // 05 00 01 -> gpl byte inc 1; then the print-string instruction.
            var chunk = new byte[]
            {
                0x05, 0x00, 0x01,
                0x4F, 0x00, 0x00, 0x92, 0x05, 0x91, 0xA4, 0x18,
            };

            GplParseResult result = GplChunkParser.Parse(chunk);

            Assert.True(result.Aligned, result.Error);
            Assert.Single(result.Strings);
            Assert.Equal("Hi", result.Strings[0].Text);
            // 0x05 0x00 0x01 occupies three bytes, so the string
            // sub-type marker follows at offset 7.
            Assert.Equal(7, result.Strings[0].SubTypeOffset);
        }

        [Fact]
        public void FindsLogString()
        {
            // 2C 05 91 A4 18 -> gpl log "Hi"; 31 -> exit gpl.
            var chunk = new byte[] { 0x2C, 0x05, 0x91, 0xA4, 0x18, 0x31 };

            GplParseResult result = GplChunkParser.Parse(chunk);

            Assert.True(result.Aligned, result.Error);
            Assert.Single(result.Strings);
            Assert.Equal(0x2C, result.Strings[0].Opcode);
            Assert.Equal(1, result.Strings[0].SubTypeOffset);
            Assert.Equal("Hi", result.Strings[0].Text);
        }

        [Fact]
        public void LoadAccumWithZeroParameterRetval_StaysAligned()
        {
            // 18 E2 8C 38 E1 -> gpl load accum ( gpl getyn )
            // gpl getyn takes no parameters, which is legal inside RETVAL.
            // Then the print-string instruction must still be reached.
            var chunk = new byte[]
            {
                0x18, 0xE2, 0x8C, 0x38, 0xE1,
                0x4F, 0x00, 0x00, 0x92, 0x05, 0x91, 0xA4, 0x18,
                0x31,
            };

            GplParseResult result = GplChunkParser.Parse(chunk);

            Assert.True(result.Aligned, result.Error);
            Assert.Single(result.Strings);
            Assert.Equal("Hi", result.Strings[0].Text);
            Assert.Equal(0x4F, result.Strings[0].Opcode);
        }

        [Fact]
        public void TwoStringsInOneMenu_AreBothFound()
        {
            // 48 (menu):
            //   title: 92 05 <packed "One"> (we use "Hi" payload for simplicity)
            //   entry: label 92 05 <"Hi">, target 00 00, enabled 00 01
            //   4A terminator
            var chunk = new byte[]
            {
                0x48,
                0x92, 0x05, 0x91, 0xA4, 0x18,
                0x92, 0x05, 0x91, 0xA4, 0x18,
                0x00, 0x00,
                0x00, 0x01,
                0x4A,
            };

            GplParseResult result = GplChunkParser.Parse(chunk);

            Assert.True(result.Aligned, result.Error);
            Assert.Equal(2, result.Strings.Count);
            Assert.All(result.Strings, s => Assert.Equal("Hi", s.Text));
        }
    }
}
