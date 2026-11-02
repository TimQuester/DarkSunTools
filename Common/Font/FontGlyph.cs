namespace DSunTools.Common.Font;

public sealed class FontGlyph
{
    public int CharacterCode { get; set; }
    public ushort Width { get; set; }
    public byte[,] Pixels { get; set; } = new byte[9, 0];
}
