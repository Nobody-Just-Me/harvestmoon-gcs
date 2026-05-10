using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Pigeon_Uno.Views;

public sealed partial class SerialPortSelectionDialog : ContentDialog
{
    public SerialPortConfig Config { get; private set; }
    private List<SerialPortInfo> _availablePorts = new();
    
    public SerialPortSelectionDialog()
    {
        this.InitializeComponent();
        Config = new SerialPortConfig();
        LoadAvailablePorts();
    }
    
    public SerialPortSelectionDialog(SerialPortConfig existingConfig)
    {
        this.InitializeComponent();
        Config = existingConfig;
        LoadAvailablePorts();
        LoadConfiguration();
    }
    
    private void LoadAvailablePorts()
    {
        try
        {
            _availablePorts.Clear();
            
            var portNames = SerialPort.GetPortNames();
            
            foreach (var portName in portNames.OrderBy(p => p))
            {
                var portInfo = new SerialPortInfo
                {
                    PortName = portName,
                    Description = $"Serial Port {portName}",
                    Manufacturer = "Unknown"
                };
                
                // Try to get more details (Windows only)
                try
                {
                    // This would use WMI on Windows to get detailed info
                    // For now, just use basic info
                }
                catch
                {
                    // Ignore errors getting detailed info
                }
                
                _availablePorts.Add(portInfo);
            }
            
            PortComboBox.ItemsSource = _availablePorts;
            
            if (_availablePorts.Count > 0 && string.IsNullOrEmpty(Config.PortName))
            {
                PortComboBox.SelectedIndex = 0;
            }
            else if (!string.IsNullOrEmpty(Config.PortName))
            {
                var existing = _availablePorts.FirstOrDefault(p => p.PortName == Config.PortName);
                if (existing != null)
                {
                    PortComboBox.SelectedItem = existing;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading ports: {ex.Message}");
        }
    }
    
    private void LoadConfiguration()
    {
        // Baud Rate
        var baudRateItem = BaudRateComboBox.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(i => i.Content.ToString() == Config.BaudRate.ToString());
        if (baudRateItem != null)
        {
            BaudRateComboBox.SelectedItem = baudRateItem;
        }
        
        // Data Bits
        var dataBitsItem = DataBitsComboBox.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(i => i.Content.ToString() == Config.DataBits.ToString());
        if (dataBitsItem != null)
        {
            DataBitsComboBox.SelectedItem = dataBitsItem;
        }
        
        // Parity
        ParityComboBox.SelectedIndex = (int)Config.Parity;
        
        // Stop Bits
        StopBitsComboBox.SelectedIndex = Config.StopBits == StopBits.One ? 0 :
                                          Config.StopBits == StopBits.OnePointFive ? 1 : 2;
        
        // Advanced
        DtrEnableCheck.IsChecked = Config.DtrEnable;
        RtsEnableCheck.IsChecked = Config.RtsEnable;
        AutoReconnectCheck.IsChecked = Config.AutoReconnect;
        ReadTimeoutTextBox.Text = Config.ReadTimeout.ToString();
        WriteTimeoutTextBox.Text = Config.WriteTimeout.ToString();
    }
    
    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadAvailablePorts();
    }
    
    private void PortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PortComboBox.SelectedItem is SerialPortInfo portInfo)
        {
            PortDetailsPanel.Visibility = Visibility.Visible;
            PortNameText.Text = $"Port: {portInfo.PortName}";
            PortDescriptionText.Text = $"Description: {portInfo.Description}";
            PortManufacturerText.Text = $"Manufacturer: {portInfo.Manufacturer}";
        }
        else
        {
            PortDetailsPanel.Visibility = Visibility.Collapsed;
        }
    }
    
    public void SaveConfiguration()
    {
        // Port
        if (PortComboBox.SelectedItem is SerialPortInfo portInfo)
        {
            Config.PortName = portInfo.PortName;
        }
        
        // Baud Rate
        if (BaudRateComboBox.SelectedItem is ComboBoxItem baudItem)
        {
            Config.BaudRate = int.Parse(baudItem.Content.ToString() ?? "57600");
        }
        
        // Data Bits
        if (DataBitsComboBox.SelectedItem is ComboBoxItem dataItem)
        {
            Config.DataBits = int.Parse(dataItem.Content.ToString() ?? "8");
        }
        
        // Parity
        Config.Parity = (Parity)ParityComboBox.SelectedIndex;
        
        // Stop Bits
        Config.StopBits = StopBitsComboBox.SelectedIndex switch
        {
            0 => StopBits.One,
            1 => StopBits.OnePointFive,
            2 => StopBits.Two,
            _ => StopBits.One
        };
        
        // Advanced
        Config.DtrEnable = DtrEnableCheck.IsChecked ?? true;
        Config.RtsEnable = RtsEnableCheck.IsChecked ?? true;
        Config.AutoReconnect = AutoReconnectCheck.IsChecked ?? true;
        
        if (int.TryParse(ReadTimeoutTextBox.Text, out var readTimeout))
        {
            Config.ReadTimeout = readTimeout;
        }
        
        if (int.TryParse(WriteTimeoutTextBox.Text, out var writeTimeout))
        {
            Config.WriteTimeout = writeTimeout;
        }
    }
}

/// <summary>
/// Serial port information.
/// </summary>
public class SerialPortInfo
{
    public string PortName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Manufacturer { get; set; } = "";
}

/// <summary>
/// Serial port configuration.
/// </summary>
public class SerialPortConfig
{
    public string PortName { get; set; } = "";
    public int BaudRate { get; set; } = 57600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public bool DtrEnable { get; set; } = true;
    public bool RtsEnable { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public int ReadTimeout { get; set; } = 500;
    public int WriteTimeout { get; set; } = 500;
}
