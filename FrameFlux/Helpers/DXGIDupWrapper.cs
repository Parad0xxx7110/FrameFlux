namespace FrameFlux.Helpers
{

    // DXGI desktop duplication wrapper with global access to created device, factory ect...

    using SharpDX.Direct3D11;
    using SharpDX.DXGI;
    using System;

    using Device = SharpDX.Direct3D11.Device;

    public class DXGIDupWrapper : IDisposable
    {
        // D3D11 Device
        public Device Device { get; private set; }

        // DXGI Factory
        public Factory1 Factory { get; private set; }

        // DXGI Adapters
        public Adapter1[] Adapters { get; private set; }

        // DXGI Outputs
        public Output1? Output1 { get; private set; }

        // DXGI Duplicaiton Output
        public OutputDuplication? Duplication { get; private set; }

        private readonly int _adapterIndex;
        private readonly int _outputIndex;

        public DXGIDupWrapper(int adapterIndex = 0, int outputIndex = 0)
        {
            _adapterIndex = adapterIndex;
            _outputIndex = outputIndex;

            Factory = new Factory1();
            Adapters = Factory.Adapters1;

            if (_adapterIndex >= Adapters.Length)
                throw new ArgumentOutOfRangeException(nameof(adapterIndex));

            var adapter = Adapters[_adapterIndex];
            Device = new Device(adapter, DeviceCreationFlags.BgraSupport);


            var output = adapter.GetOutput(_outputIndex);
            Output1 = output.QueryInterface<Output1>();
            Duplication = Output1.DuplicateOutput(Device);
        }

        public void Dispose()
        {
            Duplication?.Dispose();
            Output1?.Dispose();
            Device?.Dispose();
            Factory?.Dispose();
        }
    }

}
