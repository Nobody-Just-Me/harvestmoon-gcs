using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.ViewModels;

namespace HarvestmoonGCS.Controls;

public sealed partial class LoRaControl : UserControl
{
    public LoRaViewModel ViewModel => (LoRaViewModel)DataContext;

    public LoRaControl()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<LoRaViewModel>();
    }

    private async void ToggleConnection(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ToggleConnectionCommand != null && ViewModel.ToggleConnectionCommand.CanExecute(null))
        {
            await ViewModel.ToggleConnectionCommand.ExecuteAsync(null);
        }
    }
}
