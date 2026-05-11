using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HarvestmoonGCS.Core.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    // Remove duplicate SetProperty since ObservableObject already has it
}
