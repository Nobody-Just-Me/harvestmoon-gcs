using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HarvestmoonGCS.Converters;

/// <summary>
/// Converts IsModified boolean to background brush for highlighting modified parameters.
/// </summary>
public class ModifiedBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isModified && isModified)
        {
            // Light yellow background for modified parameters
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 250, 205));
        }
        
        return new SolidColorBrush(Colors.Transparent);
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts IsModified boolean to foreground brush for highlighting modified parameter values.
/// </summary>
public class ModifiedForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isModified && isModified)
        {
            // Orange color for modified parameter values
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 126, 34));
        }
        
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 44, 62, 80));
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts IsCurrent boolean to background brush for highlighting current waypoint.
/// </summary>
public class CurrentWaypointBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isCurrent && isCurrent)
        {
            // Light green background for current waypoint
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 212, 237, 218));
        }
        
        return new SolidColorBrush(Colors.Transparent);
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
