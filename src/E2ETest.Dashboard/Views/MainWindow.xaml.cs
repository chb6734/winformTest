using System.Threading;
using System.Windows;
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
        }

        private void OnClosed(object sender, System.EventArgs e)
        {
            if (_cts != null) _cts.Cancel();
            _vm.Dispose();
        }
    }
}
