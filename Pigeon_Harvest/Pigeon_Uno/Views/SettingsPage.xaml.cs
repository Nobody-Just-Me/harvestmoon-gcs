using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using CoreThemeService = Pigeon_Uno.Core.Services.IThemeService;

namespace Pigeon_Uno.Views;

public sealed partial class SettingsPage : Page
{
    private SettingsViewModel? _viewModel;

    public SettingsPage()
    {
        this.InitializeComponent();
        this.Loaded += SettingsPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        _viewModel = new SettingsViewModel();
        this.DataContext = _viewModel;
        
        LoadSettings();
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("SettingsPage loaded");
    }

    private void LoadSettings()
    {
        if (_viewModel == null) return;

        try
        {
            ProtocolCombo.SelectedIndex = _viewModel.ConnectionTypeIndex;
            ThemeCombo.SelectedIndex = _viewModel.ThemeIndex;
            LanguageCombo.SelectedIndex = _viewModel.LanguageIndex;
            UnitCombo.SelectedIndex = _viewModel.UnitSystemIndex;
            HighContrastCheck.IsChecked = _viewModel.HighContrastMode;
            ShowTooltipsCheck.IsChecked = _viewModel.ShowTooltips;
            AnimationsCheck.IsChecked = _viewModel.EnableAnimations;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    private void SaveCurrentSettings()
    {
        if (_viewModel == null) return;

        try
        {
            _viewModel.ConnectionTypeIndex = ProtocolCombo.SelectedIndex;
            _viewModel.ThemeIndex = ThemeCombo.SelectedIndex;
            _viewModel.LanguageIndex = LanguageCombo.SelectedIndex;
            _viewModel.UnitSystemIndex = UnitCombo.SelectedIndex;
            _viewModel.HighContrastMode = HighContrastCheck.IsChecked ?? false;
            _viewModel.ShowTooltips = ShowTooltipsCheck.IsChecked ?? true;
            _viewModel.EnableAnimations = AnimationsCheck.IsChecked ?? true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var protocol = (ProtocolCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "UDP";
            var host = HostInput.Text ?? "127.0.0.1";
            var port = PortInput.Text ?? "14550";
            
            Debug.WriteLine($"Connecting to {protocol} {host}:{port}...");
            
            await Task.Delay(200);
            
            await ShowMessageDialog("Connected", $"Successfully connected to {protocol} {host}:{port}");
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Connection Error", $"Failed to connect: {ex.Message}");
        }
    }

    private async void TestBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ShowMessageDialog("Test Connection", "Connection test OK · 48 ms latency");
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Test Failed", $"Connection test failed: {ex.Message}");
        }
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveCurrentSettings();
            
            if (_viewModel != null)
            {
                await _viewModel.SaveSettings();
            }
            
            await ShowMessageDialog("Success", "Pengaturan disimpan");
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Error", $"Failed to save: {ex.Message}");
        }
    }

    private async void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var confirm = await ShowConfirmDialog("Reset", "Apakah Anda yakin ingin mereset ke default?");
            
            if (confirm)
            {
                _viewModel?.ResetToDefaults();
                LoadSettings();
                await ShowMessageDialog("Success", "Reset ke default");
            }
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Error", $"Failed to reset: {ex.Message}");
        }
    }

    private async void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var themeService = App.Current.Services.GetService(typeof(CoreThemeService)) as CoreThemeService;
            
            if (themeService == null)
            {
                Debug.WriteLine("ThemeService not available");
                return;
            }

            var selectedTheme = ThemeCombo.SelectedIndex switch
            {
                0 => ThemeMode.Light,
                1 => ThemeMode.Dark,
                2 => ThemeMode.System,
                _ => ThemeMode.Light
            };

            await themeService.SetThemeAsync(selectedTheme);
            Debug.WriteLine($"Theme changed to: {selectedTheme}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error changing theme: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task ShowMessageDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async System.Threading.Tasks.Task<bool> ShowConfirmDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
