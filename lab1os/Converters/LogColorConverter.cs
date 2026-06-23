using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DirectoryCopier.Models;

namespace DirectoryCopier.Converters
{
    public class LogColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogType type)
            {
                return type switch
                {
                    LogType.Success => Brushes.LightGreen,
                    LogType.Error => Brushes.OrangeRed,
                    LogType.Warning => Brushes.Gold,
                    LogType.Info => Brushes.LightGray,
                    _ => Brushes.White
                };
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
