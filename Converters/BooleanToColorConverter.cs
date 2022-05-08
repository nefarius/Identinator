using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Identinator.Converters;

internal class BooleanToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isSet = (bool)value;
        return isSet ? Brushes.Crimson : Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Equals(value, Brushes.Transparent);
    }
}