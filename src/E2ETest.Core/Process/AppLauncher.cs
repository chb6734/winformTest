using System;
using System.Diagnostics;
using System.IO;

namespace E2ETest.Core.ProcessMgr
{
    /// <summary>대상 앱을 실행·모니터링·종료.</summary>
    public sealed class AppLauncher : IDisposable
    {
        public int ProcessId { get; private set; }
        public System.Diagnostics.Process Process { get; private set; }
        public string ExePath { get; private set; }

        public AppLauncher(string exePath, string args = null, string workingDir = null)
        {
            if (string.IsNullOrEmpty(exePath)) throw new ArgumentException("exePath is required");
            ExePath = exePath;
            var psi = new ProcessStartInfo(exePath, args ?? "")
            {
                UseShellExecute = false,
                WorkingDirectory = string.IsNullOrEmpty(workingDir) ? Path.GetDirectoryName(Path.GetFullPath(exePath)) : workingDir
            };
            Process = System.Diagnostics.Process.Start(psi);
            if (Process == null) throw new InvalidOperationException("failed to start process: " + exePath);
            ProcessId = Process.Id;

#if NET8_PLUS
            try { Process.WaitForInputIdle(5000); } catch { /* some apps don't idle */ }
#endif
        }

        public bool HasExited { get { return Process == null || Process.HasExited; } }

        public void Kill()
        {
            try
            {
                if (Process != null && !Process.HasExited) Process.Kill();
            }
            catch { }
        }

        public void Dispose()
        {
            Kill();
            if (Process != null) Process.Dispose();
        }
    }
}
