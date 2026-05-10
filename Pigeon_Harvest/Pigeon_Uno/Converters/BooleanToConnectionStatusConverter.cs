using System;
using Microsoft.UI.Xaml.Data;
using Pigeon_Uno.Controls;

namespace Pigeon_Uno.Converters;

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
