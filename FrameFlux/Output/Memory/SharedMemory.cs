using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace FrameFlux.Output.Memory
{


    // This is what peak performance looks like, no allocations, no locks, no garbage, no gc

    public class SharedMemory : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly EventWaitHandle _dataReadyEvent;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly int _bufferSize;
        private unsafe byte* _mappedPtr;
        private SpinLock _spinLock = new();

        private int _frameCount;
        private int _frameDroppedCount;

        public int FrameCount => _frameCount;
        public int FrameDroppedCount => _frameDroppedCount;
        public int BufferSize => _bufferSize;

        /// <summary>
        /// Initializes a new instance of the SharedMemory class.
        /// </summary>
        /// <param name="mapName">The name of the memory-mapped file.</param>
        /// <param name="eventName">The name of the event for synchronization and IPC.</param>
        /// <param name="bufferSize">The size of the shared buffer.</param>
        /// <exception cref="ArgumentException">Thrown if mapName or eventName is null/empty, or bufferSize is non-positive.</exception>
        public SharedMemory(string mapName, string eventName, int bufferSize)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                throw new ArgumentException("Map name cannot be null or empty.", nameof(mapName));
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name cannot be null or empty.", nameof(eventName));
            if (bufferSize <= 0)
                throw new ArgumentException("Buffer size must be positive.", nameof(bufferSize));

            _bufferSize = bufferSize;
            _mmf = MemoryMappedFile.CreateOrOpen(mapName, bufferSize, MemoryMappedFileAccess.ReadWrite);
            _accessor = _mmf.CreateViewAccessor();
            _dataReadyEvent = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);

          
            unsafe
            {
                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _mappedPtr);
            }
        }

        /// <summary>
        /// Writes frame data to the shared memory buffer and signals the event if possible.
        /// </summary>
        /// <param name="data">The frame data to write.</param>
        /// <exception cref="ArgumentException">Thrown if the data size exceeds the buffer size.</exception>
        public void WriteFrameData(ReadOnlySpan<byte> data)
        {
            if (data.Length > _bufferSize)
                throw new ArgumentException("Data size exceeds buffer size.");

            try
            {
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    unsafe
                    {
                        data.CopyTo(new Span<byte>(_mappedPtr, _bufferSize));
                    }
                   // _accessor.Flush();
                }
                finally
                {
                    if (lockTaken) _spinLock.Exit();
                }

                Interlocked.Increment(ref _frameCount);

                if (!_dataReadyEvent.Set())
                {
                    Interlocked.Increment(ref _frameDroppedCount);
                }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _frameDroppedCount);
            }
        }

        public void Dispose()
        {
            unsafe
            {
                if (_mappedPtr != null)
                {
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    _mappedPtr = null;
                }
            }
            _accessor?.Dispose();
            _mmf?.Dispose();
            _dataReadyEvent?.Dispose();
        }
    }
}