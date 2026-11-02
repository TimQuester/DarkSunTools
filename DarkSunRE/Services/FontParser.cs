using DarkSunRE.Models;
using DSunTools.Common.Font;
using System.Collections.Generic;
using System.Linq;

namespace DarkSunRE.Services
{
    public class FontParser
    {
        private FontResource _fontResource;

        public List<FontCharacter> ParseFontData(byte[] data)
        {
            _fontResource = FontResourceCodec.Parse(data);
            return _fontResource.Glyphs.Select(glyph => new FontCharacter
            {
                CharCode = glyph.CharacterCode,
                Width = glyph.Width,
                Pixels = glyph.Pixels
            }).ToList();
        }

        public byte[] BuildFontData(List<FontCharacter> characters)
        {
            if (_fontResource == null)
                throw new InvalidOperationException("FONT data must be parsed before it can be rebuilt.");

            var glyphs = characters.Select(character => new FontGlyph
            {
                CharacterCode = character.CharCode,
                Width = checked((ushort)character.Width),
                Pixels = character.Pixels
            }).ToList();

            return FontResourceCodec.Build(new FontResource
            {
                Header = _fontResource.Header,
                Glyphs = glyphs
            });
        }
    }
}