using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Controls
{
    /// <summary>
    /// Waypoint item control for displaying waypoint information in the dock
    /// </summary>
    public sealed partial class WaypointItem : UserControl
    {
        public WaypointData Waypoint { get; }

        public WaypointItem(WaypointData waypoint)
        {
            Waypoint = waypoint;
            InitializeComponent();
            DataContext = waypoint;
        }

        private void InitializeComponent()
        {
            // Create simple UI for waypoint item
            var grid = new Grid
            {
                Height = 40,
                Margin = new Thickness(5, 2, 5, 2),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0x2D, 0x2D, 0x30))
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            // Sequence number
            var sequenceText = new TextBlock
            {
                Text = Waypoint.Sequence.ToString(),
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))
            };
            Grid.SetColumn(sequenceText, 0);

            // Coordinates
            var coordsText = new TextBlock
            {
                Text = $"{Waypoint.Latitude:F6}, {Waypoint.Longitude:F6}",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC))
            };
            Grid.SetColumn(coordsText, 1);

            // Altitude
            var altText = new TextBlock
            {
                Text = $"{Waypoint.Altitude:F0}m",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0x00, 0xFF, 0x00))
            };
            Grid.SetColumn(altText, 2);

            grid.Children.Add(sequenceText);
            grid.Children.Add(coordsText);
            grid.Children.Add(altText);

            Content = grid;
        }
    }
}
