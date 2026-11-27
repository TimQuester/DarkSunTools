using DSunTools.Common.Image;
using System.Linq;
using Xunit;

namespace Dsun.Common.Tests
{
    public class BitmapContainerWriterTests
    {
        [Fact]
        public void RoundTripsFrames()
        {
            byte[] first = new byte[16 * 9];
            for (int i = 0; i < first.Length; i++)
                first[i] = (byte)(i % 5);

            byte[] second = (byte[])first.Clone();
            second[0] = 0xFF;
            second[second.Length - 1] = 0xAA;

            byte[] encoded = BitmapContainerWriter.Write(new[] { first, second }, 16, 9);
            var frames = ImageReading.ExtractFrames(encoded);

            Assert.Equal(2, frames.Count);
            Assert.Equal(first, frames[0].GetPixels());
            Assert.Equal(second, frames[1].GetPixels());
        }

        [Fact]
        public void HandlesWideRowsAndRepeatRuns()
        {
            byte[] pixels = new byte[320 * 4];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)(i / 40 % 3);

            byte[] encoded = BitmapContainerWriter.Write(new[] { pixels }, 320, 4);
            var frame = ImageReading.ExtractFrames(encoded).Single();

            Assert.Equal(pixels, frame.GetPixels());
        }
    }
}
