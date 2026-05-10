using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Pigeon_Uno.ViewModels;

namespace Pigeon_Uno.Views;

public sealed partial class LoRaPage : Page
{
    public LoRaViewModel ViewModel => (LoRaViewModel)DataContext;

    public LoRaPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<LoRaViewModel>();
    }

    public void OnPageActivated()
    {
        InvalidateArrange();
    }

    private void CommandInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SendCommand();
        }
    }

    private void SendCommand_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SendCommand();
    }

    private void SendCommand()
    {
        var text = CommandInputBox?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ViewModel.SendRawCommand(text.Trim());
        CommandInputBox.Text = string.Empty;
    }
}
