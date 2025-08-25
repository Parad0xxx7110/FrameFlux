// ------------------------------------------------------------------------------
//  PreciseStats.cs   –  Fixed single-row, live-updated, no repetition
// ------------------------------------------------------------------------------
using Spectre.Console;
using Spectre.Console.Rendering;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FrameFlux.Core
{
    public sealed class PreciseStats : IDisposable
    {
        private const int RingBits = 10;           // 1024 samples
        private const int RingSize = 1 << RingBits;
        private const int RingMask = RingSize - 1;

        private readonly double[] _durations = new double[RingSize];
        private int _write;          // single producer
        private int _count;          // atomic

        private readonly Thread _ui;
        private volatile bool _running = true;

        private readonly Func<double> _getUsage;

        public PreciseStats(Func<double> bufferUsageProvider)
        {
            _getUsage = bufferUsageProvider ?? throw new ArgumentNullException(nameof(bufferUsageProvider));
            _ui = new Thread(UiLoop) { IsBackground = true, Name = "Stats-UI" };
            _ui.Start();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordHardwareDelta(long qpcDelta)
        {
            if (qpcDelta <= 0) return;
            double ms = qpcDelta * 1000.0 / Stopwatch.Frequency;
            int idx = Interlocked.Increment(ref _write) & RingMask;
            _durations[idx] = ms;

            int c = _count;
            if (c < RingSize)
                Interlocked.CompareExchange(ref _count, c + 1, c);
        }

        private void UiLoop()
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("FPS");
            table.AddColumn("Avg ms");
            table.AddColumn("Min ms");
            table.AddColumn("Max ms");
            table.AddColumn("Last ms");

            // single fixed row
            table.AddRow("—", "—", "—", "—", "—");

            var bufferBar = new BarChart()
                .Width(40)
                .Label("Buffer");

            var root = new Panel(new Rows(table, bufferBar))
                .Border(BoxBorder.Rounded)
                .Header("FrameFlux Live");

            AnsiConsole.Live(root).Start(ctx =>
            {
                while (_running)
                {
                    Render(root, table, bufferBar);
                    ctx.Refresh();
                    Thread.Sleep(100);
                }
            });
        }

        private void Render(IRenderable root, Table table, BarChart bar)
        {
            int cnt = Math.Min(_count, RingSize);
            if (cnt == 0)
            {
                table.UpdateCell(0, 0, "—");
                table.UpdateCell(0, 1, "—");
                table.UpdateCell(0, 2, "—");
                table.UpdateCell(0, 3, "—");
                table.UpdateCell(0, 4, "—");
                return;
            }

            double sum = 0, min = double.MaxValue, max = double.MinValue;
            int r = _write;
            for (int i = 0; i < cnt; i++)
            {
                double v = _durations[(r - i) & RingMask];
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            double avg = sum / cnt;
            double fps = avg > 0 ? 1000.0 / avg : 0;
            double last = _durations[r & RingMask];

            table.UpdateCell(0, 0, fps.ToString("F1"));
            table.UpdateCell(0, 1, avg.ToString("F2"));
            table.UpdateCell(0, 2, min.ToString("F2"));
            table.UpdateCell(0, 3, max.ToString("F2"));
            table.UpdateCell(0, 4, last.ToString("F2"));

            // Barre – évite BarChart.AddItem(0)
            bar.Data.Clear();
            bar.AddItem("used", Math.Max(0.1, _getUsage()) * 100, _getUsage() switch
            {
                < 0.5 => Color.Green,
                < 0.85 => Color.Yellow,
                _ => Color.Red
            });
        }

        public void Dispose()
        {
            _running = false;
            _ui.Join();
        }
    }
}