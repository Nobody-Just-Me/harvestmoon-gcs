using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System;
using Windows.UI;

namespace HarvestmoonGCS.Converters;

/// <summary>
/// Converts boolean status to color brush for visual indicators
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isOnline && isOnline)
        {
            // Green for online/active
            return new SolidColorBrush(Color.FromArgb(255, 0, 255, 0));
        }
        else
        {
            // Gray for offline/inactive
            return new SolidColorBrush(Color.FromArgb(255, 74, 74, 122));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
