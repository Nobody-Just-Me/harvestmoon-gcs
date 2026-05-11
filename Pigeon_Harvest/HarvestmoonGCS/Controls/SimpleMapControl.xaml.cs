using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Controls
{
    public sealed partial class SimpleMapControl : UserControl
    {
        private double _currentLat = -7.2754; // Surabaya default
        private double _currentLon = 112.7947;
        private int _currentZoom = 15;
        private bool _isMapLoaded = false;

        public SimpleMapControl()
        {
            this.InitializeComponent();
            this.Loaded += SimpleMapControl_Loaded;
        }

        private async void SimpleMapControl_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeMapAsync();
        }

        private async Task InitializeMapAsync()
        {
            try
            {
                mapStatusText.Text = "Loading map...";
                
                // Try to initialize WebView2 with OpenStreetMap
                await Task.Delay(500); // Simulate loading
                
                var htmlContent = GenerateMapHtml();
                
                try
                {
                    mapWebView.NavigateToString(htmlContent);
                    // Wait a bit for navigation
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    // WebView2 might not be available, show placeholder
                    mapStatusText.Text = $"Map display using placeholder\n(WebView2 not available)\n\nCenter: {_currentLat:F4}, {_currentLon:F4}";
                    mapLoadingRing.IsActive = false;
                    _isMapLoaded = true;
                }
            }
            catch (Exception ex)
            {
                mapStatusText.Text = $"Map initialization error\nUsing placeholder mode\n\n{ex.Message}";
                mapLoadingRing.IsActive = false;
            }
        }

        private void MapWebView_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                mapPlaceholder.Visibility = Visibility.Collapsed;
                mapWebView.Visibility = Visibility.Visible;
                mapOverlay.Visibility = Visibility.Visible;
                _isMapLoaded = true;
            }
            else
            {
                mapStatusText.Text = "Map loaded in placeholder mode\nClick controls to interact";
                mapLoadingRing.IsActive = false;
            }
        }

        private string GenerateMapHtml()
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Map</title>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        body {{ margin: 0; padding: 0; }}
        #map {{ width: 100vw; height: 100vh; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map = L.map('map').setView([{_currentLat}, {_currentLon}], {_currentZoom});
        
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            attribution: '© OpenStreetMap contributors',
            maxZoom: 19
        }}).addTo(map);
        
        // Add vehicle marker
        var vehicleIcon = L.divIcon({{
            className: 'vehicle-marker',
            html: '<div style=""background: #FF4444; width: 30px; height: 30px; border-radius: 50%; border: 2px solid white; display: flex; align-items: center; justify-content: center; color: white; font-size: 16px;"">✈</div>',
            iconSize: [30, 30],
            iconAnchor: [15, 15]
        }});
        
        var vehicleMarker = L.marker([{_currentLat}, {_currentLon}], {{icon: vehicleIcon}}).addTo(map);
        
        // Expose functions to C#
        window.updateVehiclePosition = function(lat, lon) {{
            vehicleMarker.setLatLng([lat, lon]);
            map.panTo([lat, lon]);
        }};
        
        window.setMapCenter = function(lat, lon, zoom) {{
            map.setView([lat, lon], zoom || map.getZoom());
        }};
        
        window.zoomIn = function() {{
            map.zoomIn();
        }};
        
        window.zoomOut = function() {{
            map.zoomOut();
        }};
    </script>
</body>
</html>";
        }

        // Public methods for external control
        public void SetCenter(double latitude, double longitude, int? zoom = null)
        {
            _currentLat = latitude;
            _currentLon = longitude;
            if (zoom.HasValue)
                _currentZoom = zoom.Value;

            txtMapCoordinates.Text = $"Lat: {latitude:F4}, Lon: {longitude:F4}";
            txtMapZoom.Text = $"Zoom: {_currentZoom}";

            if (_isMapLoaded && mapWebView.Visibility == Visibility.Visible)
            {
                try
                {
                    _ = mapWebView.ExecuteScriptAsync($"setMapCenter({latitude}, {longitude}, {_currentZoom})");
                }
                catch { }
            }
        }

        public void UpdateVehiclePosition(double latitude, double longitude)
        {
            _currentLat = latitude;
            _currentLon = longitude;

            txtMapCoordinates.Text = $"Lat: {latitude:F4}, Lon: {longitude:F4}";

            if (_isMapLoaded && mapWebView.Visibility == Visibility.Visible)
            {
                try
                {
                    _ = mapWebView.ExecuteScriptAsync($"updateVehiclePosition({latitude}, {longitude})");
                }
                catch { }
            }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom++;
            txtMapZoom.Text = $"Zoom: {_currentZoom}";

            if (_isMapLoaded && mapWebView.Visibility == Visibility.Visible)
            {
                try
                {
                    _ = mapWebView.ExecuteScriptAsync("zoomIn()");
                }
                catch { }
            }
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom--;
            txtMapZoom.Text = $"Zoom: {_currentZoom}";

            if (_isMapLoaded && mapWebView.Visibility == Visibility.Visible)
            {
                try
                {
                    _ = mapWebView.ExecuteScriptAsync("zoomOut()");
                }
                catch { }
            }
        }

        private void BtnCenterMap_Click(object sender, RoutedEventArgs e)
        {
            SetCenter(_currentLat, _currentLon, _currentZoom);
        }

        // Properties for binding
        public double Latitude
        {
            get => _currentLat;
            set => SetCenter(value, _currentLon);
        }

        public double Longitude
        {
            get => _currentLon;
            set => SetCenter(_currentLat, value);
        }

        public int ZoomLevel
        {
            get => _currentZoom;
            set
            {
                _currentZoom = value;
                SetCenter(_currentLat, _currentLon, value);
            }
        }
    }
}
