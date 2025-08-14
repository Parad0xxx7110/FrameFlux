using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace FrameFlux.Core
{

    // I think i can't optimize this further... Thinking about implementing shader pre-processing
    // instead of CPU for image resizing, normalizing, etc


    // This class represents a single frame captured from the desktop duplication API,
    // providing func to access pixel data and metadata about the frame.
    public sealed class Frame : IDisposable
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
                    Vortice.DXGI.Format.B5G6R5_UNorm => 2,       
                    Vortice.DXGI.Format.B5G5R5A1_UNorm => 2,       
                    Vortice.DXGI.Format.R16G16B16A16_Float => 8,  
                    Vortice.DXGI.Format.R32G32B32A32_Float => 16,  
                    Vortice.DXGI.Format.R8_UNorm => 1,              
                    _ => throw new NotSupportedException($"Unsupported: {Format}")
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



        // SIMD optimized version of GetFrameBytes, mostly same logic but using SIMD intrinsics
        public unsafe byte[]? FastGetFrameBytes()
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

                if (width <= 0 || height <= 0)
                {
                    throw new InvalidOperationException("Invalid texture dimensions.");
                }

                int bytesPerPixel = Format switch
                {
                    Vortice.DXGI.Format.B8G8R8A8_UNorm => 4,       // BGRA 8 bits
                    Vortice.DXGI.Format.R8G8B8A8_UNorm => 4,       // RGBA 8 bits
                    Vortice.DXGI.Format.B5G6R5_UNorm => 2,         // 16 bits (5-6-5 bits)
                    Vortice.DXGI.Format.B5G5R5A1_UNorm => 2,       // 16 bits (5-5-5-1 bits)
                    Vortice.DXGI.Format.R16G16B16A16_Float => 8,   // 64 bits float (16 bits x4)
                    Vortice.DXGI.Format.R32G32B32A32_Float => 16,  // 128 bits float (32 bits x4)
                    Vortice.DXGI.Format.R8_UNorm => 1,             // 8 bits (grayscale or alpha)
                    _ => throw new NotSupportedException($"Unsupported: {Format}")
                };

                int rowPitch = (int)dataBox.RowPitch;
                int requiredSize = width * height * bytesPerPixel;

                if (_pixelBuffer == null || _pixelBuffer.Length != requiredSize)
                {
                    _pixelBuffer = new byte[requiredSize];
                }

                byte* srcPtr = (byte*)dataBox.DataPointer;
                fixed (byte* dstPtr = _pixelBuffer)
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* srcLine = srcPtr + y * rowPitch;
                        byte* dstLine = dstPtr + y * width * bytesPerPixel;
                        int lineBytes = width * bytesPerPixel;


                        // TODO : Check for support before using the func
                        // and refactor this

                        if (Avx2.IsSupported)
                        {
                            Console.WriteLine("Using AVX2 for pixel copy");
                            Avx2Copy(srcLine, dstLine, lineBytes);
                        }
                        else if (Sse2.IsSupported)
                        {
                            Console.WriteLine("Using SSE2 for pixel copy");
                            Sse2Copy(srcLine, dstLine, lineBytes);
                        }
                        else
                        {
                            Console.WriteLine("Using default copy for pixel copy");
                            DefaultCopy(srcLine, dstLine, lineBytes);
                        }
                    }
                }

                return _pixelBuffer;
            }
            finally
            {
                context.Unmap(_stagingTexture, 0);
            }
        }

        private unsafe void Avx2Copy(byte* src, byte* dst, int length)
        {
            int offset = 0;
            int vectorSize = 32; // 256 bits -> 32 bytes
            for (; offset <= length - vectorSize; offset += vectorSize)
            {
                var vector = Avx2.LoadVector256(src + offset);
                Avx2.Store(dst + offset, vector);
            }
            // Remaining bytes
            for (; offset < length; offset++)
            {
                dst[offset] = src[offset];
            }
        }

        private unsafe void Sse2Copy(byte* src, byte* dst, int length)
        {
            int offset = 0;
            int vectorSize = 16; // 128 bits -> 16 bytes
            for (; offset <= length - vectorSize; offset += vectorSize)
            {
                var vector = Sse2.LoadVector128(src + offset);
                Sse2.Store(dst + offset, vector);
            }

            for (; offset < length; offset++)
            {
                dst[offset] = src[offset];
            }
        }

        private unsafe void DefaultCopy(byte* src, byte* dst, int length)
        {
            for (int i = 0; i < length; i++)
            {
                dst[i] = src[i];
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
