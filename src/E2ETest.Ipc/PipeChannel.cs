using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using E2ETest.Core.Events;

namespace E2ETest.Ipc
{
    /// <summary>
    /// Runner와 Dashboard 사이 Named Pipe 브로드캐스트 채널. 줄 단위 JSON.
    /// Dashboard가 서버, Runner가 클라이언트.
    /// </summary>
    public sealed class PipeServer : IDisposable
    {
        private readonly string _pipeName;
        public event Action<RunnerEvent> OnEvent;
        public event Action OnClientConnected;
        public event Action OnClientDisconnected;
        private volatile bool _disposed;

        public PipeServer(string pipeName = "e2etest-pipe") { _pipeName = pipeName; }

        /// <summary>
        /// 무한 accept 루프. 클라이언트(CLI) 연결 → 이벤트 스트림 수신 → 연결 끊김 → 다음 연결 대기.
        /// Dashboard는 여러 Re-run을 연속으로 받을 수 있음.
        /// </summary>
        public void StartAndWait(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_disposed)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    // 비동기 wait (취소 지원)
                    var ar = pipe.BeginWaitForConnection(null, null);
                    while (!ar.IsCompleted)
                    {
                        if (ct.WaitHandle.WaitOne(200)) { try { pipe.Dispose(); } catch { } return; }
                    }
                    pipe.EndWaitForConnection(ar);

                    if (OnClientConnected != null) OnClientConnected();

                    using (var reader = new StreamReader(pipe))
                    {
                        string line;
                        while (!ct.IsCancellationRequested && (line = reader.ReadLine()) != null)
                        {
                            try
                            {
                                var evt = JsonSerializer.Deserialize<RunnerEvent>(line);
                                if (evt != null && OnEvent != null) OnEvent(evt);
                            }
                            catch { /* bad line — skip */ }
                        }
                    }

                    if (OnClientDisconnected != null) OnClientDisconnected();
                }
                catch
                {
                    // 에러 시 잠시 대기 후 재시도
                    if (ct.WaitHandle.WaitOne(500)) return;
                }
                finally
                {
                    try { if (pipe != null) pipe.Dispose(); } catch { }
                }
            }
        }

        public void Dispose() { _disposed = true; }
    }

    public sealed class PipeClient : IDisposable
    {
        private readonly string _pipeName;
        private NamedPipeClientStream _pipe;
        private StreamWriter _writer;
        private readonly object _lock = new object();
        public bool Connected { get; private set; }

        public PipeClient(string pipeName = "e2etest-pipe") { _pipeName = pipeName; }

        public bool TryConnect(int timeoutMs = 2000)
        {
            try
            {
                _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                _pipe.Connect(timeoutMs);
                _writer = new StreamWriter(_pipe) { AutoFlush = true };
                Connected = true;
                return true;
            }
            catch { Connected = false; return false; }
        }

        public void Send(RunnerEvent evt)
        {
            if (!Connected) return;
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(evt);
                    _writer.WriteLine(json);
                }
                catch { Connected = false; }
            }
        }

        public void Dispose()
        {
            try { if (_writer != null) _writer.Dispose(); } catch { }
            try { if (_pipe != null) _pipe.Dispose(); } catch { }
        }
    }
}
