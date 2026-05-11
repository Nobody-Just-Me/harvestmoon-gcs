using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HarvestmoonGCS.Controls
{
    public sealed partial class AirSpeedIndicator : UserControl
    {
        public AirSpeedIndicator()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty SpeedProperty =
            DependencyProperty.Register(nameof(Speed), typeof(double), typeof(AirSpeedIndicator), new PropertyMetadata(0.0, OnSpeedChanged));

        public double Speed
        {
            get => (double)GetValue(SpeedProperty);
            set => SetValue(SpeedProperty, value);
        }

        private static void OnSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AirSpeedIndicator control)
            {
                control.NeedleRotate.Angle = (double)e.NewValue;
            }
        }
    }
}
