using System.Buffers.Binary;

namespace DSunTools.Common.Font;

public sealed class FontResource
{
    public byte[] Header { get; init; } = Array.Empty<byte>();
    public List<FontGlyph> Glyphs { get; init; } = new();
}

public static class FontResourceCodec
{
    public const int GlyphCount = 256;
    public const int GlyphHeight = 9;
    public const int OffsetTableStart = 264;
    public const int GlyphDataStart = OffsetTableStart + GlyphCount * sizeof(ushort);

    public static FontResource Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < GlyphDataStart)
            throw new InvalidDataException($"FONT resource is truncated: expected at least {GlyphDataStart} bytes, got {data.Length}.");
        if (data[2] != GlyphHeight)
            throw new InvalidDataException($"Unsupported FONT glyph height {data[2]}; expected {GlyphHeight}.");

        var offsets = new int[GlyphCount];
        for (int i = 0; i < GlyphCount; i++)
        {
            offsets[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(OffsetTableStart + i * sizeof(ushort), sizeof(ushort)));
            if (offsets[i] < GlyphDataStart || offsets[i] > data.Length - sizeof(ushort))
                throw new InvalidDataException($"FONT glyph {i} has an invalid offset {offsets[i]}.");
            if (i > 0 && offsets[i] <= offsets[i - 1])
                throw new InvalidDataException($"FONT glyph offsets are not strictly increasing at glyph {i}.");
        }

        var glyphs = new List<FontGlyph>(GlyphCount);
        for (int i = 0; i < GlyphCount; i++)
        {
            int start = offsets[i];
            int end = i + 1 < GlyphCount ? offsets[i + 1] : data.Length;
            ushort width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start, sizeof(ushort)));
            int expectedLength = sizeof(ushort) + width * GlyphHeight;
            if (end - start != expectedLength)
                throw new InvalidDataException($"FONT glyph {i} has {end - start} bytes, but width {width} requires {expectedLength}.");

            var pixels = new byte[GlyphHeight, width];
            for (int y = 0; y < GlyphHeight; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[y, x] = data[start + sizeof(ushort) + y * width + x];
            }

            glyphs.Add(new FontGlyph { CharacterCode = i, Width = width, Pixels = pixels });
        }

        return new FontResource
        {
            Header = data[..GlyphDataStart],
            Glyphs = glyphs
        };
    }

    public static byte[] Build(FontResource font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (font.Header == null || font.Header.Length != GlyphDataStart)
            throw new InvalidDataException($"FONT header must contain exactly {GlyphDataStart} bytes.");
        if (font.Glyphs == null || font.Glyphs.Count != GlyphCount)
            throw new InvalidDataException($"FONT requires exactly {GlyphCount} glyphs.");

        var glyphs = new FontGlyph[GlyphCount];
        foreach (FontGlyph glyph in font.Glyphs)
        {
            if (glyph == null || glyph.CharacterCode < 0 || glyph.CharacterCode >= GlyphCount || glyphs[glyph.CharacterCode] != null)
                throw new InvalidDataException("FONT glyph codes must uniquely cover the range 0 through 255.");
            if (glyph.Pixels == null || glyph.Pixels.GetLength(0) != GlyphHeight || glyph.Pixels.GetLength(1) != glyph.Width)
                throw new InvalidDataException($"FONT glyph {glyph.CharacterCode} pixel dimensions do not match its width {glyph.Width}.");
            glyphs[glyph.CharacterCode] = glyph;
        }

        if (glyphs.Any(glyph => glyph == null))
            throw new InvalidDataException("FONT glyph codes must uniquely cover the range 0 through 255.");

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        byte[] header = (byte[])font.Header.Clone();

        int offset = GlyphDataStart;
        for (int i = 0; i < GlyphCount; i++)
        {
            if (offset > ushort.MaxValue)
                throw new InvalidDataException("FONT glyph data exceeds the 16-bit offset table capacity.");
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(OffsetTableStart + i * sizeof(ushort), sizeof(ushort)), (ushort)offset);
            offset = checked(offset + sizeof(ushort) + glyphs[i].Width * GlyphHeight);
        }

        writer.Write(header);
        foreach (FontGlyph glyph in glyphs)
        {
            writer.Write(glyph.Width);
            for (int y = 0; y < GlyphHeight; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                    writer.Write(glyph.Pixels[y, x]);
            }
        }

        return stream.ToArray();
    }
}
