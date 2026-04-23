using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using E2ETest.Dashboard.ViewModels;

namespace E2ETest.Dashboard.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            _vm.StartPipeServer(_cts.Token);
            // 커맨드라인 인자로 CLI exe path + test file path 전달받음
            var args = Environment.GetCommandLineArgs();
            _vm.ParseCommandLine(args);
        }

        private void OnClosed(object sender, System.EventArgs e)
        {
            if (_cts != null) _cts.Cancel();
            _vm.Dispose();
        }

        private void OnStepClick(object sender, MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;
            var step = border.Tag as StepVm;
            if (step == null) return;
            _vm.SelectStep(step);
        }

        private void OnRerunClick(object sender, RoutedEventArgs e) => _vm.Rerun();

        private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_vm.LastRunDirectory) && Directory.Exists(_vm.LastRunDirectory))
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _vm.LastRunDirectory + "\"") { UseShellExecute = true }); }
                catch { }
            }
        }
    }
}
