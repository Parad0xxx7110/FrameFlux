using FrameFlux.Core;
using Spectre.Console;

class Program
{
    static async Task Main()
    {
        await using var capturer = new CaptureEngine(adapterIndex: 0, outputIndex: 0, maxFps: 60);
        var cts = new CancellationTokenSource();

        
        var captureThread = new Thread(() =>
        {
            try
            {
              
                capturer.StartCaptureAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Capture annulée.");
            }
        })
        {
            IsBackground = true
        };
        captureThread.Start();

       
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(120));
            cts.Cancel();
        });

        Console.WriteLine("Appuie sur [Esc] pour arrêter la capture...");

        
        while (!cts.IsCancellationRequested)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                cts.Cancel();
            await Task.Delay(50);

        }

        captureThread.Join();
        Console.WriteLine("Capture terminée.");
    }

}
