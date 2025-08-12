using Vortice.Direct3D11;
using Vortice.DXGI;

namespace FrameFlux.Core
{
    // This class represents a single frame captured from the desktop duplication API,
    // providing func to access pixel data and metadata about the frame.
    internal sealed class Frame : IDisposable
    {
        private readonly IDXGIResource _resource;
        private readonly IDXGIOutputDuplication _duplication;
        private readonly ID3D11Texture2D _gpuTexture;

        private ID3D11Texture2D? _stagingTexture;
        private readonly ID3D11Device _device;

        private byte[]? _pixelBuffer; // Reused buffer for pixel data,squeezing every bit of performance
                                      // we can get, 1ms is a lot of time in this context

        public OutduplFrameInfo Info { get; }

        public int Width => (int)_gpuTexture.Description.Width;
        public int Height => (int)_gpuTexture.Description.Height;
        public Vortice.DXGI.Format Format => _gpuTexture.Description.Format;

        public Frame(IDXGIResource resource, OutduplFrameInfo info, IDXGIOutputDuplication duplication)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            Info = info;
            _duplication = duplication ?? throw new ArgumentNullException(nameof(duplication));

            _gpuTexture = resource.QueryInterfaceOrNull<ID3D11Texture2D>()
                ?? throw new InvalidOperationException("Resource is not a valid D3D11 texture.");

            _device = _gpuTexture.Device
                ?? throw new InvalidOperationException("Failed to retrieve D3D11 device from texture.");
        }

        public unsafe byte[]? GetFrameBytes()
        {
            if (_stagingTexture == null)
            {
                var desc = _gpuTexture.Description;
                desc.Usage = ResourceUsage.Staging;
                desc.CPUAccessFlags = CpuAccessFlags.Read;
                desc.BindFlags = BindFlags.None;
                desc.MiscFlags = ResourceOptionFlags.None;

                _stagingTexture = _device.CreateTexture2D(desc);
            }

            var context = _device.ImmediateContext;
            context.CopyResource(_stagingTexture, _gpuTexture);

            var dataBox = context.Map(_stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

            try
            {
                int width = Width;
                int height = Height;
                int bytesPerPixel = Format switch
                {
                    Vortice.DXGI.Format.B8G8R8A8_UNorm => 4,
                    Vortice.DXGI.Format.R8G8B8A8_UNorm => 4,
                    _ => 4
                };

                int rowPitch = (int)dataBox.RowPitch; // Real size of a row in bytes to account for padding
                int requiredSize = width * height * bytesPerPixel;

                if (_pixelBuffer == null || _pixelBuffer.Length != requiredSize)
                {
                    _pixelBuffer = new byte[requiredSize];
                }

                byte* pSrc = (byte*)dataBox.DataPointer;
                fixed (byte* pDst = _pixelBuffer)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(pSrc + y * rowPitch, pDst + y * width * bytesPerPixel,
                            width * bytesPerPixel, width * bytesPerPixel);
                    }
                }

                return _pixelBuffer;
            }
            finally
            {
                context.Unmap(_stagingTexture, 0);
            }
        }

        public void Dispose()
        {
            _stagingTexture?.Dispose();
            _resource.Dispose();
            _duplication.ReleaseFrame();
        }
    }
}
