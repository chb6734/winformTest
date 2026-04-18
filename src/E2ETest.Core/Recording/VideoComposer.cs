using System;
using System.Diagnostics;
using System.IO;

namespace E2ETest.Core.Recording
{
    /// <summary>프레임 폴더 → MP4 합성. FFmpeg 필요. 없으면 경고 로그 남기고 실패로 표시.</summary>
    public static class VideoComposer
    {
        public static string ComposeMp4(string framesDir, string outputMp4, int fps = 6, string ffmpegPath = null)
        {
            if (!Directory.Exists(framesDir)) throw new DirectoryNotFoundException(framesDir);

            string ffmpeg = ffmpegPath ?? ResolveFfmpeg();
            if (string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg))
            {
                throw new FileNotFoundException("ffmpeg.exe not found. Place it in ./tools/ffmpeg.exe or pass ffmpegPath.");
            }

            string inputPattern = Path.Combine(framesDir, "frame_%06d.png");
            var psi = new ProcessStartInfo(ffmpeg,
                "-y -framerate " + fps + " -i \"" + inputPattern + "\" -c:v libx264 -pix_fmt yuv420p \"" + outputMp4 + "\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                    throw new Exception("ffmpeg failed (exit=" + p.ExitCode + "): " + stderr);
            }
            return outputMp4;
        }

        public static string ResolveFfmpeg()
        {
            // 우선순위: 현재 dir ./tools/ffmpeg.exe → 환경변수 FFMPEG → PATH
            string local = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools"), "ffmpeg.exe");
            if (File.Exists(local)) return local;
            string env = Environment.GetEnvironmentVariable("FFMPEG");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            return "ffmpeg"; // 의존: PATH에 있음
        }
    }
}
