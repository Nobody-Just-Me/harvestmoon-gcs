using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Models;
using HarvestmoonGCS.Services;
using HarvestmoonGCS.ViewModels;

namespace HarvestmoonGCS.Views;

public sealed partial class TlogPage : Page
{
    public TlogViewModel ViewModel => (TlogViewModel)DataContext;
    private readonly IMavLinkService? _mavLinkService;
    private readonly IncidentTimelineService? _timelineService;
    private bool _replayMapCentered;
    private DateTime _lastReplayTimelineEvent = DateTime.MinValue;

    public TlogPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<TlogViewModel>();
        _mavLinkService = App.Current.Services.GetService<IMavLinkService>();
        _timelineService = App.Current.Services.GetService<IncidentTimelineService>();
        Loaded += TlogPage_Loaded;
        Unloaded += TlogPage_Unloaded;
    }

    private void TlogPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_mavLinkService != null)
        {
            _mavLinkService.TelemetryReceived -= OnReplayTelemetryReceived;
            _mavLinkService.TelemetryReceived += OnReplayTelemetryReceived;
        }

        ReplayMap.SetCenter(-7.2754, 112.7947, 15);
    }

    private void TlogPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_mavLinkService != null)
        {
            _mavLinkService.TelemetryReceived -= OnReplayTelemetryReceived;
        }
    }

    private void OnReplayTelemetryReceived(object? sender, FlightData data)
    {
        var lat = data.GPS.Latitude / 10_000_000.0;
        var lon = data.GPS.Longitude / 10_000_000.0;
        if (lat is < -90 or > 90 || lon is < -180 or > 180 || (lat == 0 && lon == 0))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            ReplayMap.UpdateVehiclePosition(lat, lon);
            if (!_replayMapCentered)
            {
                ReplayMap.SetCenter(lat, lon, 16);
                _replayMapCentered = true;
            }

            ReplayMapStatusText.Text = $"Replay {lat:F6}, {lon:F6} · ALT {data.AltitudeFloat:F1} m";
            if ((DateTime.UtcNow - _lastReplayTimelineEvent).TotalSeconds >= 5)
            {
                _lastReplayTimelineEvent = DateTime.UtcNow;
                _timelineService?.Add("replay", $"TLOG replay position {lat:F6}, {lon:F6}", "info");
            }
        });
    }
}
