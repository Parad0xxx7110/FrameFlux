using System;
using System.IO.MemoryMappedFiles;
using System.Threading;


namespace FrameFlux.Output.Memory
{
    // Shared memory buffer for inter-process communication
    // This class is used to write frames to the shared memory for inference in another process.
    // It uses a semaphore to signal when a frame is ready for processing.
    // The buffer size is defined at creation time, and it can be used to write byte arrays representing frames.
    //
    // Usage:
    // 1. Create an instance of SharedMemory with a unique map name and semaphore name.
    // 2. Use WriteFrameData(byte[] data) to write frame data to the shared memory.
    // 3. Dispose the instance when done to release resources.


    public class SharedMemory : IDisposable
    {

        // This class provides a shared memory buffer for inter-process communication,
        // inference is done in another process, and this class is used to write frames to the shared memory.

        private readonly MemoryMappedFile _mmf;
        private readonly Semaphore _semaphore;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly int _bufferSize;


        private int _frameCount;
        private int _frameDroppedCount;

        // For stats
        public int FrameCount => _frameCount;
        public int FrameDroppedCount => _frameDroppedCount;


        // Will need both mapname and the semaphore name to be used by the server process.
        public SharedMemory(string mapName, string semaphoreName, int bufferSize)
        {
            _bufferSize = bufferSize;
            _mmf = MemoryMappedFile.CreateOrOpen(mapName, bufferSize, MemoryMappedFileAccess.Write);
            _accessor = _mmf.CreateViewAccessor();
            _semaphore = new Semaphore(0, 1, semaphoreName); // We need a 1/1 semaphore to signal frame readiness
                                                             // since we need to consume frames one by one for the
                                                             // inference process.
        }



        public void WriteFrameData(byte[] data)
        {
            if (data.Length > _bufferSize)
                throw new ArgumentException("Data size exceeds buffer size.");

            _accessor.WriteArray(0, data, 0, data.Length);
            _frameCount++;

            try
            {
                // Frame ready
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                Console.WriteLine("Semaphore already full, frame not processed in time.");
                _frameDroppedCount++; // Higly unlikely to happen, but just in case...
            }
        }

        public void Dispose()
        {
            _accessor.Dispose();
            _mmf.Dispose();
            _semaphore.Dispose();
        }
    }
}