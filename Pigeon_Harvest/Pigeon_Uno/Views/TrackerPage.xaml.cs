using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Pigeon_Uno.ViewModels;

namespace Pigeon_Uno.Views;

public sealed partial class TrackerPage : Page
{
    public TrackerViewModel ViewModel => (TrackerViewModel)DataContext;

    public TrackerPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<TrackerViewModel>();
    }

    public void OnPageActivated()
    {
        InvalidateArrange();
    }
}
