using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace FrameFlux.Helpers
{
    internal class D3DDevice
    {
        public ID3D11Device Device { get; private set; }
        public ID3D11DeviceContext Context { get; private set; }

        public D3DDevice(bool enableDebug = false)
        {
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport; // For desktop dup support
            if (enableDebug)
                flags |= DeviceCreationFlags.Debug;

            FeatureLevel[] featureLevels = new FeatureLevel[]
            {
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0
            };

            var result = D3D11.D3D11CreateDevice(
                adapter: null, // Default GPU
                DriverType.Hardware,
                flags,
                featureLevels,
                out ID3D11Device device,
                out ID3D11DeviceContext context
            );

            if (result.Failure)
                throw new InvalidOperationException($"Failed to create D3D11 device: {result}");

            Device = device;
            Context = context;
        }

        public void Dispose()
        {
            Context?.Dispose();
            Device?.Dispose();
        }
    }
}
