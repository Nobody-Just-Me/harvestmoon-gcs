using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Pigeon_Uno.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            bool invert = parameter?.ToString() == "Invert";
            bool result = invert ? !boolValue : boolValue;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
        {
            bool result = visibility == Visibility.Visible;
            bool invert = parameter?.ToString() == "Invert";
            return invert ? !result : result;
        }
        return false;
    }
}
