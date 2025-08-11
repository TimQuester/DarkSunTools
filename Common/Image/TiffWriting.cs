using System;
using System.Collections.Generic;
using BitMiracle.LibTiff.Classic;

namespace DSunTools.Common.Image
{
    public static class TiffWriting
    {
        public interface IPixelSampleSetter
        {
            void Set(int x, int y, int red, int green, int blue, int alpha);
        }

        public interface IPixelsSetter
        {
            void SetPixels(IPixelSampleSetter pixelSetter);
        }

        public static Tiff CreateRgbTiffDirectory(
            int width, int height, IPixelsSetter pixelsSetter, string outputPath)
        {
            using (Tiff tiff = Tiff.Open(outputPath, "w"))
            {
                if (tiff == null)
                    throw new InvalidOperationException("Could not create TIFF file");

                // Set basic TIFF tags
                tiff.SetField(TiffTag.IMAGEWIDTH, width);
                tiff.SetField(TiffTag.IMAGELENGTH, height);
                tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
                tiff.SetField(TiffTag.COMPRESSION, Compression.DEFLATE);
                tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                tiff.SetField(TiffTag.SAMPLESPERPIXEL, 4);
                tiff.SetField(TiffTag.EXTRASAMPLES, 1, new[] { (short)ExtraSample.ASSOCALPHA });
                tiff.SetField(TiffTag.ROWSPERSTRIP, tiff.DefaultStripSize(0));
                tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                tiff.SetField(TiffTag.SAMPLEFORMAT, SampleFormat.UINT);

                // Create pixel buffer
                byte[] raster = new byte[width * height * 4];
                var pixelSetter = new PixelSampleSetterImpl(width, raster);
                pixelsSetter.SetPixels(pixelSetter);

                // Write the image to the file
                for (int row = 0; row < height; row++)
                {
                    int offset = row * width * 4;
                    if (!tiff.WriteScanline(raster, offset, row, 0))
                        throw new InvalidOperationException("Failed to write TIFF scanline");
                }

                return tiff;
            }
        }

        private class PixelSampleSetterImpl : IPixelSampleSetter
        {
            private readonly int _width;
            private readonly byte[] _raster;

            public PixelSampleSetterImpl(int width, byte[] raster)
            {
                _width = width;
                _raster = raster;
            }

            public void Set(int x, int y, int r, int g, int b, int a)
            {
                int offset = (y * _width + x) * 4;
                _raster[offset] = (byte)r;
                _raster[offset + 1] = (byte)g;
                _raster[offset + 2] = (byte)b;
                _raster[offset + 3] = (byte)a;
            }
        }
    }
}