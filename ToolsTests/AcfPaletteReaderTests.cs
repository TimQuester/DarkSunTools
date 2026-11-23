using DSunTools.Common.Image;
using Xunit;

namespace Dsun.Common.Tests
{
    public class AcfPaletteReaderTests
    {
        [Fact]
        public void ReadsCommittedOp14Palette()
        {
            byte[] acf =
            {
                0x06, 0x14, // op 0x14, 3 words: set entry
                0x06, 0x01, // index 1, R = 6
                0x0A, 0x04, // G = 4, B = 10
                0x02, 0x15, // op 0x15, 1 word: commit
                0x02, 0x00, // end
            };

            AcfPaletteInfo info = AcfPaletteReader.Read(acf);

            Assert.NotNull(info.BuiltPalette);
            Assert.Equal(1, info.EntriesSet);
            var color = info.BuiltPalette.GetColor(1);
            Assert.Equal(24, color.Red);
            Assert.Equal(16, color.Green);
            Assert.Equal(40, color.Blue);
        }

        [Fact]
        public void ReadsPaletteChunkReference()
        {
            byte[] acf =
            {
                0x04, 0x3C, // op 0x3C, 2 words: load PAL
                0x0A, 0x00, // PAL id 10
                0x02, 0x00, // end
            };

            AcfPaletteInfo info = AcfPaletteReader.Read(acf);

            Assert.Null(info.BuiltPalette);
            Assert.Equal(10, info.PaletteChunkId);
        }

        [Fact]
        public void EmptyScriptHasNoPalette()
        {
            AcfPaletteInfo info = AcfPaletteReader.Read(new byte[] { 0x02, 0x00 });

            Assert.Null(info.BuiltPalette);
            Assert.Null(info.PaletteChunkId);
            Assert.Equal(0, info.EntriesSet);
        }
    }
}
