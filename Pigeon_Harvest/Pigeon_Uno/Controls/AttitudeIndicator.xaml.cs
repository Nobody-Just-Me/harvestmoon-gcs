using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Pigeon_Uno.Controls
{
    public sealed partial class AttitudeIndicator : UserControl
    {
        public AttitudeIndicator()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty PitchProperty =
            DependencyProperty.Register(nameof(Pitch), typeof(double), typeof(AttitudeIndicator), new PropertyMetadata(0.0, OnPitchChanged));

        public double Pitch
        {
            get => (double)GetValue(PitchProperty);
            set => SetValue(PitchProperty, value);
        }

        public static readonly DependencyProperty RollProperty =
            DependencyProperty.Register(nameof(Roll), typeof(double), typeof(AttitudeIndicator), new PropertyMetadata(0.0, OnRollChanged));

        public double Roll
        {
            get => (double)GetValue(RollProperty);
            set => SetValue(RollProperty, value);
        }

        private static void OnPitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AttitudeIndicator control)
            {
                double pitch = (double)e.NewValue;
                control.PitchTranslate.Y = pitch * 4.0; 
            }
        }

        private static void OnRollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AttitudeIndicator control)
            {
                control.RollRotate.Angle = -(double)e.NewValue;
            }
        }
    }
}
