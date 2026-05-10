using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Pigeon_Uno.Controls
{
    public sealed partial class StatusBar : UserControl
    {
        public StatusBar()
        {
            this.InitializeComponent();
        }

        public void UpdateConnectionStatus(bool isOnline)
        {
            if (isOnline)
            {
                txtConnectionStatus.Text = "ONLINE";
            }
            else
            {
                txtConnectionStatus.Text = "OFFLINE";
            }
        }

        public void UpdateSignal(int signalPercent)
        {
            txtSignalValue.Text = $"{signalPercent}%";
        }

        public void UpdateFlightTime(TimeSpan flightTime)
        {
            txtFlightTime.Text = flightTime.ToString(@"hh\:mm\:ss");
        }

        public void UpdateBattery(double voltage)
        {
            txtBattery.Text = $"{voltage:F1}V";
        }
    }
}
