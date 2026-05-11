using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Helpers;

namespace HarvestmoonGCS.Controls;

public sealed partial class LoadingIndicator : UserControl
{
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingIndicator), 
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty LoadingTextProperty =
        DependencyProperty.Register(nameof(LoadingText), typeof(string), typeof(LoadingIndicator), 
            new PropertyMetadata("Loading..."));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    public LoadingIndicator()
    {
        this.InitializeComponent();
        this.Visibility = Visibility.Collapsed;
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingIndicator indicator)
        {
            if ((bool)e.NewValue)
            {
                indicator.IsHitTestVisible = true;
                AnimationHelper.FadeIn(indicator, 0.2);
            }
            else
            {
                indicator.IsHitTestVisible = false;
                AnimationHelper.FadeOut(indicator, 0.2, () =>
                {
                    indicator.IsHitTestVisible = false;
                });
            }
        }
    }
}
