using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TelegramOrganizer.UI.ViewModels;

namespace TelegramOrganizer.UI.Views
{
    public partial class StatisticsWindow : Window
    {
        public StatisticsWindow(StatisticsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Converts count to bar height for the chart.
    /// </summary>
    public class CountToHeightConverter : IValueConverter
    {
        public double MaxHeight { get; set; } = 100;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                // Minimum height of 5 if count > 0
                if (count == 0) return 5.0;
                
                // Scale: assume max 50 files per day for full height
                double scaled = Math.Min(count / 50.0, 1.0) * MaxHeight;
                return Math.Max(scaled, 10.0); // Minimum visible height
            }
            return 5.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
