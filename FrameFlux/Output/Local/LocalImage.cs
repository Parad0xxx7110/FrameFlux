using FrameFlux.Core;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FrameFlux.Output.Local
{


    // Only used for debugging purposes and dataset creation at low fps rates.

    // TODO : Add support for other formats (e.g. PNG, JPEG), Add imageSharp for better performance and flexibility
    internal class LocalImage
    {
        static public void SaveFrameAsBmp(Frame frame, string path)
        {
            byte[] pixels = frame.GetFrameBytes() ?? throw new InvalidOperationException("Frame data missing");

            int width = frame.Width;
            int height = frame.Height;

            PixelFormat pixelFormat = PixelFormat.Format32bppArgb;

            using Bitmap bmp = new Bitmap(width, height, pixelFormat);

            var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);

            try
            {
                int bytesPerPixel = 4;
                nint ptrBmp = bmpData.Scan0;
                int bmpStride = bmpData.Stride;

                for (int y = 0; y < height; y++)
                {
                    int srcOffset = y * width * bytesPerPixel;
                    nint destPtr = ptrBmp + y * bmpStride;

                    Marshal.Copy(pixels, srcOffset, destPtr, width * bytesPerPixel);
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            bmp.Save(path, ImageFormat.Bmp);
        }
    }
}
