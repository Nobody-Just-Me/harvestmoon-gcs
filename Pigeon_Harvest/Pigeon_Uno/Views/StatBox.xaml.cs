using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Pigeon_Uno.Views;

public sealed partial class StatBox : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(StatBox), new PropertyMetadata(string.Empty, (d, e) => ((StatBox)d).LabelText.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(StatBox), new PropertyMetadata(string.Empty, (d, e) => ((StatBox)d).ValueText.Text = (string)e.NewValue));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public StatBox()
    {
        this.InitializeComponent();
    }
}
