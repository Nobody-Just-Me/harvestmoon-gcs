using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pigeon_Uno.ViewModels;

namespace Pigeon_Uno.Views;

public sealed partial class TlogControl : UserControl
{
    public TlogViewModel? ViewModel { get; private set; }

    public TlogControl()
    {
        this.InitializeComponent();
        
        // Set DataContext from parent or get from DI
        this.Loaded += (s, e) =>
        {
            // Try to get from parent DataContext first
            if (this.DataContext is TlogViewModel vm)
            {
                ViewModel = vm;
            }
            else
            {
                // Get from DI as fallback
                ViewModel = App.GetService<TlogViewModel>();
                this.DataContext = ViewModel;
            }
        };
    }

    private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ViewModel?.OnSliderPressed();
    }

    private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ViewModel?.OnSliderReleased();
    }
}
