namespace DarkSunRE.Models
{
    /// <summary>
    /// Semantic buckets shown under each GFF file. This is deliberately
    /// separate from <see cref="ResourceType"/>: several chunk kinds share a
    /// raw/hex viewer, but still belong to different resource families.
    /// </summary>
    public enum ResourceGroup
    {
        Graphics,
        Palettes,
        Fonts,
        Text,
        Scripts,
        Audio,
        UI,
        Cinematics,
        World,
        Objects,
        GameData,
        Save,
        Internal,
        Unknown
    }
}
