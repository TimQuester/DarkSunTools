using System;
using DSunTools.Common.Image;
using Xunit;

namespace Dsun.Common.Tests
{
    public class AcfPaletteTimelineTests
    {
        [Fact]
        public void CapturesPaletteAtEachFrame()
        {
            byte[] acf =
            {
                0x06, 0x14, 0x01, 0x00, 0x03, 0x02, // entry 0: 1,2,3
                0x02, 0x15,                         // commit
                0x02, 0x05,                         // display frame
                0x06, 0x14, 0x04, 0x00, 0x06, 0x05, // entry 0: 4,5,6
                0x02, 0x15,                         // commit
                0x02, 0x05,                         // display frame
                0x02, 0x00,                         // end
            };

            AcfPaletteTimeline timeline = AcfPaletteTimelineReader.Read(acf, null, null);

            Assert.Equal(2, timeline.FramePalettes.Count);
            var first = timeline.FramePalettes[0].GetColor(0);
            Assert.Equal(4, first.Red);
            Assert.Equal(8, first.Green);
            Assert.Equal(12, first.Blue);

            var second = timeline.FramePalettes[1].GetColor(0);
            Assert.Equal(16, second.Red);
            Assert.Equal(20, second.Green);
            Assert.Equal(24, second.Blue);
        }

        [Fact]
        public void InheritsInitialPaletteWhenScriptHasOnlyFrames()
        {
            var initial = new byte[768];
            initial[0] = 10;
            initial[1] = 20;
            initial[2] = 30;
            byte[] acf = { 0x02, 0x05, 0x02, 0x00 };

            AcfPaletteTimeline timeline = AcfPaletteTimelineReader.Read(acf, initial, null);

            Assert.Single(timeline.FramePalettes);
            var color = timeline.FramePalettes[0].GetColor(0);
            Assert.Equal(40, color.Red);
            Assert.Equal(80, color.Green);
            Assert.Equal(120, color.Blue);
        }

        [Fact]
        public void Op16SelectsPaletteChunk()
        {
            byte[] paletteData = new byte[768];
            paletteData[0] = 2;
            paletteData[1] = 3;
            paletteData[2] = 4;
            var palette = Palette.FromPalData(paletteData);
            byte[] acf =
            {
                0x04, 0x16, 0x08, 0x00, // op 0x16, PAL 8
                0x02, 0x05,             // display frame
                0x02, 0x00,             // end
            };

            AcfPaletteTimeline timeline = AcfPaletteTimelineReader.Read(acf, null, id => id == 8 ? palette : null);

            Assert.Single(timeline.FramePalettes);
            var color = timeline.FramePalettes[0].GetColor(0);
            Assert.Equal(8, color.Red);
            Assert.Equal(12, color.Green);
            Assert.Equal(16, color.Blue);
            Assert.Equal(8, timeline.PaletteChunkId);
        }

        [Fact]
        public void ResolvesPaletteChunkBeforeFirstFrame()
        {
            byte[] paletteData = new byte[768];
            paletteData[0] = 7;
            paletteData[1] = 8;
            paletteData[2] = 9;
            var palette = Palette.FromPalData(paletteData);
            byte[] acf =
            {
                0x04, 0x3C, 0x02, 0x00, // load PAL 2
                0x02, 0x05,             // display frame
                0x02, 0x00,             // end
            };

            AcfPaletteTimeline timeline = AcfPaletteTimelineReader.Read(acf, null, id => id == 2 ? palette : null);

            Assert.Single(timeline.FramePalettes);
            var color = timeline.FramePalettes[0].GetColor(0);
            Assert.Equal(28, color.Red);
            Assert.Equal(32, color.Green);
            Assert.Equal(36, color.Blue);
        }
    }
}
