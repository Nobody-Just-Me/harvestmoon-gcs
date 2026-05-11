using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace HarvestmoonGCS.Droid;

[Activity(
    MainLauncher = true,
    ConfigurationChanges = global::Uno.UI.ActivityHelper.AllConfigChanges,
    WindowSoftInputMode = SoftInput.AdjustNothing | SoftInput.StateHidden
)]
public class MainActivity : Microsoft.UI.Xaml.ApplicationActivity
{
    private const int PermissionRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        global::AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);

        base.OnCreate(savedInstanceState);

        // Request camera + location permissions at startup
        RequestRequiredPermissions();

        // Keep screen on during GCS operation
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
    }

    private void RequestRequiredPermissions()
    {
        var permissions = new[]
        {
            Android.Manifest.Permission.Camera,
            Android.Manifest.Permission.AccessFineLocation,
            Android.Manifest.Permission.Internet,
        };

        var needed = permissions
            .Where(p => ContextCompat.CheckSelfPermission(this, p) != Permission.Granted)
            .ToArray();

        if (needed.Length > 0)
        {
            ActivityCompat.RequestPermissions(this, needed, PermissionRequestCode);
        }
    }
}
