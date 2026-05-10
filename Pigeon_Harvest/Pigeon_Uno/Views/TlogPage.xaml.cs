using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Pigeon_Uno.ViewModels;

namespace Pigeon_Uno.Views;

public sealed partial class TlogPage : Page
{
    public TlogViewModel ViewModel => (TlogViewModel)DataContext;

    public TlogPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<TlogViewModel>();
    }
}
