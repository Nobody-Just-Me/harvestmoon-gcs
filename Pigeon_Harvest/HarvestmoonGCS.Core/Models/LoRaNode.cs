using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HarvestmoonGCS.Core.Models
{
    /// <summary>
    /// Model data untuk node LoRa dengan INotifyPropertyChanged
    /// Versi ini lebih lengkap dengan property change notification
    /// </summary>
    public class LoRaNode : INotifyPropertyChanged
    {
        private int _nodeId;
        private string _name = string.Empty;
        private int _batteryPercent;
        private int _rssi;
        private float _snr;
        private double _latitude;
        private double _longitude;
        private float _altitude;
        private float _temperature;
        private float _humidity;
        private float _batteryVoltage;
        private int _hopCount;
        private DateTime _lastUpdate;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int NodeId 
        { 
            get => _nodeId; 
            set { _nodeId = value; OnPropertyChanged(); } 
        }

        public string Name 
        { 
            get => _name; 
            set { _name = value; OnPropertyChanged(); } 
        }

        public int BatteryPercent 
        { 
            get => _batteryPercent; 
            set { _batteryPercent = value; OnPropertyChanged(); } 
        }

        public int RSSI 
        { 
            get => _rssi; 
            set { _rssi = value; OnPropertyChanged(); } 
        }

        public float SNR 
        { 
            get => _snr; 
            set { _snr = value; OnPropertyChanged(); } 
        }

        public double Latitude 
        { 
            get => _latitude; 
            set { _latitude = value; OnPropertyChanged(); OnPropertyChanged(nameof(LocationString)); } 
        }

        public double Longitude 
        { 
            get => _longitude; 
            set { _longitude = value; OnPropertyChanged(); OnPropertyChanged(nameof(LocationString)); } 
        }

        public float Altitude 
        { 
            get => _altitude; 
            set { _altitude = value; OnPropertyChanged(); } 
        }

        public float Temperature 
        { 
            get => _temperature; 
            set { _temperature = value; OnPropertyChanged(); } 
        }

        public float Humidity 
        { 
            get => _humidity; 
            set { _humidity = value; OnPropertyChanged(); } 
        }

        public float BatteryVoltage 
        { 
            get => _batteryVoltage; 
            set { _batteryVoltage = value; OnPropertyChanged(); } 
        }

        public int HopCount 
        { 
            get => _hopCount; 
            set { _hopCount = value; OnPropertyChanged(); } 
        }

        public DateTime LastUpdate 
        { 
            get => _lastUpdate; 
            set 
            { 
                _lastUpdate = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsOnline)); 
                OnPropertyChanged(nameof(StatusColor)); 
            } 
        }

        public bool IsOnline => (DateTime.Now - LastUpdate).TotalSeconds < 10;

        public string StatusColor => IsOnline ? "#00FF7F" : "#808080";

        public string LocationString => $"{Latitude:F6}, {Longitude:F6}";

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
