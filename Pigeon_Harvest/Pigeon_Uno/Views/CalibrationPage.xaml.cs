using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Pigeon_Uno.Core.ViewModels;

namespace Pigeon_Uno.Views;

public sealed partial class CalibrationPage : Page
{
    public CalibrationViewModel ViewModel => (CalibrationViewModel)DataContext;

    public CalibrationPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<CalibrationViewModel>();
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            if (AccelPanel != null) AccelPanel.Visibility = tag == "Accel" ? Visibility.Visible : Visibility.Collapsed;
            if (CompassPanel != null) CompassPanel.Visibility = tag == "Compass" ? Visibility.Visible : Visibility.Collapsed;
            if (RadioPanel != null) RadioPanel.Visibility = tag == "Radio" ? Visibility.Visible : Visibility.Collapsed;
            if (FlightModesPanel != null) FlightModesPanel.Visibility = tag == "FlightModes" ? Visibility.Visible : Visibility.Collapsed;
            if (ServoOutputPanel != null) ServoOutputPanel.Visibility = tag == "ServoOutput" ? Visibility.Visible : Visibility.Collapsed;
            if (MotorPanel != null) MotorPanel.Visibility = tag == "MotorTest" ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
