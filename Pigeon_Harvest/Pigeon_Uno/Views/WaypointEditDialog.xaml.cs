using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Views
{
    public sealed partial class WaypointEditDialog : ContentDialog
    {
        private WaypointData _originalWaypoint;
        public WaypointData ResultWaypoint { get; private set; }

        public WaypointEditDialog(WaypointData waypoint)
        {
            this.InitializeComponent();
            _originalWaypoint = waypoint;
            LoadData();
        }

        private void LoadData()
        {
            if (_originalWaypoint == null) return;

            SequenceTextBox.Text = _originalWaypoint.Sequence.ToString();
            LatitudeTextBox.Text = _originalWaypoint.Latitude.ToString("F7", CultureInfo.InvariantCulture);
            LongitudeTextBox.Text = _originalWaypoint.Longitude.ToString("F7", CultureInfo.InvariantCulture);
            AltitudeTextBox.Text = _originalWaypoint.Altitude.ToString("F2", CultureInfo.InvariantCulture);

            foreach (ComboBoxItem item in CommandComboBox.Items)
            {
                if (item.Tag is string tagStr && int.TryParse(tagStr, out int cmdId))
                {
                    if (cmdId == (int)_originalWaypoint.Command)
                    {
                        CommandComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            if (CommandComboBox.SelectedIndex == -1) CommandComboBox.SelectedIndex = 0;

            Param1TextBox.Text = _originalWaypoint.Param1.ToString("F2", CultureInfo.InvariantCulture);
            Param2TextBox.Text = _originalWaypoint.Param2.ToString("F2", CultureInfo.InvariantCulture);
            Param3TextBox.Text = _originalWaypoint.Param3.ToString("F2", CultureInfo.InvariantCulture);
            Param4TextBox.Text = _originalWaypoint.Param4.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            ErrorTextBlock.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = "";

            if (!double.TryParse(LatitudeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) || lat < -90 || lat > 90)
            {
                ShowError("Invalid Latitude. Must be between -90 and 90.");
                return;
            }

            if (!double.TryParse(LongitudeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon) || lon < -180 || lon > 180)
            {
                ShowError("Invalid Longitude. Must be between -180 and 180.");
                return;
            }

            if (!double.TryParse(AltitudeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double alt))
            {
                ShowError("Invalid Altitude.");
                return;
            }

            double p1 = 0, p2 = 0, p3 = 0, p4 = 0;
            double.TryParse(Param1TextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out p1);
            double.TryParse(Param2TextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out p2);
            double.TryParse(Param3TextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out p3);
            double.TryParse(Param4TextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out p4);

            WaypointCommand command = WaypointCommand.Waypoint;
            if (CommandComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tagStr && int.TryParse(tagStr, out int cmdId))
            {
                command = (WaypointCommand)cmdId;
            }

            ResultWaypoint = new WaypointData
            {
                Sequence = _originalWaypoint.Sequence,
                Latitude = lat,
                Longitude = lon,
                Altitude = alt,
                Command = command,
                Param1 = p1,
                Param2 = p2,
                Param3 = p3,
                Param4 = p4,
                IsCurrent = _originalWaypoint.IsCurrent
            };

            args.Cancel = false;
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void CommandComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CommandComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int cmdId))
            {
                UpdateParamLabels((WaypointCommand)cmdId);
            }
        }

        private void UpdateParamLabels(WaypointCommand command)
        {
            Param1Label.Text = "Param 1:";
            Param2Label.Text = "Param 2:";
            Param3Label.Text = "Param 3:";
            Param4Label.Text = "Param 4:";

            switch (command)
            {
                case WaypointCommand.Loiter:
                case WaypointCommand.LoiterTime:
                    Param1Label.Text = "Time (s):";
                    break;
                case WaypointCommand.LoiterTurns:
                    Param1Label.Text = "Turns:";
                    break;
                case WaypointCommand.TakeOff:
                    Param1Label.Text = "Pitch:";
                    break;
                case WaypointCommand.NavVtolTakeoff:
                    Param1Label.Text = "Unused:";
                    Param2Label.Text = "Unused:";
                    Param3Label.Text = "Unused:";
                    Param4Label.Text = "Yaw (deg):";
                    break;
                case WaypointCommand.NavVtolLand:
                    Param1Label.Text = "Approach (0/1):";
                    Param2Label.Text = "Unused:";
                    Param3Label.Text = "Unused:";
                    Param4Label.Text = "Yaw (deg):";
                    break;
                case WaypointCommand.DoVtolTransition:
                    Param1Label.Text = "State (3=MC,4=FW):";
                    Param2Label.Text = "Unused:";
                    Param3Label.Text = "Unused:";
                    Param4Label.Text = "Unused:";
                    break;
                case WaypointCommand.DoChangeSpeed:
                    Param1Label.Text = "Speed (m/s):";
                    Param2Label.Text = "Throttle (%):";
                    break;
            }
        }
    }
}
