using FrameFlux.Core;
using FrameFlux.Output.Memory;
using Spectre.Console;

class Program
{
    static async Task Main()
    {
        const int MAX_WIDTH = 1920;
        const int MAX_HEIGHT = 1080;
        const int BYTES_PER_PIXEL = 4;
        const int BUFFER_SIZE = MAX_WIDTH * MAX_HEIGHT * BYTES_PER_PIXEL;

        await using var capturer = new CaptureEngine(adapterIndex: 0, outputIndex: 0, maxFps: 0);
        using var sharedMem = new SharedMemory("FrameBuffer", "FrameReady", BUFFER_SIZE);

        var cts = new CancellationTokenSource();

        // Consomme les frames et les pousse dans la shared memory
        var captureTask = Task.Run(async () =>
        {
            await foreach (var frame in capturer.StartCaptureAsync(sharedMem, cts.Token))
            {
                var pixels = frame.FastGetFrameBytes();
                if (pixels != null)
                    sharedMem.WriteFrameData(pixels);
            }
        });

        // Arrêt avec Esc ou après 120 s
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

        await captureTask;
        AnsiConsole.MarkupLine("[green]Capture terminée.[/]");
    }
}