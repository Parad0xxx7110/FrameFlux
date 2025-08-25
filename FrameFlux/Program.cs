// ------------------------------------------------------------------------------
//  Program.cs  –  FrameFlux minimal runnable entry point
// ------------------------------------------------------------------------------
using FrameFlux.Core;
using FrameFlux.Helpers;
using Spectre.Console;
using System;
using System.Threading;

namespace FrameFlux
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                AnsiConsole.MarkupLine("[red]Shutting down…[/]");
            };

            using var dxgi = new DXGIDupWrapper();
            using var engine = new CaptureEngine(dxgi, width: 640, height: 480, size: 128);

            AnsiConsole.MarkupLine("[green]Initializing DXGI…[/]");
            AnsiConsole.MarkupLine("[green]Capture loop started.  Press Ctrl+C or any key to stop…[/]");

            engine.Start(debugBmp: false);

            // Attendre Ctrl+C ou une touche
            Console.ReadKey(intercept: true);

            AnsiConsole.MarkupLine("[yellow]Stopping…[/]");
            engine.Stop();
            AnsiConsole.MarkupLine("[green]Stopped.[/]");
        }
    }
}