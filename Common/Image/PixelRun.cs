namespace DSunTools.Common.Image
{
    public class PixelRun
    {
        public int StartX { get; }
        private readonly byte[] pixels;

        public PixelRun(int startX, byte[] pixels)
        {
            this.StartX = startX;
            this.pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        }

        public int Length => pixels.Length;

        public void WriteTo(byte[] dest, int destPos)
        {
            if (dest == null)
                throw new ArgumentNullException(nameof(dest));
            if (destPos < 0 || destPos + pixels.Length > dest.Length)
                throw new ArgumentOutOfRangeException(nameof(destPos));

            System.Buffer.BlockCopy(pixels, 0, dest, destPos, pixels.Length);
        }
    }
}