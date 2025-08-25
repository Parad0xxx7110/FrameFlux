// ------------------------------------------------------------------------------
//  CaptureEngine.cs  –  full, clean, threaded version
// ------------------------------------------------------------------------------
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using FrameFlux.Helpers;
using FrameFlux.Memory;
using FrameFlux.Shaders;
using SharpDX.Direct3D11;
using Spectre.Console;

namespace FrameFlux.Core
{
    public sealed class CaptureEngine : IDisposable
    {
        private readonly DXGIDupWrapper  _dxgi;
        private readonly Preprocessing   _pre;
        private readonly RingBuffer      _ring;
        private readonly Thread          _infer;

        private volatile bool _running;
        private PreciseStats    _stats;

        public CaptureEngine(DXGIDupWrapper dxgi,
                             int width  = 640,
                             int height = 480,
                             int size   = 60)
        {
            _dxgi  = dxgi  ?? throw new ArgumentNullException(nameof(dxgi));
            _pre   = new Preprocessing(dxgi.Device, width, height);
            _ring  = new RingBuffer(dxgi.Device, size);
            _infer = new Thread(InferenceLoop) { IsBackground = true, Name = "Inference" };
        }

        // ---------------------------------------------------------------------
        public void Start(bool debugBmp = false)
        {
            if (_running) return;
            _running = true;
            _stats   = new PreciseStats(() => _ring.UsageRatio);

            new Thread(() => CaptureLoop(debugBmp)) { IsBackground = true, Name = "Capture" }.Start();
            _infer.Start();
        }

        public void Stop()
        {
            _running = false;
            _infer.Join();
        }

        public void Dispose()
        {
            Stop();
            _ring?.Dispose();
            _pre?.Dispose();
            _dxgi?.Dispose();
            _stats?.Dispose();
        }

        // ---------------------------------------------------------------------
        private void CaptureLoop(bool debugBmp)
        {
            long lastQpc = 0;

            while (_running)
            {
                var hr = _dxgi.Duplication.TryAcquireNextFrame(16, out var info, out var desktopRes);
                if (!hr.Success) continue;

                try
                {
                    if (info.LastPresentTime == 0 || desktopRes == null) continue;

                    long delta = lastQpc == 0 ? 0 : info.LastPresentTime - lastQpc;
                    if (delta > 0) _stats.RecordHardwareDelta(delta);
                    lastQpc = info.LastPresentTime;

                    using var desktopTex = desktopRes.QueryInterface<Texture2D>();
                    Texture2D resizedTex = _pre.Resize(desktopTex);
                    _dxgi.Device.ImmediateContext.Flush();
                    _ring.Push(resizedTex);

                    if (debugBmp) DumpBitmap(resizedTex);
                }
                finally
                {
                    _dxgi.Duplication.ReleaseFrame();
                }
            }
        }

        private void InferenceLoop()
        {
            while (_running)
            {
                if (_ring.TryPop(out var tex, out var fence))
                {
                    _ring.WaitForFence(fence);
                    tex?.Dispose();
                }
                else Thread.Sleep(200);
            }
        }

        private static void DumpBitmap(Texture2D tex)
        {
            Task.Run(() =>
            {
                using var bmp = Output.Local.LocalImageHelper.TextureToBitmap(null, tex);
                bmp?.Save($"debug_{DateTime.Now:HHmmss_fff}.bmp", ImageFormat.Bmp);
            });
        }
    }
}