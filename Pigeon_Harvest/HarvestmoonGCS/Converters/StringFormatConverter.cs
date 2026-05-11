using System;
using Microsoft.UI.Xaml.Data;

namespace HarvestmoonGCS.Converters;

public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string format && value != null)
        {
            try
            {
                return string.Format(format, value);
            }
            catch
            {
                return value.ToString();
            }
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
