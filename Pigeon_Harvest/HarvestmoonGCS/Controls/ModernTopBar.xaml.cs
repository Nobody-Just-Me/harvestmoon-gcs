using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace HarvestmoonGCS.Controls;

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected
}

public sealed partial class ModernTopBar : UserControl
{
    private readonly DispatcherTimer _exitTimer;
    private bool _isConfirmingExit;
    private ConnectionStatus _lastVisualStatus = (ConnectionStatus)(-1);
    private double _lastRenderedBattery = double.NaN;
    private int _lastRenderedSats = int.MinValue;
    private string? _lastRenderedMode;
    private bool _lastRenderedArmed;
    private string? _lastRenderedEndpoint;
    private bool? _lastRenderedVoiceListening;
    private string? _lastRenderedVoiceStatus;

    public ModernTopBar()
    {
        this.InitializeComponent();
        
        _exitTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _exitTimer.Tick += (s, e) => ResetExitState();
        
        this.Loaded += (s, e) => UpdateConnectionVisuals();
    }

    // Dependency Properties
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(ConnectionStatus), typeof(ModernTopBar), 
            new PropertyMetadata(ConnectionStatus.Disconnected, (d, e) => ((ModernTopBar)d).UpdateConnectionVisuals()));

    public ConnectionStatus Status
    {
        get => (ConnectionStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty BatteryProperty =
        DependencyProperty.Register(nameof(Battery), typeof(double), typeof(ModernTopBar), 
            new PropertyMetadata(0.0, (d, e) => ((ModernTopBar)d).UpdateBatteryVisuals()));

    public double Battery
    {
        get => (double)GetValue(BatteryProperty);
        set => SetValue(BatteryProperty, value);
    }

    public static readonly DependencyProperty GpsSatsProperty =
        DependencyProperty.Register(nameof(GpsSats), typeof(int), typeof(ModernTopBar), 
            new PropertyMetadata(0, (d, e) => ((ModernTopBar)d).UpdateGpsVisuals()));

    public int GpsSats
    {
        get => (int)GetValue(GpsSatsProperty);
        set => SetValue(GpsSatsProperty, value);
    }

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(string), typeof(ModernTopBar), 
            new PropertyMetadata("—", (d, e) => ((ModernTopBar)d).UpdateModeVisuals()));

    public string Mode
    {
        get => (string)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty IsArmedProperty =
        DependencyProperty.Register(nameof(IsArmed), typeof(bool), typeof(ModernTopBar), 
            new PropertyMetadata(false, (d, e) => ((ModernTopBar)d).UpdateArmedVisuals()));

    public bool IsArmed
    {
        get => (bool)GetValue(IsArmedProperty);
        set => SetValue(IsArmedProperty, value);
    }

    public static readonly DependencyProperty AiFpsProperty =
        DependencyProperty.Register(nameof(AiFps), typeof(double), typeof(ModernTopBar),
            new PropertyMetadata(0.0, (d, e) => ((ModernTopBar)d).UpdateAiFpsVisuals()));

    public double AiFps
    {
        get => (double)GetValue(AiFpsProperty);
        set => SetValue(AiFpsProperty, value);
    }

    private void UpdateAiFpsVisuals()
    {
        if (AIFpsText != null)
        {
            AIFpsText.Text = AiFps > 0 ? $"AI {AiFps:F0} FPS" : "AI — FPS";
        }
    }

    public static readonly DependencyProperty EndpointProperty =
        DependencyProperty.Register(nameof(Endpoint), typeof(string), typeof(ModernTopBar), 
            new PropertyMetadata(null, (d, e) => ((ModernTopBar)d).UpdateEndpointVisuals()));

    public string Endpoint
    {
        get => (string)GetValue(EndpointProperty);
        set => SetValue(EndpointProperty, value);
    }

    public static readonly DependencyProperty IsVoiceListeningProperty =
        DependencyProperty.Register(nameof(IsVoiceListening), typeof(bool), typeof(ModernTopBar),
            new PropertyMetadata(false, (d, e) => ((ModernTopBar)d).UpdateVoiceVisuals()));

    public bool IsVoiceListening
    {
        get => (bool)GetValue(IsVoiceListeningProperty);
        set => SetValue(IsVoiceListeningProperty, value);
    }

    public static readonly DependencyProperty VoiceStatusProperty =
        DependencyProperty.Register(nameof(VoiceStatus), typeof(string), typeof(ModernTopBar),
            new PropertyMetadata("idle", (d, e) => ((ModernTopBar)d).UpdateVoiceVisuals()));

    public string VoiceStatus
    {
        get => (string)GetValue(VoiceStatusProperty);
        set => SetValue(VoiceStatusProperty, value);
    }

    // Events
    public event EventHandler? ConnectClicked;
    public event EventHandler? ExitClicked;
    public event EventHandler? RTLClicked;
    public event EventHandler? MissionClicked;

    // Mission state
    private bool _isMissionRunning = true;

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        ConnectClicked?.Invoke(this, EventArgs.Empty);
    }

    private void RTLButton_Click(object sender, RoutedEventArgs e)
    {
        RTLClicked?.Invoke(this, EventArgs.Empty);
    }

    private void MissionButton_Click(object sender, RoutedEventArgs e)
    {
        _isMissionRunning = !_isMissionRunning;
        if (_isMissionRunning)
        {
            MissionLabel.Text = "Stop Mission";
            MissionIcon.Glyph = "\uE768"; // Stop icon
        }
        else
        {
            MissionLabel.Text = "Start Mission";
            MissionIcon.Glyph = "\uE768"; // Play icon
        }
        MissionClicked?.Invoke(this, EventArgs.Empty);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConfirmingExit)
        {
            _isConfirmingExit = true;
            VisualStateManager.GoToState(this, "ExitConfirm", true);
            _exitTimer.Start();
        }
        else
        {
            ResetExitState();
            ExitClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ResetExitState()
    {
        _isConfirmingExit = false;
        _exitTimer.Stop();
        VisualStateManager.GoToState(this, "ExitNormal", true);
    }

    // Page title update method
    public void UpdatePageTitle(string title, string subtitle, string iconGlyph)
    {
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
        PageIcon.Glyph = iconGlyph;
    }

    private void UpdateConnectionVisuals()
    {
        if (_lastVisualStatus == Status)
        {
            return;
        }

        _lastVisualStatus = Status;

        string state = Status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting",
            _ => "Disconnected"
        };
        VisualStateManager.GoToState(this, state, true);
        if (state == "Connected")
        {
            (Resources["DotPulseAnimation"] as Storyboard)?.Begin();
        }
        else
        {
            (Resources["DotPulseAnimation"] as Storyboard)?.Stop();
        }

        
        ConnectButton.Style = Status switch
        {
            ConnectionStatus.Connected => (Style)Application.Current.Resources["ConnectedButtonStyle"],
            ConnectionStatus.Connecting => (Style)Application.Current.Resources["ConnectingButtonStyle"],
            _ => (Style)Application.Current.Resources["DisconnectedButtonStyle"]
        };

        UpdateTelemetryColors();
    }

    private void UpdateBatteryVisuals()
    {
        bool isConnected = Status == ConnectionStatus.Connected;
        var nextText = isConnected ? $"{Battery:F0}%" : "—";
        if (!string.Equals(BatteryText.Text, nextText, StringComparison.Ordinal))
        {
            BatteryText.Text = nextText;
        }

        if (isConnected && !double.IsNaN(_lastRenderedBattery) && Math.Abs(_lastRenderedBattery - Battery) < 0.5)
        {
            return;
        }

        _lastRenderedBattery = Battery;
        
        if (isConnected)
        {
            var brushKey = Battery > 50 ? "SuccessBrush" : (Battery > 20 ? "WarningBrush" : "ErrorBrush");
            BatteryText.Foreground = Application.Current.Resources.TryGetValue(brushKey, out var brush) ? (Brush)brush : BatteryText.Foreground;
            BatteryIcon.Foreground = Application.Current.Resources.TryGetValue(brushKey, out var iconBrush) ? (Brush)iconBrush : BatteryIcon.Foreground;
        }
        else
        {
            BatteryText.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
            BatteryIcon.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
        }
    }

    private void UpdateGpsVisuals()
    {
        bool isConnected = Status == ConnectionStatus.Connected;
        var nextText = isConnected ? (GpsSats > 0 ? $"Fix ({GpsSats})" : "No Fix") : "—";
        if (!string.Equals(GpsText.Text, nextText, StringComparison.Ordinal))
        {
            GpsText.Text = nextText;
        }

        if (isConnected && _lastRenderedSats == GpsSats)
        {
            return;
        }

        _lastRenderedSats = GpsSats;
        
        if (isConnected && GpsSats > 0)
        {
            GpsText.Foreground = (Brush)Application.Current.Resources["InfoBrush"];
            GpsIcon.Foreground = (Brush)Application.Current.Resources["InfoBrush"];
        }
        else
        {
            GpsText.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
            GpsIcon.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
        }
    }

    private void UpdateModeVisuals()
    {
        bool isConnected = Status == ConnectionStatus.Connected;
        var nextMode = isConnected ? Mode : "—";
        if (!string.Equals(ModeText.Text, nextMode, StringComparison.Ordinal))
        {
            ModeText.Text = nextMode;
        }

        if (isConnected && string.Equals(_lastRenderedMode, Mode, StringComparison.Ordinal))
        {
            return;
        }

        _lastRenderedMode = Mode;
        
        if (isConnected)
        {
            ModeText.Foreground = (Brush)Application.Current.Resources["NavStatsBrush"];
            ModeIcon.Foreground = (Brush)Application.Current.Resources["NavStatsBrush"];
        }
        else
        {
            ModeText.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
            ModeIcon.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
        }
    }

    private void UpdateArmedVisuals()
    {
        if (_lastRenderedArmed == IsArmed && Status == _lastVisualStatus)
        {
            return;
        }

        _lastRenderedArmed = IsArmed;
        ArmedBadge.Visibility = (Status == ConnectionStatus.Connected && IsArmed) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateEndpointVisuals()
    {
        if (string.Equals(_lastRenderedEndpoint, Endpoint, StringComparison.Ordinal) && Status == _lastVisualStatus)
        {
            return;
        }

        _lastRenderedEndpoint = Endpoint;
        bool hasEndpoint = !string.IsNullOrEmpty(Endpoint) && Status == ConnectionStatus.Connected;
        EndpointBadge.Visibility = hasEndpoint ? Visibility.Visible : Visibility.Collapsed;
        EndpointText.Text = Endpoint;
    }

    private void UpdateTelemetryColors()
    {
        UpdateBatteryVisuals();
        UpdateGpsVisuals();
        UpdateModeVisuals();
        UpdateArmedVisuals();
        UpdateEndpointVisuals();
        UpdateVoiceVisuals();
    }

    private void UpdateVoiceVisuals()
    {
        if (_lastRenderedVoiceListening == IsVoiceListening &&
            string.Equals(_lastRenderedVoiceStatus, VoiceStatus, StringComparison.Ordinal))
        {
            return;
        }

        _lastRenderedVoiceListening = IsVoiceListening;
        _lastRenderedVoiceStatus = VoiceStatus;

        string text;
        if (IsVoiceListening)
        {
            text = "listening";
        }
        else if (!string.IsNullOrWhiteSpace(VoiceStatus) &&
                 !string.Equals(VoiceStatus, "voice idle", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(VoiceStatus, "idle", StringComparison.OrdinalIgnoreCase))
        {
            text = VoiceStatus;
        }
        else
        {
            text = "idle";
        }

        VoiceStateText.Text = text;

        if (IsVoiceListening)
        {
            VoiceStateText.Foreground = (Brush)Application.Current.Resources["SuccessBrush"];
            VoiceIcon.Foreground = (Brush)Application.Current.Resources["SuccessBrush"];
            return;
        }

        if (string.Equals(text, "voice unavailable", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "voice error", StringComparison.OrdinalIgnoreCase))
        {
            VoiceStateText.Foreground = (Brush)Application.Current.Resources["WarningBrush"];
            VoiceIcon.Foreground = (Brush)Application.Current.Resources["WarningBrush"];
            return;
        }

        VoiceStateText.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
        VoiceIcon.Foreground = (Brush)Application.Current.Resources["MutedForegroundBrush"];
    }
}
