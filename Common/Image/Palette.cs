using System;
using System.Collections.Generic;

namespace DSunTools.Common.Image
{
    public class Palette
    {
        private const int BytesPerColor = 3;
        private readonly IReadOnlyList<Color> colors;

        public static Palette FromPalData(byte[] palData)
        {
            if (palData == null)
                throw new ArgumentNullException(nameof(palData));
            if (palData.Length % BytesPerColor != 0)
                throw new ArgumentException("Invalid palette data length");

            int numberOfColors = palData.Length / BytesPerColor;
            var colors = new List<Color>(numberOfColors);

            for (int iColor = 0; iColor < numberOfColors; iColor++)
            {
                int colorOffset = iColor * BytesPerColor;
                int red = 4 * palData[colorOffset + 0];
                int green = 4 * palData[colorOffset + 1];
                int blue = 4 * palData[colorOffset + 2];

                colors.Add(new Color(red, green, blue));
            }

            return new Palette(colors);
        }

        private Palette(IReadOnlyList<Color> colors)
        {
            this.colors = colors ?? throw new ArgumentNullException(nameof(colors));
        }

        public Color GetColor(int paletteIndex)
        {
            if (paletteIndex < 0 || paletteIndex >= colors.Count)
                throw new ArgumentOutOfRangeException(nameof(paletteIndex));

            return colors[paletteIndex];
        }
    }
}