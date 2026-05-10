using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pigeon_Uno.ViewModels;
using Pigeon_Uno.Views;
using System;
using Windows.UI;

namespace Pigeon_Uno;

public sealed partial class MainPage : Page
{
    private DispatcherTimer _clockTimer;
    private DispatcherTimer _flightTimer;
    private TimeSpan _flightTime;
    private bool _isFlightActive;
    private string _currentLanguage = "ID";
    private Button _activeTabButton;

    public MainPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<MainViewModel>();
        
        InitializeTimers();
        SetLanguage("ID");
        SetActiveTab(FlightButton);
        
        ContentFrame.Navigate(typeof(FlightPage));
    }

    private void InitializeTimers()
    {
        _clockTimer = new DispatcherTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();

        _flightTimer = new DispatcherTimer();
        _flightTimer.Interval = TimeSpan.FromSeconds(1);
        _flightTimer.Tick += FlightTimer_Tick;
        _flightTime = TimeSpan.Zero;
    }

    private void ClockTimer_Tick(object sender, object e)
    {
        DigitalClock.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void FlightTimer_Tick(object sender, object e)
    {
        if (_isFlightActive)
        {
            _flightTime = _flightTime.Add(TimeSpan.FromSeconds(1));
            FlightTimeText.Text = _flightTime.ToString(@"hh\:mm\:ss");
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            SetActiveTab(btn);
            
            switch (tag)
            {
                case "Flight":
                    ContentFrame.Navigate(typeof(FlightPage));
                    break;
                case "Map":
                    ContentFrame.Navigate(typeof(MapPage));
                    break;
                case "Stats":
                    ContentFrame.Navigate(typeof(StatsPage));
                    break;
                case "Tracker":
                    ContentFrame.Navigate(typeof(TrackerPage));
                    break;
                case "Calibration":
                    ContentFrame.Navigate(typeof(CalibrationPage));
                    break;
                case "LoRa":
                    ContentFrame.Navigate(typeof(LoRaPage));
                    break;
                case "TLOG":
                    ContentFrame.Navigate(typeof(TlogPage));
                    break;
            }
        }
    }

    private void SetActiveTab(Button activeButton)
    {
        FlightIndicator.Opacity = 0;
        MapIndicator.Opacity = 0;
        StatsIndicator.Opacity = 0;
        TrackerIndicator.Opacity = 0;
        CalibrationIndicator.Opacity = 0;
        TlogIndicator.Opacity = 0;
        LoRaIndicator.Opacity = 0;

        _activeTabButton = activeButton;
        if (activeButton == FlightButton)
            FlightIndicator.Opacity = 1;
        else if (activeButton == MapButton)
            MapIndicator.Opacity = 1;
        else if (activeButton == StatsButton)
            StatsIndicator.Opacity = 1;
        else if (activeButton == TrackerButton)
            TrackerIndicator.Opacity = 1;
        else if (activeButton == CalibrationButton)
            CalibrationIndicator.Opacity = 1;
        else if (activeButton == TlogButton)
            TlogIndicator.Opacity = 1;
        else if (activeButton == LoRaButton)
            LoRaIndicator.Opacity = 1;
    }

    private void NavButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button btn && btn != _activeTabButton)
        {
            btn.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        }
    }

    private void NavButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button btn && btn != _activeTabButton)
        {
            btn.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string language)
        {
            SetLanguage(language);
        }
    }

    private void SetLanguage(string language)
    {
        _currentLanguage = language;
        
        IndonesiaFlag.Background = language == "ID" ? 
            new SolidColorBrush(Color.FromArgb(255, 0, 191, 255)) : 
            new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        EnglishFlag.Background = language == "EN" ? 
            new SolidColorBrush(Color.FromArgb(255, 0, 191, 255)) : 
            new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        if (language == "ID")
        {
            LanguageLabel.Text = "Bahasa :";
            FlightLabel.Text = "Terbang";
            MapLabel.Text = "Peta";
            StatsLabel.Text = "Statistik";
            TrackerLabel.Text = "Pelacak";
            CalibrationLabel.Text = "Kalibrasi";
            TlogLabel.Text = "TLOG";
            LoRaLabel.Text = "LoRa";
            ExitLabel.Text = "KELUAR";
        }
        else
        {
            LanguageLabel.Text = "Language :";
            FlightLabel.Text = "Flight";
            MapLabel.Text = "Map";
            StatsLabel.Text = "Statistics";
            TrackerLabel.Text = "Tracker";
            CalibrationLabel.Text = "Calibration";
            TlogLabel.Text = "TLOG";
            LoRaLabel.Text = "LoRa";
            ExitLabel.Text = "EXIT";
        }
    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Content = _currentLanguage == "ID" ? 
                "Apakah Anda yakin ingin keluar dari aplikasi?" : 
                "Are you sure you want to exit the application?",
            PrimaryButtonText = _currentLanguage == "ID" ? "Ya" : "Yes",
            SecondaryButtonText = _currentLanguage == "ID" ? "Tidak" : "No",
            DefaultButton = ContentDialogButton.Secondary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            Application.Current.Exit();
        }
    }

    private void ContentFrame_NavigationFailed(object sender, Microsoft.UI.Xaml.Navigation.NavigationFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Navigation failed: {e.Exception?.Message}");
    }

    public void UpdateConnectionStatus(bool wahanaConnected, bool trackerConnected)
    {
        if (wahanaConnected && trackerConnected)
        {
            StatusText.Text = "ONLINE";
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0));
        }
        else if (wahanaConnected)
        {
            StatusText.Text = "WAHANA";
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0));
        }
        else if (trackerConnected)
        {
            StatusText.Text = "TRACKER";
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0));
        }
        else
        {
            StatusText.Text = "OFFLINE";
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 150, 150));
        }
    }

    public void UpdateSignalStrength(int percentage)
    {
        SignalText.Text = $"{percentage}%";
    }

    public void UpdateBatteryLevel(int percentage)
    {
        BatteryText.Text = $"{percentage}%";
    }

    public void StartFlightTimer()
    {
        _isFlightActive = true;
        _flightTime = TimeSpan.Zero;
        _flightTimer.Start();
    }

    public void StopFlightTimer()
    {
        _isFlightActive = false;
        _flightTimer.Stop();
    }

    public void ResetFlightTimer()
    {
        _isFlightActive = false;
        _flightTime = TimeSpan.Zero;
        FlightTimeText.Text = "00:00:00";
        _flightTimer.Stop();
    }
}
