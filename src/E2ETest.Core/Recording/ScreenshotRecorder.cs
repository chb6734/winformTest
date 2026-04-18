#if NET8_PLUS
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace E2ETest.Core.Recording
{
    /// <summary>GDI+ 기반 스크린샷 레코더. 백그라운드 타이머로 일정 FPS 캡처 + 이벤트 트리거.</summary>
    public sealed class ScreenshotRecorder : IRecorder
    {
        private readonly string _framesDir;
        private Rectangle _region = Rectangle.Empty;
        private readonly int _fps;
        private System.Threading.Timer _timer;
        private int _frameIndex;
        private readonly object _lock = new object();
        private string _lastFrameBase64;
        private bool _running;

        public string LastFrameBase64 { get { return _lastFrameBase64; } }
        public string FramesDirectory { get { return _framesDir; } }

        public ScreenshotRecorder(string outputDir, int fps = 6)
        {
            _framesDir = outputDir;
            _fps = fps;
            Directory.CreateDirectory(_framesDir);
        }

        public void SetRegion(int x, int y, int width, int height)
        {
            _region = new Rectangle(x, y, width, height);
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            int intervalMs = 1000 / Math.Max(1, _fps);
            _timer = new System.Threading.Timer(_ => TryCapture(null), null, 0, intervalMs);
        }

        public void CaptureFrame(string name)
        {
            TryCapture(name);
        }

        private void TryCapture(string explicitName)
        {
            try
            {
                lock (_lock)
                {
                    var rect = _region.IsEmpty ? System.Windows.Forms.Screen.PrimaryScreen.Bounds : _region;
                    if (rect.Width <= 0 || rect.Height <= 0) return;

                    using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(rect.X, rect.Y, 0, 0, new Size(rect.Width, rect.Height), CopyPixelOperation.SourceCopy);
                        }
                        string filename = explicitName != null
                            ? Path.Combine(_framesDir, SafeFilename(explicitName) + ".png")
                            : Path.Combine(_framesDir, string.Format("frame_{0:D6}.png", _frameIndex++));
                        bmp.Save(filename, ImageFormat.Png);

                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Jpeg);
                            _lastFrameBase64 = Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
            catch { /* swallow - 캡처 실패로 테스트가 죽으면 안 됨 */ }
        }

        private static string SafeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        public void Stop()
        {
            _running = false;
            if (_timer != null) { _timer.Dispose(); _timer = null; }
        }

        public void Dispose() { Stop(); }
    }
}
#endif
