using System;
using System.Collections.Generic;
using System.IO;
using DSunTools.Common.Gff;
using DSunTools.Common.Image;
using Xunit;

namespace Dsun.Common.Tests
{
    public class BmaReaderTests
    {
        [Fact]
        public void DecodesFullFrameAndDeltaFrame()
        {
            byte[] frame0 = BuildFrame(
                width: 4,
                height: 2,
                rows: new[]
                {
                    BuildRow(0, 0x8000, 4, 0x06, 0x01, 0x02, 0x03, 0x04),
                    BuildRow(1, 0x8000, 4, 0x06, 0x05, 0x06, 0x07, 0x08),
                });

            // Only row 1, x=2, two literal pixels 09 0A.
            byte[] frame1 = BuildFrame(
                width: 4,
                height: 2,
                rows: new[]
                {
                    BuildRow(1, 0x8002, 2, 0x02, 0x09, 0x0A),
                });

            // No-op frame: hold the previous screen for one tick.
            byte[] frame2 = BuildFrame(4, 2, Array.Empty<byte[]>());

            BmaAnimation animation = BmaReader.Read(BuildContainer(frame0, frame1, frame2));

            Assert.Equal(4, animation.Width);
            Assert.Equal(2, animation.Height);
            Assert.Equal(3, animation.Frames.Count);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, animation.Frames[0].GetPixels());
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 9, 10 }, animation.Frames[1].GetPixels());
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 9, 10 }, animation.Frames[2].GetPixels());
        }

        [Fact]
        public void DecodesRepeatedRleRun()
        {
            byte[] frame = BuildFrame(
                width: 2,
                height: 1,
                rows: new[]
                {
                    BuildRow(0, 0x8000, 2, 0x03, 0xAA),
                });

            BmaAnimation animation = BmaReader.Read(BuildContainer(frame));

            Assert.Equal(new byte[] { 0xAA, 0xAA }, animation.Frames[0].GetPixels());
        }

        [Fact]
        public void CanvasPersistsAcrossContainers()
        {
            byte[] frame0 = BuildFrame(
                2, 1,
                new[] { BuildRow(0, 0x8000, 2, 0x02, 0x01, 0x02) });
            byte[] frame1 = BuildFrame(
                2, 1,
                new[] { BuildRow(0, 0x8000, 2, 0x02, 0x03, 0x04) });

            byte[] data = Concat(BuildContainer(frame0), BuildContainer(frame1));
            BmaAnimation animation = BmaReader.Read(data);

            Assert.Equal(2, animation.Frames.Count);
            Assert.Equal(new byte[] { 1, 2 }, animation.Frames[0].GetPixels());
            Assert.Equal(new byte[] { 3, 4 }, animation.Frames[1].GetPixels());
        }

        private static byte[] BuildFrame(int width, int height, IReadOnlyList<byte[]> rows)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((ushort)width);
            writer.Write((ushort)height);
            foreach (byte[] row in rows)
                writer.Write(row);
            writer.Write((byte)0xFF);
            return ms.ToArray();
        }

        private static byte[] BuildRow(int row, int xword, byte span, params byte[] rle)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)row);
            writer.Write((ushort)xword);
            writer.Write(span);
            writer.Write((byte)rle.Length);
            writer.Write(rle);
            return ms.ToArray();
        }

        private static byte[] BuildContainer(params byte[][] frames)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            int headerSize = 6 + frames.Length * 4;
            int totalSize = headerSize + Sum(frames);
            writer.Write(totalSize);
            writer.Write((ushort)frames.Length);

            int frameOffset = headerSize;
            foreach (byte[] frame in frames)
            {
                writer.Write(frameOffset);
                frameOffset += frame.Length;
            }

            foreach (byte[] frame in frames)
                writer.Write(frame);

            return ms.ToArray();
        }

        private static int Sum(byte[][] arrays)
        {
            int total = 0;
            foreach (byte[] array in arrays)
                total += array.Length;
            return total;
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            using var ms = new MemoryStream();
            foreach (byte[] array in arrays)
                ms.Write(array, 0, array.Length);
            return ms.ToArray();
        }
    }

    public class GffWithoutGffiTests
    {
        [Fact]
        public void GffWithoutGffiTable_CanBeRead()
        {
            // CINE.GFF has no GFFI type at all; GFFI is only needed for
            // segmented/secondary tables.
            byte[] data = { 1, 2, 3, 4 };

            const int tocOffset = 32;
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)'G'); writer.Write((byte)'F'); writer.Write((byte)'F'); writer.Write((byte)'I');
            writer.Write(0x00030000);
            writer.Write(28);
            writer.Write(tocOffset);
            writer.Write(32); // TOC length
            writer.Write(0);
            writer.Write(1);
            writer.Write(data);
            writer.Write(8);  // types offset
            writer.Write(30); // free-list offset
            writer.Write((ushort)1);
            writer.Write((byte)'T'); writer.Write((byte)'E'); writer.Write((byte)'S'); writer.Write((byte)'T');
            writer.Write(1);
            writer.Write(0);
            writer.Write(28);
            writer.Write(4);
            writer.Write((ushort)0);

            GffFile gff = GffFile.Create(ms.ToArray());

            Assert.True(gff.HasResource("TEST", 0));
            Assert.Equal(data, gff.GetResourceData("TEST", 0));
        }
    }
}
