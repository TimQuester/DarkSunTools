using System;
using System.Collections.Generic;
using System.IO;

namespace DSunTools.Common.Image
{
    /// <summary>
    /// Palette state captured from an ACF cinematic script: one palette per
    /// displayed BMA frame, plus the palette left active at the end.
    /// </summary>
    public sealed class AcfPaletteTimeline
    {
        public AcfPaletteTimeline(
            IReadOnlyList<Palette> framePalettes,
            Palette finalPalette,
            byte[] finalPaletteData,
            int entriesSet,
            int? paletteChunkId)
        {
            FramePalettes = framePalettes;
            FinalPalette = finalPalette;
            FinalPaletteData = finalPaletteData;
            EntriesSet = entriesSet;
            PaletteChunkId = paletteChunkId;
        }

        /// <summary>One palette for each op-05 (display BMA frame).</summary>
        public IReadOnlyList<Palette> FramePalettes { get; }

        /// <summary>Palette active after the script's last instruction.</summary>
        public Palette FinalPalette { get; }

        /// <summary>Raw 6-bit palette left active, for chaining cinematics.</summary>
        public byte[] FinalPaletteData { get; }

        public int EntriesSet { get; }
        public int? PaletteChunkId { get; }
    }

    /// <summary>
    /// Walks an ACF word stream and records the palette active at every
    /// op-05 frame display. Only palette state is interpreted; wait/sound/
    /// stream-control instructions are skipped by their encoded length.
    /// </summary>
    public static class AcfPaletteTimelineReader
    {
        public static AcfPaletteTimeline Read(
            byte[] acfData,
            byte[] initialPalette,
            Func<int, Palette> resolvePalette)
        {
            if (acfData == null) throw new ArgumentNullException(nameof(acfData));

            var active = new byte[768];
            if (initialPalette != null && initialPalette.Length >= 768)
                Buffer.BlockCopy(initialPalette, 0, active, 0, 768);

            var staging = (byte[])active.Clone();
            var entryWritten = new bool[256];
            var framePalettes = new List<Palette>();
            int entriesSet = 0;
            int? paletteChunkId = null;
            bool committedInScript = false;
            bool activeValid = initialPalette != null;

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
                        committedInScript = true;
                        activeValid = true;
                        break;

                    case 0x16: // switch to a PAL chunk (observed operands are PAL ids)
                    case 0x3C: // load PAL chunk
                        if (totalWords >= 2)
                            paletteChunkId = ReadWord(acfData, position + 1);

                        // A committed palette wins over a later PAL load, but
                        // before any commit the load establishes the active
                        // palette (used by still-only scripts and ACF 11).
                        if (!committedInScript && resolvePalette != null && paletteChunkId.HasValue)
                        {
                            Palette loaded = resolvePalette(paletteChunkId.Value);
                            if (loaded != null)
                            {
                                // Palette does not expose its raw bytes, so
                                // this path only matters for frame capture;
                                // store its colors via a temporary byte array.
                                // ResolvePalette callers supply PAL chunks with
                                // 6-bit components, matching the ACF layout.
                                for (int i = 0; i < 256; i++)
                                {
                                    var color = loaded.GetColor(i);
                                    active[i * 3] = (byte)(color.Red / 4);
                                    active[i * 3 + 1] = (byte)(color.Green / 4);
                                    active[i * 3 + 2] = (byte)(color.Blue / 4);
                                }

                                Buffer.BlockCopy(active, 0, staging, 0, 768);
                                activeValid = true;
                            }
                        }
                        break;

                    case 0x05: // display next BMA frame
                        framePalettes.Add(Palette.FromPalData((byte[])active.Clone()));
                        break;
                }

                position += totalWords;
            }

            return new AcfPaletteTimeline(
                framePalettes,
                activeValid ? Palette.FromPalData((byte[])active.Clone()) : null,
                activeValid ? (byte[])active.Clone() : null,
                entriesSet,
                paletteChunkId);
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
