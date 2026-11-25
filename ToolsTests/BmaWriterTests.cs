using System;
using DSunTools.Common.Image;
using Xunit;

namespace Dsun.Common.Tests
{
    public class BmaWriterTests
    {
        [Fact]
        public void RoundTripsFullAndDeltaFrames()
        {
            byte[] frame0 = { 1, 2, 3, 4, 5, 6, 7, 8 };
            byte[] frame1 = { 1, 2, 9, 4, 5, 6, 7, 10 };
            byte[] frame2 = (byte[])frame1.Clone(); // no-op frame

            byte[] bma = BmaWriter.Write(new[] { frame0, frame1, frame2 }, 4, 2);
            BmaAnimation animation = BmaReader.Read(bma);

            Assert.Equal(3, animation.Frames.Count);
            Assert.Equal(frame0, animation.Frames[0].GetPixels());
            Assert.Equal(frame1, animation.Frames[1].GetPixels());
            Assert.Equal(frame1, animation.Frames[2].GetPixels());
        }

        [Fact]
        public void PreservesContainerFrameGrouping()
        {
            byte[] frame0 = { 1, 1, 1, 1 };
            byte[] frame1 = { 2, 2, 2, 2 };
            byte[] frame2 = { 3, 3, 3, 3 };

            byte[] bma = BmaWriter.Write(
                new[] { frame0, frame1, frame2 },
                2,
                2,
                new[] { 2, 1 });
            BmaAnimation animation = BmaReader.Read(bma);

            Assert.Equal(2, animation.Containers.Count);
            Assert.Equal(2, animation.Containers[0].FrameCount);
            Assert.Equal(1, animation.Containers[1].FrameCount);
            Assert.Equal(3, animation.Frames.Count);
            Assert.Equal(frame0, animation.Frames[0].GetPixels());
            Assert.Equal(frame1, animation.Frames[1].GetPixels());
            Assert.Equal(frame2, animation.Frames[2].GetPixels());
        }

        [Fact]
        public void RejectsLayoutWithWrongFrameCount()
        {
            Assert.Throws<ArgumentException>(() =>
                BmaWriter.Write(new[] { new byte[4] }, 2, 2, new[] { 2 }));
        }

        [Fact]
        public void EncodesRepeatRuns()
        {
            byte[] frame = new byte[16];
            Array.Fill(frame, (byte)0xAA);

            byte[] bma = BmaWriter.Write(new[] { frame }, 4, 4);
            BmaAnimation animation = BmaReader.Read(bma);

            Assert.Equal(frame, animation.Frames[0].GetPixels());
        }

        [Fact]
        public void RejectsWrongFrameSize()
        {
            Assert.Throws<ArgumentException>(() =>
                BmaWriter.Write(new[] { new byte[3] }, 2, 2));
        }
    }
}
