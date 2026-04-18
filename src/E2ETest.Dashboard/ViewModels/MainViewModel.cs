using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using E2ETest.Core.Events;
using E2ETest.Ipc;

namespace E2ETest.Dashboard.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private PipeServer _server;

        public ObservableCollection<StepVm> Steps { get; } = new ObservableCollection<StepVm>();
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        private string _testName = "대기 중...";
        public string TestName { get { return _testName; } set { _testName = value; OnChanged(); } }

        private string _progressText = "";
        public string ProgressText { get { return _progressText; } set { _progressText = value; OnChanged(); } }

        private string _statusText = "READY";
        public string StatusText { get { return _statusText; } set { _statusText = value; OnChanged(); OnChanged(nameof(StatusColor)); } }

        public Brush StatusColor
        {
            get
            {
                switch (_statusText)
                {
                    case "RUNNING": return Brushes.Gold;
                    case "PASSED": return Brushes.LightGreen;
                    case "FAILED": return Brushes.IndianRed;
                    default: return Brushes.LightGray;
                }
            }
        }

        private ImageSource _screenshotImage;
        public ImageSource ScreenshotImage { get { return _screenshotImage; } set { _screenshotImage = value; OnChanged(); } }

        public void StartPipeServer(CancellationToken ct)
        {
            _server = new PipeServer();
            _server.OnEvent += OnRunnerEvent;
            new Thread(() => _server.StartAndWait(ct)) { IsBackground = true }.Start();
            AddLog("INFO", "Dashboard ready. Waiting for runner...");
        }

        private int _total;

        private void OnRunnerEvent(RunnerEvent evt)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                switch (evt.Type)
                {
                    case "run.started":
                        Steps.Clear();
                        TestName = evt.TestName ?? "(no name)";
                        _total = evt.TotalSteps ?? 0;
                        ProgressText = "0/" + _total;
                        StatusText = "RUNNING";
                        AddLog("INFO", "=== Run started: " + TestName + " ===");
                        break;
                    case "step.started":
                        {
                            var vm = new StepVm
                            {
                                Title = (evt.StepIndex + 1) + ". " + evt.StepAction + " " + (evt.StepTarget ?? ""),
                                Subtitle = evt.StepName ?? "",
                                Icon = "⏳",
                                ColorBrush = Brushes.Gold
                            };
                            Steps.Add(vm);
                            ProgressText = (evt.StepIndex + 1) + "/" + _total;
                        }
                        break;
                    case "step.ended":
                        if (evt.StepIndex.HasValue && evt.StepIndex.Value < Steps.Count)
                        {
                            var vm = Steps[evt.StepIndex.Value];
                            if (evt.Status == "Passed") { vm.Icon = "✓"; vm.ColorBrush = Brushes.LightGreen; }
                            else if (evt.Status == "Failed") { vm.Icon = "✗"; vm.ColorBrush = Brushes.IndianRed; vm.Subtitle = evt.Message; }
                            vm.NotifyChanged();
                        }
                        break;
                    case "run.ended":
                        StatusText = evt.Status == "Passed" ? "PASSED" : "FAILED";
                        AddLog("INFO", "=== Run ended: " + evt.Status + " ===");
                        break;
                    case "log":
                        AddLog(evt.LogLevel ?? "INFO", evt.Message ?? "");
                        break;
                    case "screenshot":
                        if (!string.IsNullOrEmpty(evt.ScreenshotPath) && File.Exists(evt.ScreenshotPath))
                        {
                            ScreenshotImage = LoadImage(evt.ScreenshotPath);
                        }
                        break;
                }
            });
        }

        private void AddLog(string lvl, string msg)
        {
            Logs.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] [" + lvl + "] " + msg);
            while (Logs.Count > 500) Logs.RemoveAt(0);
        }

        private static ImageSource LoadImage(string path)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose() { if (_server != null) _server.Dispose(); }
    }

    public sealed class StepVm : INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Icon { get; set; }
        public Brush ColorBrush { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Title"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Subtitle"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Icon"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ColorBrush"));
        }
    }
}
