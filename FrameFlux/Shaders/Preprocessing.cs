using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Reflection;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace FrameFlux.Shaders
{
    public sealed class Preprocessing : IDisposable
    {
        private readonly Device _device;
        private readonly ComputeShader _resizeShader;
        private readonly SamplerState _sampler;
        private readonly int _targetWidth;
        private readonly int _targetHeight;

        public Preprocessing(Device device, int targetWidth, int targetHeight)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _targetWidth = targetWidth;
            _targetHeight = targetHeight;
            _resizeShader = InitializeResizeShader();
            _sampler = InitializeSampler();
        }

        public Texture2D Resize(Texture2D inputTex)
        {
            var texDesc = new Texture2DDescription
            {
                Width = _targetWidth,
                Height = _targetHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };
            var resizedTex = new Texture2D(_device, texDesc);

            using var srv = new ShaderResourceView(_device, inputTex);
            using var uav = new UnorderedAccessView(_device, resizedTex);

            var context = _device.ImmediateContext;
            context.ComputeShader.Set(_resizeShader);
            context.ComputeShader.SetShaderResource(0, srv);
            context.ComputeShader.SetUnorderedAccessView(0, uav);
            context.ComputeShader.SetSampler(0, _sampler);

            var srcDesc = inputTex.Description;
            var cbufferData = new ResizeParams
            {
                srcWidth = srcDesc.Width,
                srcHeight = srcDesc.Height,
                dstWidth = _targetWidth,
                dstHeight = _targetHeight
            };
            using var cbuffer = new Buffer(_device, Utilities.SizeOf<ResizeParams>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            context.UpdateSubresource(ref cbufferData, cbuffer);
            context.ComputeShader.SetConstantBuffer(0, cbuffer);

            int threadGroupX = (_targetWidth + 15) / 16;
            int threadGroupY = (_targetHeight + 15) / 16;
            context.Dispatch(threadGroupX, threadGroupY, 1);

            context.Flush();

            context.ComputeShader.SetShaderResource(0, null);
            context.ComputeShader.SetUnorderedAccessView(0, null);
            context.ComputeShader.SetSampler(0, null);
            context.ComputeShader.SetConstantBuffer(0, null);

            return resizedTex;
        }

        private ComputeShader InitializeResizeShader()
        {
            try
            {
                string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "Preprocess.hlsl");
                var shaderCode = ShaderBytecode.CompileFromFile(shaderPath, "CSMain", "cs_5_0");
                return new ComputeShader(_device, shaderCode);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to compile Preprocess.hlsl: " + ex.Message, ex);
            }
        }

        private SamplerState InitializeSampler()
        {
            var samplerDesc = new SamplerStateDescription
            {
                Filter = Filter.MinMagLinearMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp
            };
            return new SamplerState(_device, samplerDesc);
        }

        public void Dispose()
        {
            _resizeShader?.Dispose();
            _sampler?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ResizeParams
        {
            public int srcWidth;
            public int srcHeight;
            public int dstWidth;
            public int dstHeight;
        }
    }
}