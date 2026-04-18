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
        private NamedPipeServerStream _pipe;
        private StreamReader _reader;
        public event Action<RunnerEvent> OnEvent;

        public PipeServer(string pipeName = "e2etest-pipe") { _pipeName = pipeName; }

        public void StartAndWait(CancellationToken ct)
        {
            _pipe = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _pipe.WaitForConnection();
            _reader = new StreamReader(_pipe);
            Task.Run(() => ReadLoop(ct), ct);
        }

        private void ReadLoop(CancellationToken ct)
        {
            try
            {
                string line;
                while (!ct.IsCancellationRequested && (line = _reader.ReadLine()) != null)
                {
                    try
                    {
                        var evt = JsonSerializer.Deserialize<RunnerEvent>(line);
                        if (evt != null && OnEvent != null) OnEvent(evt);
                    }
                    catch { /* bad line — skip */ }
                }
            }
            catch { /* pipe closed */ }
        }

        public void Dispose()
        {
            try { if (_reader != null) _reader.Dispose(); } catch { }
            try { if (_pipe != null) _pipe.Dispose(); } catch { }
        }
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
