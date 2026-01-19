using System.Windows;
using TelegramOrganizer.UI.ViewModels;

namespace TelegramOrganizer.UI.Views
{
    public partial class RulesWindow : Window
    {
        public RulesWindow(RulesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
