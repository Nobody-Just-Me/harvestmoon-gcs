using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Model untuk servo output channel
/// </summary>
public class ServoOutputItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _channel;
    private int _pwmValue;
    private string _label = string.Empty;
    private ServoFunction _function;

    public int Channel
    {
        get => _channel;
        set => SetProperty(ref _channel, value);
    }

    public int PWMValue
    {
        get => _pwmValue;
        set => SetProperty(ref _pwmValue, value);
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public ServoFunction Function
    {
        get => _function;
        set
        {
            SetProperty(ref _function, value);
            // Update label based on function
            Label = value.ToString();
        }
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
