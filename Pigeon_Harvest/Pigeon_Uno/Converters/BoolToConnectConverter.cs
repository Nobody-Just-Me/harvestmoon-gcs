using Microsoft.UI.Xaml.Data;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace Pigeon_Uno.Converters;

public class BoolToConnectTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool b && b) ? "Disconnect" : "Connect";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BoolToConnectColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool b && b) ? new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)) : new SolidColorBrush(Color.FromArgb(255, 0, 128, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
