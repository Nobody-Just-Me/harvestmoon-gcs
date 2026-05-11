using System;
using Microsoft.UI.Xaml.Data;
using HarvestmoonGCS.Controls;

namespace HarvestmoonGCS.Converters;

public class BooleanToConnectionStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return boolValue ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
        }
        return ConnectionStatus.Disconnected;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is ConnectionStatus status)
        {
            return status == ConnectionStatus.Connected;
        }
        return false;
    }
}
