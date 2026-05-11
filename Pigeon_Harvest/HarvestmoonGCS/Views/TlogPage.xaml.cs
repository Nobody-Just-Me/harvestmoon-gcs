using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.ViewModels;

namespace HarvestmoonGCS.Views;

public sealed partial class TlogPage : Page
{
    public TlogViewModel ViewModel => (TlogViewModel)DataContext;

    public TlogPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<TlogViewModel>();
    }
}
