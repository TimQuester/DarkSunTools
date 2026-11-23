using System;
using System.IO;

namespace DSunTools.Common.Image
{
    /// <summary>
    /// Palette information recovered from an ACF cinematic script.
    /// </summary>
    public sealed class AcfPaletteInfo
    {
        public AcfPaletteInfo(Palette builtPalette, int entriesSet, int? paletteChunkId)
        {
            BuiltPalette = builtPalette;
            EntriesSet = entriesSet;
            PaletteChunkId = paletteChunkId;
        }

        /// <summary>
        /// Palette assembled from the ACF op-14 / op-15 instructions. Null
        /// when the script only loads a PAL chunk (or has no palette ops).
        /// </summary>
        public Palette BuiltPalette { get; }

        /// <summary>Number of distinct palette entries written by op-14.</summary>
        public int EntriesSet { get; }

        /// <summary>The last PAL id referenced by an op-3C load, if any.</summary>
        public int? PaletteChunkId { get; }
    }

    /// <summary>
    /// Extracts only the palette-building instructions from a DS1 ACF
    /// script. The animation/control logic is intentionally not executed;
    /// this exists so BMA frames can be shown with the palette the
    /// cinematic committed instead of a random PAL chunk.
    ///
    /// ACF word stream: an instruction is
    /// <c>(opcode &lt;&lt; 8) | (2 * total_words)</c>, operands follow.
    /// Op-14 operands pack (index, R) and (G, B); op-15 commits the staging
    /// palette; op-3C loads a PAL chunk by id.
    /// </summary>
    public static class AcfPaletteReader
    {
        public static AcfPaletteInfo Read(byte[] acfData)
        {
            if (acfData == null) throw new ArgumentNullException(nameof(acfData));

            var staging = new byte[768];
            var active = new byte[768];
            var entryWritten = new bool[256];
            byte[] bestPalette = null;
            int entriesSet = 0;
            int bestEntries = -1;
            int? paletteChunkId = null;

            int wordCount = acfData.Length / 2;
            int position = 0;

            while (position < wordCount)
            {
                int instruction = ReadWord(acfData, position);
                int opcode = instruction >> 8;
                int totalWords = (instruction & 0xFF) / 2;
                if (totalWords < 1 || position + totalWords > wordCount)
                    break;

                switch (opcode)
                {
                    case 0x14: // staged palette entry: idx/R, G/B
                        if (totalWords >= 3)
                        {
                            int first = ReadWord(acfData, position + 1);
                            int second = ReadWord(acfData, position + 2);
                            int index = first >> 8;
                            if (index >= 0 && index < 256)
                            {
                                int offset = index * 3;
                                staging[offset] = (byte)(first & 0x3F);
                                staging[offset + 1] = (byte)((second >> 8) & 0x3F);
                                staging[offset + 2] = (byte)(second & 0x3F);
                                if (!entryWritten[index])
                                {
                                    entryWritten[index] = true;
                                    entriesSet++;
                                }
                            }
                        }
                        break;

                    case 0x15: // commit staging palette
                        Buffer.BlockCopy(staging, 0, active, 0, active.Length);
                        if (entriesSet > bestEntries)
                        {
                            bestEntries = entriesSet;
                            bestPalette = (byte[])active.Clone();
                        }
                        break;

                    case 0x3C: // load PAL chunk
                        if (totalWords >= 2)
                            paletteChunkId = ReadWord(acfData, position + 1);
                        break;
                }

                position += totalWords;
            }

            // If the script wrote entries but never committed, expose the
            // staged values as a last resort; the engine normally commits.
            if (bestPalette == null && entriesSet != 0)
            {
                bestPalette = (byte[])staging.Clone();
                bestEntries = entriesSet;
            }

            Palette palette = bestPalette == null ? null : Palette.FromPalData(bestPalette);
            return new AcfPaletteInfo(palette, bestEntries < 0 ? entriesSet : bestEntries, paletteChunkId);
        }

        private static int ReadWord(byte[] data, int wordIndex)
        {
            int offset = wordIndex * 2;
            if (offset < 0 || offset + 2 > data.Length)
                throw new InvalidDataException("ACF read past the end of the chunk.");

            return data[offset] | (data[offset + 1] << 8);
        }
    }
}
