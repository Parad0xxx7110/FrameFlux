using FrameFlux.Helpers;
using SharpDX.Direct3D11;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;
using FrameFlux.Output.Local;

namespace FrameFlux.Core
{
    public sealed class CaptureEngine : IDisposable
    {
        private readonly DXGIDupWrapper _dxgiWrapper;
        private bool _running;

        public CaptureEngine(DXGIDupWrapper dxgiWrapper)
        {
            _dxgiWrapper = dxgiWrapper ?? throw new ArgumentNullException(nameof(dxgiWrapper));
        }

        public Texture2D? CaptureFrame()
        {
            var result = _dxgiWrapper.Duplication.TryAcquireNextFrame(200, out _, out var capturedFrame);
            if (!result.Success || capturedFrame == null) return null;

            return capturedFrame.QueryInterface<Texture2D>();
        }

        public Bitmap? CaptureFrameToBitmap(Texture2D tex)
        {
            if (tex == null) return null;
            return LocalImageHelper.TextureToBitmap(_dxgiWrapper, tex);
        }

        public void CaptureLoop(int targetFPS, Action<Texture2D> onFrameCaptured, bool debugBmp = false)
        {
            _running = true;
            var frameTime = TimeSpan.FromMilliseconds(1000.0 / targetFPS);

            while (_running)
            {
                var sw = Stopwatch.StartNew();

                var tex = CaptureFrame();
                if (tex != null)
                {
                    onFrameCaptured?.Invoke(tex);

                    // Debug
                    if (debugBmp)
                    {
                        using var bmp = CaptureFrameToBitmap(tex);
                        bmp?.Save("capture_debug.bmp", ImageFormat.Bmp);
                    }

                    _dxgiWrapper.Duplication.ReleaseFrame();
                    tex.Dispose();
                }

                sw.Stop();
                var delay = frameTime - sw.Elapsed;
                if (delay > TimeSpan.Zero)
                    Thread.Sleep(delay);
            }
        }

        public void StopLoop()
        {
            _running = false;
        }

        public void Dispose()
        {
            _dxgiWrapper?.Dispose();
        }
    }
}
