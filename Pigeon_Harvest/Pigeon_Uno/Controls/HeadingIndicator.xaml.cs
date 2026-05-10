using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Pigeon_Uno.Controls
{
    public sealed partial class HeadingIndicator : UserControl
    {
        public HeadingIndicator()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty HeadingProperty =
            DependencyProperty.Register(nameof(Heading), typeof(double), typeof(HeadingIndicator), new PropertyMetadata(0.0, OnHeadingChanged));

        public double Heading
        {
            get => (double)GetValue(HeadingProperty);
            set => SetValue(HeadingProperty, value);
        }

        private static void OnHeadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeadingIndicator control)
            {
                control.HeadingRotate.Angle = -(double)e.NewValue;
            }
        }
    }
}
