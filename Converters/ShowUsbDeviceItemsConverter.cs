using System;
using System.Globalization;
using System.Windows.Data;

namespace Identinator.Converters;

internal class ShowUsbDeviceItemsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if ((bool)values[1]) return values[0];
        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return null;
    }
}