using FrameFlux.Helpers;
using SharpDX.Direct3D11;
using System.Drawing;
using System.Drawing.Imaging;
using SharpDX;

namespace FrameFlux.Output.Local
{
    internal static class LocalImageHelper
    {
        /// <summary>
        /// Copy a GPU texture to a bitmap object
        /// </summary>
        public static Bitmap TextureToBitmap(DXGIDupWrapper wrapper, Texture2D texture)
        {
            var desc = texture.Description;

            var stagingDesc = new Texture2DDescription
            {
                Width = desc.Width,
                Height = desc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = desc.Format,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read
            };

            using var staging = new Texture2D(wrapper.Device, stagingDesc);
            wrapper.Device.ImmediateContext.CopyResource(texture, staging);

            var databox = wrapper.Device.ImmediateContext.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);

            var bitmap = new Bitmap(desc.Width, desc.Height, PixelFormat.Format32bppArgb);
            var boundRect = new Rectangle(0, 0, desc.Width, desc.Height);
            var bitmapData = bitmap.LockBits(boundRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* srcPtr = (byte*)databox.DataPointer;
                byte* dstPtr = (byte*)bitmapData.Scan0;
                int rowBytes = desc.Width * 4;

                for (int y = 0; y < desc.Height; y++)
                {
                    byte* srcRow = srcPtr + y * databox.RowPitch;
                    byte* dstRow = dstPtr + y * bitmapData.Stride;
                    System.Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                }
            }

            bitmap.UnlockBits(bitmapData);
            wrapper.Device.ImmediateContext.UnmapSubresource(staging, 0);

            return bitmap;
        }
    }
}
