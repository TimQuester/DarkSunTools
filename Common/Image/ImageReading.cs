using DSunTools.Common.Image;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSunTools.Common.Image
{
    public static class ImageReading
    {
        private class PixelRunsBuilder
        {
            private readonly List<PixelRun> pixelRuns = new List<PixelRun>();
            private readonly MemoryStream accumulatedBytes = new MemoryStream();
            private int? startX = null;

            public void Record(int x, byte pixel)
            {
                if (!startX.HasValue || x != startX.Value + accumulatedBytes.Length)
                {
                    FinishRun();
                    startX = x;
                }

                accumulatedBytes.WriteByte(pixel);
            }

            public void FinishRun()
            {
                if (!startX.HasValue) return;

                pixelRuns.Add(new PixelRun(startX.Value, accumulatedBytes.ToArray()));
                accumulatedBytes.SetLength(0);
                startX = null;
            }

            public List<PixelRun> GetRuns() => pixelRuns.ToList();
        }

        private class PlanarDecoder
        {
            private readonly Func<int> symbolSource;
            private readonly IReadOnlyList<byte> dictionary;

            public PlanarDecoder(Func<int> symbolSource, IReadOnlyList<byte> dictionary)
            {
                this.symbolSource = symbolSource;
                this.dictionary = dictionary;
            }

            public byte? NextPixel()
            {
                int symbol = symbolSource();
                byte dictionaryValue = dictionary[symbol];
                return dictionaryValue == 0 ? (byte?)null : dictionaryValue;
            }
        }

        private class PlanSymbolSource
        {
            private readonly BitChomper chomper;
            private readonly int bitsPerSymbol;

            public PlanSymbolSource(BitChomper chomper, int bitsPerSymbol)
            {
                this.chomper = chomper;
                this.bitsPerSymbol = bitsPerSymbol;
            }

            public int GetNext() => chomper.Chomp(bitsPerSymbol);
        }

        private class PlnrSymbolSource
        {
            private readonly BitChomper chomper;
            private readonly int bitsPerSymbol;
            private int lastValue = 0;
            private int remaining = 0;

            public PlnrSymbolSource(BitChomper chomper, int bitsPerSymbol)
            {
                this.chomper = chomper;
                this.bitsPerSymbol = bitsPerSymbol;
            }

            public int GetNext()
            {
                if (remaining == 0)
                {
                    int firstCode = chomper.Chomp(bitsPerSymbol);
                    if (firstCode == 0)
                    {
                        int secondCode = chomper.Chomp(bitsPerSymbol);
                        if (secondCode == 0)
                        {
                            lastValue = 0;
                            remaining = 1;
                        }
                        else
                        {
                            remaining = secondCode + 2;
                        }
                    }
                    else
                    {
                        lastValue = firstCode;
                        remaining = 1;
                    }
                }

                remaining--;
                return lastValue;
            }
        }

        private const int NO_MORE_ROWS_ROW_NUMBER = 0xFF;
        private const int COLUMN_256_FLAG = 0x1;
        private const int LAST_RUN_FLAG = 0x80;
        private const string PLAN_TAG = "PLAN";
        private const string PLNR_TAG = "PLNR";

        public static List<ImageFrame> ExtractFrames(byte[] imageBytes)
        {
            using var buffer = new BinaryReader(new MemoryStream(imageBytes));
            buffer.BaseStream.Position = 4; // skip file size

            int numberOfFrames = buffer.ReadUInt16();
            var frames = new List<ImageFrame>(numberOfFrames);

            for (int iFrame = 0; iFrame < numberOfFrames; iFrame++)
            {
                int frameOffset = buffer.ReadInt32();
                frames.Add(ReadFrame(imageBytes, frameOffset));
            }

            return frames;
        }

        private static ImageFrame ReadFrame(byte[] imageBytes, int frameStartOffset)
        {
            if (imageBytes[frameStartOffset + 4] == 0xFF &&
                imageBytes.Length >= frameStartOffset + 5 + PLAN_TAG.Length)
            {
                string tag = Encoding.ASCII.GetString(
                    imageBytes, frameStartOffset + 5, PLAN_TAG.Length);

                if (tag == PLAN_TAG)
                {
                    return ReadPlanarImageFrame(imageBytes, frameStartOffset,
                        (chomper, bits) => new PlanSymbolSource(chomper, bits).GetNext);
                }
                else if (tag == PLNR_TAG)
                {
                    return ReadPlanarImageFrame(imageBytes, frameStartOffset,
                        (chomper, bits) => new PlnrSymbolSource(chomper, bits).GetNext);
                }
            }

            return ReadRowBasedFrame(imageBytes, frameStartOffset);
        }

        private static ImageFrame ReadRowBasedFrame(byte[] imageBytes, int frameStartOffset)
        {
            using var buffer = new BinaryReader(new MemoryStream(imageBytes));
            buffer.BaseStream.Position = frameStartOffset;

            int width = buffer.ReadUInt16();
            int height = buffer.ReadUInt16();

            var runsByRow = new SortedDictionary<int, List<PixelRun>>();

            while (runsByRow.Count < height)
            {
                int rowNumber = buffer.ReadByte();
                if (rowNumber == NO_MORE_ROWS_ROW_NUMBER) break;
                if (rowNumber >= height)
                    throw new Exception($"row number {rowNumber:X} >= frame height {height:X}");

                var runs = new List<PixelRun>();
                while (true)
                {
                    int startX = buffer.ReadByte();
                    int flags = buffer.ReadByte();
                    int uncompressedLength = buffer.ReadByte();
                    int compressedLength = buffer.ReadByte();

                    if ((flags & COLUMN_256_FLAG) != 0)
                        startX += 256;

                    byte[] compressedData = buffer.ReadBytes(compressedLength);
                    byte[] uncompressedData = UncompressPixels(compressedData, uncompressedLength);

                    runs.Add(new PixelRun(startX, uncompressedData));

                    if ((flags & LAST_RUN_FLAG) != 0) break;
                }

                runsByRow[rowNumber] = runs;
            }

            return new ImageFrame(width, height, runsByRow);
        }

        private static byte[] UncompressPixels(byte[] compressedData, int uncompressedLength)
        {
            var uncompressedData = new byte[uncompressedLength];
            int iUncompressed = 0;

            for (int iCompressed = 0; iCompressed < compressedData.Length;)
            {
                int codeByte = compressedData[iCompressed++];
                int runLength = codeByte / 2 + 1;

                if (codeByte % 2 == 0)
                {
                    // Plain bytes run
                    Array.Copy(compressedData, iCompressed,
                             uncompressedData, iUncompressed, runLength);
                    iCompressed += runLength;
                    iUncompressed += runLength;
                }
                else
                {
                    // Repeated byte run
                    byte repeatedByte = compressedData[iCompressed++];
                    for (int i = 0; i < runLength; i++)
                        uncompressedData[iUncompressed++] = repeatedByte;
                }
            }

            return uncompressedData;
        }

        private static ImageFrame ReadPlanarImageFrame(
            byte[] bytes,
            int frameStartPosition,
            Func<BitChomper, int, Func<int>> symbolSourceProvider)
        {
            using var buffer = new BinaryReader(new MemoryStream(bytes));
            buffer.BaseStream.Position = frameStartPosition;

            int width = buffer.ReadUInt16();
            int height = buffer.ReadUInt16();

            buffer.BaseStream.Position = frameStartPosition + 9;
            int bitsPerSymbol = buffer.ReadByte();
            if (bitsPerSymbol == 0)
                return new ImageFrame(width, height, new SortedDictionary<int, List<PixelRun>>());

            int dictionarySize = 1 << bitsPerSymbol;
            var pixelValueDictionary = new List<byte>(dictionarySize);

            for (int i = 0; i < dictionarySize; i++)
                pixelValueDictionary.Add(buffer.ReadByte());

            int codeStart = frameStartPosition + 10 + dictionarySize;
            var chomper = new BitChomper(bytes, codeStart, 0, Endianness.BigEndian);
            var decoder = new PlanarDecoder(symbolSourceProvider(chomper, bitsPerSymbol), pixelValueDictionary);

            var runsByRow = new SortedDictionary<int, List<PixelRun>>();
            for (int y = 0; y < height; y++)
            {
                var runsBuilder = new PixelRunsBuilder();
                for (int x = 0; x < width; x++)
                {
                    var pixel = decoder.NextPixel();
                    if (pixel.HasValue)
                        runsBuilder.Record(x, pixel.Value);
                }
                runsBuilder.FinishRun();
                runsByRow[y] = runsBuilder.GetRuns();
            }

            return new ImageFrame(width, height, runsByRow);
        }
    }
}