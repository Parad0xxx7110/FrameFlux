    using SharpDX.Direct3D11;
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;

    namespace FrameFlux.Memory
    {
        public sealed class RingBuffer : IDisposable
        {
            private readonly Texture2D[] _buffer;
            private readonly long[] _fenceValues;
            private long _writeIndex;
            private long _readIndex;
            private long _lastFenceValue;
            private readonly Fence _fence;
            private readonly Device5 _device5;
            private readonly DeviceContext4 _context4;
            private readonly IntPtr _eventHandle;
            private int _disposed; // 0 = alive, 1 = Elvis

        public double UsageRatio =>
        Math.Min(1.0, (double)Math.Max(0, Volatile.Read(ref _writeIndex) - Volatile.Read(ref _readIndex)) / _buffer.Length);

        public RingBuffer(Device device, int size)
            {
                if (device == null) throw new ArgumentNullException(nameof(device));
                if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

                _device5 = device.QueryInterfaceOrNull<Device5>()
                    ?? throw new NotSupportedException("Device must support D3D11.4");

                _context4 = device.ImmediateContext.QueryInterfaceOrNull<DeviceContext4>()
                    ?? throw new NotSupportedException("DeviceContext must support D3D11.4.");

                _buffer = new Texture2D[size];
                _fenceValues = new long[size];
                _fence = new Fence(_device5, 0, FenceFlags.None);
                _eventHandle = CreateEvent(IntPtr.Zero, false, false, null);
                if (_eventHandle == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to create event handle");
            }

            public void Push(Texture2D texture)
            {
                if (texture == null) throw new ArgumentNullException(nameof(texture));
                if (Volatile.Read(ref _disposed) != 0)
                    throw new ObjectDisposedException(nameof(RingBuffer));

            
                var index = (int)(Volatile.Read(ref _writeIndex) % _buffer.Length);
                Interlocked.Increment(ref _writeIndex);
             //   Console.WriteLine($"Push: index={index}, writeIndex={_writeIndex}, texture={(texture != null ? "valid" : "null")}");

                var prevTex = Interlocked.Exchange(ref _buffer[index], texture);
           //     Console.WriteLine($"Push: prevTex={(prevTex != null ? "valid" : "null")}");
                prevTex?.Dispose();

                var fenceValue = Interlocked.Increment(ref _lastFenceValue);
                Volatile.Write(ref _fenceValues[index], fenceValue);
           //     Console.WriteLine($"Push: fenceValue={fenceValue}, fenceValues[{index}]={_fenceValues[index]}");

                _context4.Signal(_fence, fenceValue);
            }

            public bool TryPop(out Texture2D texture, out long fenceValue)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    throw new ObjectDisposedException(nameof(RingBuffer));

                var currentReadIndex = Volatile.Read(ref _readIndex);
                var writeIndexSnapshot = Volatile.Read(ref _writeIndex);
          //      Console.WriteLine($"TryPop: readIndex={currentReadIndex}, writeIndex={writeIndexSnapshot}");

                if (currentReadIndex >= writeIndexSnapshot)
                {
                    texture = null;
                    fenceValue = 0;
                   // Console.WriteLine("TryPop: Buffer vide (readIndex >= writeIndex)");
                    return false;
                }

                var index = (int)(currentReadIndex % _buffer.Length);
                texture = Interlocked.Exchange(ref _buffer[index], null);
                fenceValue = _fenceValues[index];
         //       Console.WriteLine($"TryPop: index={index}, texture={(texture != null ? "valid" : "null")}, fenceValue={fenceValue}");

                Volatile.Write(ref _readIndex, currentReadIndex + 1);
                return texture != null;
            }

            public void WaitForFence(long fenceValue)
            {
                if (Volatile.Read(ref _disposed) != 0) return;

                const int maxAttempts = 10; // 10ms is a lot in this context bruh...
                int attempts = 0;

                while (_fence.CompletedValue < fenceValue && attempts < maxAttempts)
                {
                    _fence.SetEventOnCompletion(fenceValue, _eventHandle);
                    uint result = WaitForSingleObject(_eventHandle, 1);
                    if (result == 0x00000000) // WAIT_OBJECT_0
                        break;
                    if (result == 0xFFFFFFFF) // WAIT_FAILED
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    attempts++;
                }

            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

                for (int i = 0; i < _buffer.Length; i++)
                {
                    _buffer[i]?.Dispose();
                    _buffer[i] = null;
                }

                _fence?.Dispose();
                if (_eventHandle != IntPtr.Zero)
                    CloseHandle(_eventHandle);
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        }
    }