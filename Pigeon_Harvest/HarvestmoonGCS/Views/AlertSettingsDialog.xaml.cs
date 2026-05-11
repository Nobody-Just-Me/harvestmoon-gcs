using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Views;

public sealed partial class AlertSettingsDialog : ContentDialog
{
    public AlertSettings Settings { get; private set; }
    
    public AlertSettingsDialog()
    {
        this.InitializeComponent();
        Settings = new AlertSettings();
        LoadSettings();
    }
    
    public AlertSettingsDialog(AlertSettings existingSettings)
    {
        this.InitializeComponent();
        Settings = existingSettings;
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        // TTS and Sound
        AlertSoundCheck.IsChecked = Settings.SoundEnabled;
        BatteryVoiceCheck.IsChecked = Settings.TtsEnabled;
        GpsVoiceCheck.IsChecked = Settings.TtsEnabled;
        ConnectionVoiceCheck.IsChecked = Settings.TtsEnabled;
        GeofenceVoiceCheck.IsChecked = Settings.TtsEnabled;
        FlightModeVoiceCheck.IsChecked = Settings.TtsEnabled;
        
        // Battery
        BatteryAlertEnabledCheck.IsChecked = Settings.BatteryWarningEnabled;
        BatteryThresholdSlider.Value = Settings.BatteryWarningThreshold;
        
        // GPS
        GpsAlertEnabledCheck.IsChecked = Settings.GpsLostEnabled;
        
        // Connection
        ConnectionAlertEnabledCheck.IsChecked = Settings.ConnectionLostEnabled;
        
        // Geofence
        GeofenceAlertEnabledCheck.IsChecked = Settings.GeofenceViolationEnabled;
        
        // Flight Mode
        FlightModeAlertEnabledCheck.IsChecked = Settings.FlightModeChangeEnabled;
    }
    
    public void SaveSettings()
    {
        // TTS and Sound - use first checkbox value for all
        Settings.TtsEnabled = BatteryVoiceCheck.IsChecked ?? true;
        Settings.SoundEnabled = AlertSoundCheck.IsChecked ?? true;
        
        // Battery
        Settings.BatteryWarningEnabled = BatteryAlertEnabledCheck.IsChecked ?? true;
        Settings.BatteryWarningThreshold = (int)BatteryThresholdSlider.Value;
        
        // GPS
        Settings.GpsLostEnabled = GpsAlertEnabledCheck.IsChecked ?? true;
        
        // Connection
        Settings.ConnectionLostEnabled = ConnectionAlertEnabledCheck.IsChecked ?? true;
        
        // Geofence
        Settings.GeofenceViolationEnabled = GeofenceAlertEnabledCheck.IsChecked ?? true;
        
        // Flight Mode
        Settings.FlightModeChangeEnabled = FlightModeAlertEnabledCheck.IsChecked ?? true;
    }
}
