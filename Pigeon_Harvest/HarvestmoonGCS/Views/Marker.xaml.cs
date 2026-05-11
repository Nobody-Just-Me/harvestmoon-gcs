using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Views;

/// <summary>
/// Interaction logic for Marker.xaml
/// </summary>
public sealed partial class Marker : UserControl
{
    public Marker()
    {
        InitializeComponent();
    }

    public Marker(WaypointData waypoint) : this()
    {
        TheWaypointData = waypoint;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (TheWaypointData == null) return;

        wp_name_.Text = $"#{TheWaypointData.Sequence} - {TheWaypointData.Command}";
        wp_lat_.Text = TheWaypointData.Latitude.ToString("0.#########");
        wp_longt_.Text = TheWaypointData.Longitude.ToString("0.#########");
    }

    public void SetProperties(double lat, double lng)
    {
        if (TheWaypointData != null)
        {
            TheWaypointData.Latitude = lat;
            TheWaypointData.Longitude = lng;
        }
        wp_lat_.Text = lat.ToString("0.#########");
        wp_longt_.Text = lng.ToString("0.#########");
    }

    /// <summary>
    /// Gets the waypoint data for this marker
    /// </summary>
    public WaypointData TheWaypointData { get; private set; }

    private void wp_lat_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(wp_lat_.Text, out double lat) && TheWaypointData != null)
        {
            TheWaypointData.Latitude = lat;
        }
    }

    private void wp_longt_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(wp_longt_.Text, out double lng) && TheWaypointData != null)
        {
            TheWaypointData.Longitude = lng;
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        wp_grid_.Children.Clear();
        wp_grid_.RowDefinitions.Clear();
        wp_grid_.ColumnDefinitions.Clear();
    }

    private void wp_command_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (wp_command_.SelectedItem is ComboBoxItem selectedItem && TheWaypointData != null)
        {
            string selectedContent = selectedItem.Content?.ToString() ?? "";
            
            // Update command based on selection
            TheWaypointData.Command = selectedContent switch
            {
                "Waypoint" => WaypointCommand.Waypoint,
                "Takeoff" => WaypointCommand.TakeOff,
                "Land" => WaypointCommand.Land,
                "SetHome" => WaypointCommand.SetHome,
                _ => WaypointCommand.Waypoint
            };
            
            UpdateUI();
        }
    }
}
