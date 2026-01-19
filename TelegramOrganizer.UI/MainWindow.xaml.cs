using System.Windows;
using TelegramOrganizer.UI.ViewModels;

namespace TelegramOrganizer.UI
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        // الـ Container هو اللي هيبعتلنا الـ ViewModel جاهز
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
        }

        private void Window_StateChanged(object sender, System.EventArgs e)
        {
            // Minimize to tray when window is minimized
            if (WindowState == WindowState.Minimized)
            {
                _viewModel.MinimizeToTray();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.Cleanup();
        }
    }
}