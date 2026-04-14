using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GitBashDesktop.Helpers
{
    public class DiffLineColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            var line = value?.ToString() ?? "";
            var param = parameter?.ToString() ?? "fg";

            if (param == "bg")
            {
                if (line.StartsWith("+") && !line.StartsWith("+++"))
                    return new SolidColorBrush(Color.FromRgb(20, 60, 20));
                if (line.StartsWith("-") && !line.StartsWith("---"))
                    return new SolidColorBrush(Color.FromRgb(60, 20, 20));
                return Brushes.Transparent;
            }
            else // fg
            {
                if (line.StartsWith("+") && !line.StartsWith("+++"))
                    return new SolidColorBrush(Color.FromRgb(78, 201, 148));
                if (line.StartsWith("-") && !line.StartsWith("---"))
                    return new SolidColorBrush(Color.FromRgb(244, 112, 103));
                if (line.StartsWith("@@"))
                    return new SolidColorBrush(Color.FromRgb(86, 156, 214));

                // Use theme-aware color for normal lines
                return Application.Current.Resources["TextPrimaryBrush"]
                    as SolidColorBrush
                    ?? new SolidColorBrush(Color.FromRgb(30, 30, 30));
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}