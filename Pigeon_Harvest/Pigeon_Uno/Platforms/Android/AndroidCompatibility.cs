#if __ANDROID__
using Android.Content;
using Android.OS;
using Android.Widget;
using Pigeon_Uno.Platforms.Android.Services;
using System;
using System.IO;

namespace Pigeon_Uno.Platforms.Android;

/// <summary>
/// Android compatibility helpers to ensure all features work on Android
/// </summary>
public static class AndroidCompatibility
{
    private static Context? _context;
    private static global::Android.App.Activity? _activity;
    private static AndroidFileService? _fileService;
    private static AndroidSettingsService? _settingsService;
    private static AndroidCameraService? _cameraService;
    
    public static void Initialize(Context context)
    {
        _context = context;
        _activity = context as global::Android.App.Activity;
        _fileService = new AndroidFileService(context);
        _settingsService = new AndroidSettingsService(context);
        _cameraService = new AndroidCameraService(context);
    }

    public static global::Android.App.Activity? CurrentActivity => _activity;
    
    /// <summary>
    /// Get file service
    /// </summary>
    public static AndroidFileService? FileService => _fileService;
    
    /// <summary>
    /// Get settings service
    /// </summary>
    public static AndroidSettingsService? SettingsService => _settingsService;
    
    /// <summary>
    /// Get camera service
    /// </summary>
    public static AndroidCameraService? CameraService => _cameraService;
    
    /// <summary>
    /// Get Downloads folder path (Android-safe)
    /// </summary>
    public static string GetDownloadsPath()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            // Android 10+ (Scoped Storage)
            var downloadsDir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads);
            return downloadsDir?.AbsolutePath ?? "/storage/emulated/0/Download";
        }
        else
        {
            // Android 9 and below
            return Path.Combine(global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "", "Download");
        }
    }
    
    /// <summary>
    /// Get app-specific storage path (always accessible)
    /// </summary>
    public static string GetAppStoragePath()
    {
        if (_context == null)
            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            
        return _context.GetExternalFilesDir(null)?.AbsolutePath 
            ?? _context.FilesDir?.AbsolutePath 
            ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
    }
    
    /// <summary>
    /// Show toast message
    /// </summary>
    public static void ShowToast(string message, bool longDuration = false)
    {
        if (_context == null) return;
        
        var duration = longDuration ? ToastLength.Long : ToastLength.Short;
        Toast.MakeText(_context, message, duration)?.Show();
    }
    
    /// <summary>
    /// Check if permission is granted
    /// </summary>
    public static bool HasPermission(string permission)
    {
        if (_context == null) return false;
        
        return _context.CheckSelfPermission(permission) == global::Android.Content.PM.Permission.Granted;
    }
    
    /// <summary>
    /// Check if running on Android
    /// </summary>
    public static bool IsAndroid => true;
    
    /// <summary>
    /// Get Android version
    /// </summary>
    public static int AndroidVersion => (int)Build.VERSION.SdkInt;
    
    /// <summary>
    /// Check if device has USB host support
    /// </summary>
    public static bool HasUsbHost()
    {
        if (_context == null) return false;
        
        var packageManager = _context.PackageManager;
        return packageManager?.HasSystemFeature(global::Android.Content.PM.PackageManager.FeatureUsbHost) ?? false;
    }
    
    /// <summary>
    /// Check if device has camera
    /// </summary>
    public static bool HasCamera()
    {
        if (_context == null) return false;
        
        var packageManager = _context.PackageManager;
        return packageManager?.HasSystemFeature(global::Android.Content.PM.PackageManager.FeatureCamera) ?? false;
    }
    
    /// <summary>
    /// Get device model
    /// </summary>
    public static string DeviceModel => Build.Model ?? "Unknown";
    
    /// <summary>
    /// Get device manufacturer
    /// </summary>
    public static string DeviceManufacturer => Build.Manufacturer ?? "Unknown";
    
    /// <summary>
    /// Check if running on emulator
    /// </summary>
    public static bool IsEmulator()
    {
        return Build.Fingerprint?.Contains("generic") == true
            || Build.Fingerprint?.Contains("unknown") == true
            || Build.Model?.Contains("google_sdk") == true
            || Build.Model?.Contains("Emulator") == true
            || Build.Model?.Contains("Android SDK") == true
            || Build.Manufacturer?.Contains("Genymotion") == true
            || Build.Hardware?.Contains("goldfish") == true
            || Build.Hardware?.Contains("ranchu") == true
            || Build.Product?.Contains("sdk") == true
            || Build.Product?.Contains("google_sdk") == true
            || Build.Product?.Contains("sdk_x86") == true
            || Build.Product?.Contains("vbox86p") == true;
    }
    
    /// <summary>
    /// Get screen density
    /// </summary>
    public static float ScreenDensity
    {
        get
        {
            if (_context == null) return 1.0f;
            return _context.Resources?.DisplayMetrics?.Density ?? 1.0f;
        }
    }
    
    /// <summary>
    /// Get screen width in pixels
    /// </summary>
    public static int ScreenWidth
    {
        get
        {
            if (_context == null) return 0;
            return _context.Resources?.DisplayMetrics?.WidthPixels ?? 0;
        }
    }
    
    /// <summary>
    /// Get screen height in pixels
    /// </summary>
    public static int ScreenHeight
    {
        get
        {
            if (_context == null) return 0;
            return _context.Resources?.DisplayMetrics?.HeightPixels ?? 0;
        }
    }
}
#endif
