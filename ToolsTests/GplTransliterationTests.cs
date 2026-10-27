using System;
using System.Collections.Generic;
using DSunTools.Common.Gpl;
using Xunit;

namespace Dsun.Common.Tests
{
    public class GplTransliterationTests
    {
        private static readonly List<GplTransliterationRule> Rules = new()
        {
            new GplTransliterationRule("A", "А"),
            new GplTransliterationRule("B", "Б"),
            new GplTransliterationRule("C", "В"),
        };

        [Fact]
        public void Encode_MapsDisplayedCyrillicBackToGameGlyphs()
        {
            Assert.Equal("ABC", GplTransliteration.EncodeToGameCharset("АБВ", Rules));
        }

        [Fact]
        public void Encode_LeavesExistingLatinTextUntouched()
        {
            Assert.Equal("ABC", GplTransliteration.EncodeToGameCharset("ABC", Rules));
            Assert.Equal("ABC ABC", GplTransliteration.EncodeToGameCharset("АБВ ABC", Rules));
        }

        [Fact]
        public void Decode_MapsGameGlyphsToDisplayedCharacters()
        {
            Assert.Equal("АБВ", GplTransliteration.DecodeToDisplay("ABC", Rules));
        }

        [Fact]
        public void MultiCharacterRules_AreIgnored()
        {
            var rules = new List<GplTransliterationRule>
            {
                new GplTransliterationRule("A", "АБ"),
            };

            Assert.Equal("АБ", GplTransliteration.EncodeToGameCharset("АБ", rules));
        }

        [Fact]
        public void UnmappedCharactersAreLeftForTheEncoderToReject()
        {
            // Г is deliberately absent from Rules: it must survive
            // encoding so the string encoder can report it.
            Assert.Equal("ABCГ", GplTransliteration.EncodeToGameCharset("АБВГ", Rules));
        }

        [Fact]
        public void EmptyInput_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, GplTransliteration.EncodeToGameCharset(string.Empty, Rules));
            Assert.Equal(string.Empty, GplTransliteration.EncodeToGameCharset(null, Rules));
            Assert.Equal("ABC", GplTransliteration.EncodeToGameCharset("ABC", null));
        }
    }
}
