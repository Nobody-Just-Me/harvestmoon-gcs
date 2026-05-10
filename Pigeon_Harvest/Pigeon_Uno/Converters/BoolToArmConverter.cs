using System;
using Microsoft.UI.Xaml.Data;

namespace Pigeon_Uno.Converters;

public class BoolToArmConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isArmed)
        {
            return isArmed ? "Armed" : "Disarmed";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString() == "Armed";
    }
}
