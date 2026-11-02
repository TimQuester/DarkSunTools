using System.Buffers.Binary;
using DSunTools.Common.Font;

namespace Dsun.Common.Tests;

public class FontResourceCodecTests
{
    [Fact]
    public void ParseAndBuild_PreservesFontByteForByte()
    {
        FontResource font = new()
        {
            Header = new byte[FontResourceCodec.GlyphDataStart],
            Glyphs = Enumerable.Range(0, FontResourceCodec.GlyphCount)
                .Select(code =>
                {
                    ushort width = (ushort)(code % 8);
                    var pixels = new byte[FontResourceCodec.GlyphHeight, width];
                    for (int y = 0; y < FontResourceCodec.GlyphHeight; y++)
                    {
                        for (int x = 0; x < width; x++)
                            pixels[y, x] = (byte)((code + x + y) % 256);
                    }
                    return new FontGlyph { CharacterCode = code, Width = width, Pixels = pixels };
                })
                .ToList()
        };
        font.Header[1] = 1;
        font.Header[2] = FontResourceCodec.GlyphHeight;

        byte[] original = FontResourceCodec.Build(font);
        byte[] rebuilt = FontResourceCodec.Build(FontResourceCodec.Parse(original));

        Assert.Equal(256, FontResourceCodec.Parse(rebuilt).Glyphs.Count);
        Assert.Equal(original, rebuilt);
    }

    [Fact]
    public void ParseAndBuild_UpdatesGlyphOffsetsAfterWidthChanges()
    {
        byte[] fontBytes = CreateMinimalFont();
        FontResource font = FontResourceCodec.Parse(fontBytes);
        font.Glyphs[65].Width = 2;
        font.Glyphs[65].Pixels = new byte[FontResourceCodec.GlyphHeight, 2];

        byte[] rebuilt = FontResourceCodec.Build(font);
        FontResource reparsed = FontResourceCodec.Parse(rebuilt);

        Assert.Equal(2, reparsed.Glyphs[65].Width);
        Assert.Equal(0, reparsed.Glyphs[66].Width);
        Assert.Equal(FontResourceCodec.GlyphDataStart + 66 * 2 + 2 * FontResourceCodec.GlyphHeight, ReadGlyphOffset(rebuilt, 66));
    }

    [Fact]
    public void Parse_RejectsInvalidGlyphOffsets()
    {
        byte[] fontBytes = CreateMinimalFont();
        BinaryPrimitives.WriteUInt16LittleEndian(fontBytes.AsSpan(FontResourceCodec.OffsetTableStart + 3 * 2, 2), 0);

        Assert.Throws<InvalidDataException>(() => FontResourceCodec.Parse(fontBytes));
    }

    private static byte[] CreateMinimalFont()
    {
        byte[] data = new byte[FontResourceCodec.GlyphDataStart + FontResourceCodec.GlyphCount * sizeof(ushort)];
        data[1] = 1;
        data[2] = FontResourceCodec.GlyphHeight;
        for (int i = 0; i < FontResourceCodec.GlyphCount; i++)
        {
            int offset = FontResourceCodec.GlyphDataStart + i * sizeof(ushort);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(FontResourceCodec.OffsetTableStart + i * 2, 2), (ushort)offset);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), 0);
        }
        return data;
    }

    private static int ReadGlyphOffset(byte[] data, int index)
        => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(FontResourceCodec.OffsetTableStart + index * 2, 2));
}
