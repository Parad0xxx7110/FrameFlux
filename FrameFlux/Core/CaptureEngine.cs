using Spectre.Console;
using System.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

using static Vortice.DXGI.ResultCode;

namespace FrameFlux.Core
{

    // this class is subject to a lot of changes for testing and performance tuning
    // the api should be stable but the frame acquisition logic may change frequently
    //


    public sealed class CaptureEngine : IAsyncDisposable
    {
        private readonly uint _adapterIndex;
        private readonly uint _outputIndex;
        private readonly TimeSpan? _maxFrameInterval;

        private IDXGIOutputDuplication? _duplication;
        private ID3D11Device? _device;
        private IDXGIOutput1? _output;


        // Assuming we use the primary output by default if nothing is specified
        public CaptureEngine(uint adapterIndex = 0, uint outputIndex = 0, int maxFps = 0)
        {
            _adapterIndex = adapterIndex;
            _outputIndex = outputIndex;
            _maxFrameInterval = maxFps > 0 ? TimeSpan.FromSeconds(1.0 / maxFps) : null;
        }

        public async Task StartCaptureAsync(CancellationToken token = default)
        {
            InitDesktopDuplication();
            if (_duplication is null)
                throw new InvalidOperationException("Duplication not initialized.");

            int maxFps = _maxFrameInterval.HasValue ? (int)Math.Round(1.0 / _maxFrameInterval.Value.TotalSeconds) : 0;
            long ticksPerFrame = maxFps > 0 ? Stopwatch.Frequency / maxFps : 0;

            var swFps = Stopwatch.StartNew();
            long nextFrameTicks = Stopwatch.GetTimestamp();
            int frames = 0;
            int droppedFrames = 0;
            long lastPts = 0;
            int retryCount = 0;
            const int maxRetries = 5;

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[teal]Metric[/]")
                .AddColumn("[teal]Value[/]")
                .AddRow("Max FPS", maxFps > 0 ? maxFps.ToString() : "Unlimited")
                .AddRow("Current FPS", "0")
                .AddRow("Dropped Frames", "0")
                .AddRow("Status", "Running");

            await AnsiConsole.Live(table)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Top)
                .StartAsync(async ctx =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            using var frame = AcquireFrame(maxFps);
                            bool hasNewContent = frame != null && frame.Info.LastPresentTime != lastPts;

                            if (hasNewContent)
                            {
                                lastPts = frame!.Info.LastPresentTime;
                                frames++;
                            }
                            else if (maxFps > 0)
                            {
                                droppedFrames++;
                            }

                            retryCount = 0;

                            if (maxFps > 0)
                            {
                                nextFrameTicks += ticksPerFrame;
                                long now = Stopwatch.GetTimestamp();
                                long waitTicks = nextFrameTicks - now;

                                if (waitTicks > 0)
                                {
                                    int ms = (int)(waitTicks * 1000 / Stopwatch.Frequency);
                                    if (ms > 1)
                                        await Task.Delay(ms - 1, token);

                                    while (Stopwatch.GetTimestamp() < nextFrameTicks)
                                        Thread.SpinWait(5);
                                }
                                else
                                {
                                    nextFrameTicks = now;
                                }
                            }
                            else
                            {
                                if (!hasNewContent)
                                    Thread.SpinWait(5);
                            }

                            if (swFps.ElapsedMilliseconds >= 1000)
                            {
                                table.UpdateCell(1, 1, frames.ToString());
                                table.UpdateCell(2, 1, droppedFrames.ToString());
                                table.UpdateCell(3, 1, "Running");
                                ctx.Refresh();
                                frames = 0;
                                droppedFrames = 0;
                                swFps.Restart();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (InvalidOperationException ex) when (retryCount < maxRetries)
                        {
                            retryCount++;
                            table.UpdateCell(3, 1, $"Retry {retryCount}/{maxRetries}: {ex.Message}");
                            ctx.Refresh();
                            await Task.Delay(10 * retryCount, token);
                        }
                        catch (Exception ex)
                        {
                            table.UpdateCell(3, 1, $"Fatal: {ex.Message}");
                            ctx.Refresh();
                            break;
                        }
                    }
                });
        }


        // Initialize the D3D11 device and output duplication
        private void InitDesktopDuplication()
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            var result = factory.EnumAdapters(_adapterIndex, out var adapter);
            if (result.Failure || adapter is null)
                throw new InvalidOperationException($"No adapter found at index {_adapterIndex} (HRESULT {result.Code})");

            result = adapter.EnumOutputs(_outputIndex, out var output);
            if (result.Failure || output is null)
                throw new InvalidOperationException($"No output found at index {_outputIndex} (HRESULT {result.Code})");

            _output = output.QueryInterface<IDXGIOutput1>()
                      ?? throw new NotSupportedException("Output duplication not supported.");

            result = D3D11.D3D11CreateDevice(
                adapter,
                DriverType.Unknown,
                DeviceCreationFlags.None,
                [FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
                out _device);

            if (result.Failure || _device is null)
                throw new InvalidOperationException($"D3D11CreateDevice failed (HRESULT {result.Code})");

            _duplication = _output.DuplicateOutput(_device);
            if (_duplication is null)
                throw new InvalidOperationException("Failed to create output duplication.");
        }


        // Acquire the next frame from the duplication interface
        private Frame? AcquireFrame(int targetFps)
        {
            if (_duplication is null)
                throw new InvalidOperationException("Duplication not initialized.");

            int timeoutMs = targetFps == 0
                ? 0
                : Math.Max(0, (int)Math.Round(1000.0 / targetFps) - 1);

            var hr = _duplication.AcquireNextFrame((uint)timeoutMs, out var info, out var res);

            if (hr.Success)
                return new Frame(res, info, _duplication);

            if (hr.Code == WaitTimeout)
                return null;

            throw new InvalidOperationException($"AcquireNextFrame failed with HRESULT 0x{hr.Code:X}");
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                _duplication?.Dispose();
                _output?.Dispose();
                _device?.Dispose();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Dispose error:[/] {ex.Message}");
            }

            return ValueTask.CompletedTask;
        }
    }
}
