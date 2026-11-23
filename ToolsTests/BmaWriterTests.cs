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
