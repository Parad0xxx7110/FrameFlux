using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

public class SharedMemory : IDisposable
{

    // This class provides a shared memory buffer for inter-process communication,
    // inference is done in another process, and this class is used to write frames to the shared memory.

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly Semaphore _semaphore;
    private readonly int _bufferSize;


    private int _frameCount;
    private int _frameDroppedCount;

    // For stats
    public int FrameCount => _frameCount;
    public int FrameDroppedCount => _frameDroppedCount;



    public SharedMemory(string mapName, string semaphoreName, int bufferSize)
    {
        _bufferSize = bufferSize;
        _mmf = MemoryMappedFile.CreateOrOpen(mapName, bufferSize, MemoryMappedFileAccess.ReadWrite);
        _accessor = _mmf.CreateViewAccessor();
        _semaphore = new Semaphore(0, 1, semaphoreName);
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
