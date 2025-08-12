using FrameFlux.Core;
using Spectre.Console;

class Program
{
    static async Task Main()
    {

        var capturer = new DXGIDuplication(adapterIndex: 0, outputIndex: 0, targetFps: 60);

        var cts = new CancellationTokenSource();


        var captureTask = capturer.StartCaptureAsync(cts.Token);

        
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(120));
            cts.Cancel();
        });

        try
        {
            await captureTask;
        }
        catch (OperationCanceledException)
        {
            // Ok, we expected this
            AnsiConsole.MarkupLine("[red]Capture canceled by user.[/]");
        }
        finally
        {
            await capturer.DisposeAsync();
            AnsiConsole.MarkupLine("[yellow]Capture over.[/]");
        }
    }
}
