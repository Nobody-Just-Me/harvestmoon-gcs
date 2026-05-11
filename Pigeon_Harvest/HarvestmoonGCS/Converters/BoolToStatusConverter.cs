using System;
using Microsoft.UI.Xaml.Data;

namespace HarvestmoonGCS.Converters;

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isOnline)
        {
            return isOnline ? "ONLINE" : "OFFLINE";
        }
        return "UNKNOWN";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString() == "ONLINE";
    }
}
