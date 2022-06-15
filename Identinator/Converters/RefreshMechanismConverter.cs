using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Identinator.Converters;

internal class RefreshMechanismConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parameterString = parameter as string;
        if (parameterString == null)
            return DependencyProperty.UnsetValue;

        if (Enum.IsDefined(value.GetType(), value) == false)
            return DependencyProperty.UnsetValue;

        var paramValue = Enum.Parse(value.GetType(), parameterString);
        return paramValue.Equals(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parameterString = parameter as string;
        var valueAsBool = (bool)value;

        if (parameterString == null || !valueAsBool)
            try
            {
                return Enum.Parse(targetType, "0");
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }

        return Enum.Parse(targetType, parameterString);
    }
}