using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
        private string _cliExePath;
        private string _testFilePath;

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
                    case "PIPE ERROR": return Brushes.OrangeRed;
                    default: return Brushes.LightGray;
                }
            }
        }

        private ImageSource _screenshotImage;
        public ImageSource ScreenshotImage { get { return _screenshotImage; } set { _screenshotImage = value; OnChanged(); } }

        private string _previewCaption = "실행을 기다리는 중";
        public string PreviewCaption { get { return _previewCaption; } set { _previewCaption = value; OnChanged(); } }

        public bool CanRerun
        {
            get { return !string.IsNullOrEmpty(_cliExePath) && !string.IsNullOrEmpty(_testFilePath) && File.Exists(_cliExePath) && _statusText != "RUNNING"; }
        }

        public bool HasRunResult { get { return !string.IsNullOrEmpty(LastRunDirectory); } }
        public string LastRunDirectory { get; private set; }

        public void ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--cli-exe" && i + 1 < args.Length) _cliExePath = args[++i];
                else if (args[i] == "--test-file" && i + 1 < args.Length) _testFilePath = args[++i];
            }
            OnChanged(nameof(CanRerun));
            if (!string.IsNullOrEmpty(_testFilePath))
                AddLog("INFO", "Test file: " + _testFilePath);
        }

        public void StartPipeServer(CancellationToken ct)
        {
            _server = new PipeServer();
            _server.OnEvent += OnRunnerEvent;
            _server.OnClientConnected += () => Application.Current?.Dispatcher.Invoke(() => AddLog("INFO", "CLI connected"));
            _server.OnClientDisconnected += () => Application.Current?.Dispatcher.Invoke(() => { AddLog("INFO", "CLI disconnected"); OnChanged(nameof(CanRerun)); });
            new Thread(() =>
            {
                try { _server.StartAndWait(ct); }
                catch (Exception ex)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        StatusText = "PIPE ERROR";
                        AddLog("ERROR", "Pipe server failed: " + ex.Message);
                    });
                }
            }) { IsBackground = true }.Start();
            AddLog("INFO", "Dashboard ready. Waiting for runner...");
        }

        public void Rerun()
        {
            if (!CanRerun) return;
            try
            {
                var psi = new ProcessStartInfo(_cliExePath, "run --ui-attach \"" + _testFilePath + "\"")
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(_testFilePath)
                };
                Process.Start(psi);
                AddLog("INFO", "Re-run 요청됨: " + _testFilePath);
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "Re-run 실패: " + ex.Message);
            }
        }

        public void SelectStep(StepVm step)
        {
            if (step == null) return;
            // 이전 선택 해제
            foreach (var s in Steps) s.IsSelected = false;
            step.IsSelected = true;

            if (!string.IsNullOrEmpty(step.ScreenshotPath) && File.Exists(step.ScreenshotPath))
            {
                ScreenshotImage = LoadImage(step.ScreenshotPath);
                PreviewCaption = "스텝 " + (step.Index + 1) + " — " + step.Description + " (" + step.StatusText + ")";
            }
            else
            {
                PreviewCaption = "스텝 " + (step.Index + 1) + " — 스크린샷 없음";
            }
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
                        PreviewCaption = "실행 중...";
                        // Runner가 보내준 SourcePath가 있으면 Re-run 활성화
                        if (!string.IsNullOrEmpty(evt.TestSourcePath) && string.IsNullOrEmpty(_testFilePath))
                        {
                            _testFilePath = evt.TestSourcePath;
                        }
                        OnChanged(nameof(CanRerun));
                        AddLog("INFO", "=== Run started: " + TestName + " ===");
                        break;
                    case "step.started":
                        {
                            var vm = new StepVm
                            {
                                Index = evt.StepIndex ?? Steps.Count,
                                Description = !string.IsNullOrEmpty(evt.StepDescription) ? evt.StepDescription : FriendlyDefault(evt),
                                TechnicalLabel = BuildTechnicalLabel(evt),
                                Icon = "⏳",
                                ColorBrush = Brushes.Gold,
                                StatusText = "Running"
                            };
                            Steps.Add(vm);
                            ProgressText = (evt.StepIndex + 1) + "/" + _total;
                        }
                        break;
                    case "step.ended":
                        if (evt.StepIndex.HasValue && evt.StepIndex.Value < Steps.Count)
                        {
                            var vm = Steps[evt.StepIndex.Value];
                            if (evt.Status == "Passed") { vm.Icon = "✓"; vm.ColorBrush = Brushes.LightGreen; vm.StatusText = "Passed"; }
                            else if (evt.Status == "Failed") { vm.Icon = "✗"; vm.ColorBrush = Brushes.IndianRed; vm.StatusText = "Failed"; vm.TechnicalLabel += " — " + evt.Message; }
                            vm.DurationLabel = evt.DurationMs.HasValue ? (evt.DurationMs.Value + " ms") : "";
                            vm.ScreenshotPath = evt.ScreenshotPath;
                            vm.NotifyChanged();
                            // 가장 최근 완료된 스텝을 미리보기에 표시
                            if (!string.IsNullOrEmpty(evt.ScreenshotPath) && File.Exists(evt.ScreenshotPath))
                            {
                                ScreenshotImage = LoadImage(evt.ScreenshotPath);
                                PreviewCaption = "최근: 스텝 " + (evt.StepIndex + 1) + " " + vm.StatusText;
                            }
                            if (!string.IsNullOrEmpty(evt.ScreenshotPath))
                            {
                                LastRunDirectory = Path.GetDirectoryName(Path.GetDirectoryName(evt.ScreenshotPath));
                                OnChanged(nameof(HasRunResult));
                            }
                        }
                        break;
                    case "run.ended":
                        StatusText = evt.Status == "Passed" ? "PASSED" : "FAILED";
                        OnChanged(nameof(CanRerun));
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

        private static string FriendlyDefault(RunnerEvent evt)
        {
            string a = evt.StepAction ?? "";
            string t = evt.StepTarget ?? "";
            string v = evt.StepValue ?? "";
            switch (a.ToLowerInvariant())
            {
                case "click": return t + " 클릭";
                case "type": return t + "에 \"" + v + "\" 입력";
                case "fill": return t + "에 \"" + v + "\" 채우기";
                case "focus": return t + " 포커스";
                case "wait": return v + "ms 대기";
                case "screenshot": return "스크린샷 캡처" + (string.IsNullOrEmpty(evt.StepName) ? "" : ": " + evt.StepName);
                case "assert.visible": return t + " 화면 표시 확인";
                case "assert.text": return t + " 텍스트에 \"" + v + "\" 포함 확인";
                case "assert.enabled": return t + " 활성 상태 확인";
                default: return a + " " + t;
            }
        }

        private static string BuildTechnicalLabel(RunnerEvent evt)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(evt.StepAction);
            if (!string.IsNullOrEmpty(evt.StepTarget)) sb.Append(" ").Append(evt.StepTarget);
            if (!string.IsNullOrEmpty(evt.StepValue)) sb.Append(" = ").Append(evt.StepValue);
            return sb.ToString();
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
        public int Index { get; set; }
        public string Description { get; set; }
        public string TechnicalLabel { get; set; }
        public string Icon { get; set; }
        public Brush ColorBrush { get; set; }
        public string StatusText { get; set; } = "Pending";
        public string DurationLabel { get; set; } = "";
        public string ScreenshotPath { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; NotifyChanged(); }
        }

        public Brush RowBackground
        {
            get { return _isSelected ? (Brush)new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x55)) : Brushes.Transparent; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyChanged()
        {
            foreach (var name in new[] { "Description", "TechnicalLabel", "Icon", "ColorBrush", "StatusText", "DurationLabel", "ScreenshotPath", "IsSelected", "RowBackground" })
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
