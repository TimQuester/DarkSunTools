using System;
using DSunTools.Common.Gpl;
using Xunit;

namespace Dsun.Common.Tests
{
    public class GplStringCodecTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 3)]
        [InlineData(7, 7)]
        [InlineData(8, 8)]
        [InlineData(100, 89)]
        public void PackedLength_IsTight(int chars, int expectedBytes)
        {
            Assert.Equal(expectedBytes, GplStringCodec.PackedLength(chars));
            Assert.Equal(chars, GplStringCodec.MaxCharsForBytes(expectedBytes));
        }

        [Fact]
        public void Pack_Hi_MatchesReferenceBytes()
        {
            byte[] packed = GplStringCodec.PackCompressed("Hi", 3);
            Assert.Equal(new byte[] { 0x91, 0xA4, 0x18 }, packed);
        }

        [Fact]
        public void Decode_Hi_FromReferenceBytes()
        {
            byte[] packed = { 0x91, 0xA4, 0x18 };
            Assert.True(GplStringCodec.TryDecodeCompressed(
                packed, 0, out string text, out int consumed, out string error));
            Assert.Null(error);
            Assert.Equal("Hi", text);
            Assert.Equal(3, consumed);
        }

        [Fact]
        public void Pack_Then_Decode_RoundTrips()
        {
            const string text = "The quick brown fox jumps over the lazy dog.";
            int length = GplStringCodec.PackedLength(text.Length);
            byte[] packed = GplStringCodec.PackCompressed(text, length);

            Assert.True(GplStringCodec.TryDecodeCompressed(
                packed, 0, out string decoded, out int consumed, out _));
            Assert.Equal(text, decoded);
            Assert.Equal(length, consumed);
        }

        [Fact]
        public void Decode_StopsAtTerminator_EvenWithTrailingBytes()
        {
            // "Hi" followed by zero bytes. The decoder must stop at 0x03
            // and report how many bytes the string actually occupies.
            byte[] packed = { 0x91, 0xA4, 0x18, 0x00, 0x00 };

            Assert.True(GplStringCodec.TryDecodeCompressed(
                packed, 0, out string decoded, out int consumed, out _));
            Assert.Equal("Hi", decoded);
            Assert.Equal(3, consumed);
        }

        [Fact]
        public void Pack_RejectsPadding()
        {
            // GPL strings are inline: bytes after 0x03 are executed as
            // opcodes, so a padded packing would corrupt the chunk.
            Assert.Throws<ArgumentException>(() =>
                GplStringCodec.PackCompressed("Hi", 8));
        }

        [Fact]
        public void Pack_RefusesToShortenSilently()
        {
            Assert.Throws<ArgumentException>(() =>
                GplStringCodec.PackCompressed("Hello", 2));
        }

        [Fact]
        public void Pack_RefusesNonSevenBitCharacters()
        {
            Assert.Throws<ArgumentException>(() =>
                GplStringCodec.PackCompressed("Привет", 20));
        }

        [Fact]
        public void Pack_RefusesEmbeddedTerminator()
        {
            Assert.Throws<ArgumentException>(() =>
                GplStringCodec.PackCompressed("A\u0003B", 10));
        }

        [Fact]
        public void Decode_RejectsTruncatedPayload()
        {
            byte[] packed = { 0x91, 0xA4 }; // no terminator
            Assert.False(GplStringCodec.TryDecodeCompressed(
                packed, 0, out _, out _, out string error));
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
    }
}
