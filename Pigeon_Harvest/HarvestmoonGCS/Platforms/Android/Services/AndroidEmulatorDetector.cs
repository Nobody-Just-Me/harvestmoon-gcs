#if __ANDROID__
using Android.OS;
using HarvestmoonGCS.Services;
using System;
using System.Diagnostics;

namespace HarvestmoonGCS.Platforms.Android.Services;

/// <summary>
/// Android-specific emulator detector using Android APIs.
/// Overrides base implementation to use Android.OS.SystemProperties.
/// </summary>
public class AndroidEmulatorDetector : EmulatorDetector
{
    /// <summary>
    /// Gets Android system property using Android.OS.Build and reflection.
    /// </summary>
    protected override string GetSystemProperty(string propertyName)
    {
        try
        {
            // Use Android.OS.Build for common properties
            return propertyName switch
            {
                "ro.hardware" => Build.Hardware ?? string.Empty,
                "ro.product.model" => Build.Model ?? string.Empty,
                "ro.product.manufacturer" => Build.Manufacturer ?? string.Empty,
                "ro.product.brand" => Build.Brand ?? string.Empty,
                "ro.build.product" => Build.Product ?? string.Empty,
                "ro.build.fingerprint" => Build.Fingerprint ?? string.Empty,
                "ro.build.version.release" => Build.VERSION.Release ?? string.Empty,
                "ro.build.id" => Build.Id ?? string.Empty,
                "ro.product.cpu.abi" => Build.CpuAbi ?? string.Empty,
                _ => GetSystemPropertyViaReflection(propertyName)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidEmulatorDetector] Error getting property {propertyName}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets system property via reflection for properties not exposed by Build class.
    /// </summary>
    private string GetSystemPropertyViaReflection(string propertyName)
    {
        try
        {
            // Try to use SystemProperties class via reflection
            var systemPropertiesClass = Java.Lang.Class.ForName("android.os.SystemProperties");
            var getMethod = systemPropertiesClass.GetMethod("get", Java.Lang.Class.FromType(typeof(Java.Lang.String)));
            var value = getMethod?.Invoke(null, new Java.Lang.Object[] { new Java.Lang.String(propertyName) });
            return value?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
#endif