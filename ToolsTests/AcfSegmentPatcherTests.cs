using System;
using DSunTools.Common.Image;
using Xunit;

namespace Dsun.Common.Tests
{
    public class AcfSegmentPatcherTests
    {
        [Fact]
        public void ReadsAndPatchesSegmentSizes()
        {
            byte[] acf = BuildAcf(
                (0, 27, 62019),
                (1, 13, 61774));

            var segments = AcfSegmentPatcher.ReadSegments(acf);
            Assert.Equal(2, segments.Count);
            Assert.Equal(62019, segments[0].ByteSize);
            Assert.Equal(61774, segments[1].ByteSize);

            byte[] patched = AcfSegmentPatcher.UpdateSegmentSizes(acf, new[] { 62914, 61774 });
            var patchedSegments = AcfSegmentPatcher.ReadSegments(patched);

            Assert.Equal(62914, patchedSegments[0].ByteSize);
            Assert.Equal(61774, patchedSegments[1].ByteSize);
            Assert.Equal(62019, AcfSegmentPatcher.ReadSegments(acf)[0].ByteSize);
        }

        [Fact]
        public void RejectsWrongSegmentCount()
        {
            byte[] acf = BuildAcf((0, 27, 62019));

            Assert.Throws<System.IO.InvalidDataException>(() =>
                AcfSegmentPatcher.UpdateSegmentSizes(acf, new[] { 1, 2 }));
        }

        private static byte[] BuildAcf(params (int Index, int Frames, int Size)[] segments)
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            foreach (var segment in segments)
            {
                writer.Write((ushort)((0x01 << 8) | (4 * 2))); // op 01, 4 words
                writer.Write((ushort)segment.Index);
                writer.Write((ushort)segment.Frames);
                writer.Write((ushort)segment.Size);
            }
            writer.Write((ushort)0x0002); // end
            return ms.ToArray();
        }
    }
}
