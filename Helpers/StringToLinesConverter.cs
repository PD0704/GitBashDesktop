using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace GitBashDesktop.Helpers
{
    public class StringToLinesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (value is not string str) return new List<string>();
            return str.Split('\n')
                      .Select(l => l.Trim())
                      .Where(l => !string.IsNullOrEmpty(l))
                      .ToList();
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}