using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Identinator.Converters;

internal class HexStringConverter : IValueConverter
{
    private string lastValidValue;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string ret = null;

        if (value is not string valueAsString) return ret;
        var parts = valueAsString.ToCharArray();
        var formatted = parts.Select((p, i) => ++i % 2 == 0 ? string.Concat(p.ToString(), "") : p.ToString());
        ret = string.Join(string.Empty, formatted).Trim();
        
        return ret;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        object ret = null;
        if (value != null && value is string)
        {
            var valueAsString = ((string)value).Replace(" ", string.Empty).ToUpper();
            ret = lastValidValue = IsHex(valueAsString) ? valueAsString : lastValidValue;
        }

        return ret;
    }


    private static bool IsHex(string text)
    {
        var reg = new Regex("^[0-9A-Fa-f]*$");
        return reg.IsMatch(text);
    }
}