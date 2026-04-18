using System;

namespace E2ETest.Core.Recording
{
    public interface IRecorder : IDisposable
    {
        void Start();
        void CaptureFrame(string name);
        void Stop();
        string LastFrameBase64 { get; }
        /// <summary>녹화 중인 대상 영역. Runner가 윈도우 바운드로 설정.</summary>
        void SetRegion(int x, int y, int width, int height);
        string FramesDirectory { get; }
    }
}
