using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Helpers;

public static class Extensions
{
    public static T Map<T>(this T source, Action<T> action)
    {
        action(source);
        return source;
    }

    public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
    {
        return source.Skip(Math.Max(0, source.Count() - count));
    }

    public static double ToRadians(this double degrees) => degrees * Math.PI / 180.0;
    public static double ToDegrees(this double radians) => radians * 180.0 / Math.PI;

    public static async Task<byte[]> ToBitmapSourceAsync(this byte[] imageData)
    {
        // Simple passthrough for now - in real implementation would convert to platform-specific bitmap
        await Task.CompletedTask;
        return imageData;
    }

    public static async Task<object> ToImageSourceAsync(this byte[] imageData)
    {
        // Platform-specific image source conversion
        await Task.CompletedTask;
        return imageData;
    }

    public static string ToHexString(this byte[] bytes)
    {
        return Convert.ToHexString(bytes);
    }

    public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }
}
