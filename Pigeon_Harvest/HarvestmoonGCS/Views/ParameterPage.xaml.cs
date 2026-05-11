using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace HarvestmoonGCS.Views;

public sealed partial class ParameterPage : Page
{
    private readonly IParameterService _parameterService;
    private readonly IMavLinkService _mavLinkService;
    private List<Parameter> _allParameters = new();
    private List<Parameter> _filteredParameters = new();
    
    public ParameterPage()
    {
        this.InitializeComponent();
        
        // Get services from DI container
        _parameterService = App.GetService<IParameterService>();
        _mavLinkService = App.GetService<IMavLinkService>();
        
        // Subscribe to events
        _parameterService.ParameterReceived += OnParameterReceived;
        _parameterService.ParameterUpdated += OnParameterUpdated;
        _parameterService.LoadingProgressChanged += OnLoadingProgressChanged;
        _parameterService.ErrorOccurred += OnErrorOccurred;
        _mavLinkService.ConnectionStatusChanged += OnConnectionStatusChanged;
        
        // Initialize UI
        UpdateConnectionStatus();
        ShowEmptyState();

        SizeChanged += ParameterPage_SizeChanged;
        Loaded += (_, _) => ApplyTabletLayout(ActualWidth);
    }

    private void ParameterPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyTabletLayout(e.NewSize.Width);
    }

    private void ApplyTabletLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var compact = width < 720;
        ParameterHeaderBorder.Padding = compact ? new Thickness(10, 8, 10, 8) : new Thickness(14, 10, 14, 10);
        ParameterFilterBorder.Padding = compact ? new Thickness(10, 6, 10, 6) : new Thickness(14, 6, 14, 6);
        ParameterActionPanel.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
    }
    
    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateConnectionStatus();
        });
    }
    
    private void UpdateConnectionStatus()
    {
        if (_mavLinkService.IsConnected)
        {
            StatusTextBlock.Text = $"Connected - {_mavLinkService.ConnectionString}";
            RefreshButton.IsEnabled = true;
        }
        else
        {
            StatusTextBlock.Text = "Not connected";
            RefreshButton.IsEnabled = false;
        }
    }
    
    private void OnParameterReceived(object? sender, Parameter parameter)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Add to list if not exists
            if (!_allParameters.Any(p => p.Name == parameter.Name))
            {
                _allParameters.Add(parameter);
                ApplyFilters();
            }
        });
    }
    
    private void OnParameterUpdated(object? sender, Parameter parameter)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Update in list
            var existing = _allParameters.FirstOrDefault(p => p.Name == parameter.Name);
            if (existing != null)
            {
                var index = _allParameters.IndexOf(existing);
                _allParameters[index] = parameter;
                ApplyFilters();
            }
        });
    }
    
    private void OnLoadingProgressChanged(object? sender, int loadedCount)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var total = _parameterService.TotalParameters;
            var progress = total > 0 ? (loadedCount * 100.0 / total) : 0;
            
            LoadingProgressBar.Value = progress;
            LoadingTextBlock.Text = $"Loading parameters... {loadedCount} / {total}";
            CountTextBlock.Text = $"{loadedCount} / {total} parameters";
            
            if (loadedCount >= total && total > 0)
            {
                HideLoadingIndicator();
                ShowMessage($"Loaded {total} parameters successfully", false);
            }
        });
    }
    
    private void OnErrorOccurred(object? sender, string error)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowMessage(error, true);
            HideLoadingIndicator();
        });
    }
    
    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadParametersAsync();
    }
    
    private async Task LoadParametersAsync()
    {
        try
        {
            ShowLoadingIndicator();
            _allParameters.Clear();
            _filteredParameters.Clear();
            ParameterListView.ItemsSource = null;
            
            var success = await _parameterService.RequestAllParametersAsync();
            
            if (success)
            {
                // Parameters will be added via ParameterReceived event
                await Task.Delay(100); // Small delay to let events process
                
                // Load all parameters from service
                var parameters = await _parameterService.GetAllParametersAsync();
                _allParameters = parameters.ToList();
                ApplyFilters();
            }
            else
            {
                HideLoadingIndicator();
                ShowMessage("Failed to load parameters", true);
                ShowEmptyState();
            }
        }
        catch (Exception ex)
        {
            HideLoadingIndicator();
            ShowMessage($"Error loading parameters: {ex.Message}", true);
            ShowEmptyState();
        }
    }
    
    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }
    
    private void ShowModifiedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }
    
    private void ApplyFilters()
    {
        var searchText = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? "";
        var showModifiedOnly = ShowModifiedCheckBox.IsChecked ?? false;
        
        _filteredParameters = _allParameters
            .Where(p =>
            {
                // Apply search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!p.Name.ToLowerInvariant().Contains(searchText) &&
                        !p.Description.ToLowerInvariant().Contains(searchText) &&
                        !p.Group.ToLowerInvariant().Contains(searchText))
                    {
                        return false;
                    }
                }
                
                // Apply modified filter
                if (showModifiedOnly && !p.IsModified)
                {
                    return false;
                }
                
                return true;
            })
            .OrderBy(p => p.Name)
            .ToList();
        
        ParameterListView.ItemsSource = _filteredParameters;
        CountTextBlock.Text = $"{_filteredParameters.Count} / {_allParameters.Count} parameters";
        
        if (_filteredParameters.Count == 0 && _allParameters.Count > 0)
        {
            ShowMessage("No parameters match the current filters", false);
        }
        else if (_allParameters.Count == 0)
        {
            ShowEmptyState();
        }
        else
        {
            HideEmptyState();
        }
    }
    
    private void ParameterListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle selection if needed
    }
    
    private async void EditParameter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Parameter parameter)
        {
            await ShowEditDialogAsync(parameter);
        }
    }

    private async Task ShowEditDialogAsync(Parameter parameter)
    {
        var dialog = new ContentDialog
        {
            Title = $"Edit Parameter: {parameter.Name}",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        
        var stackPanel = new StackPanel { Spacing = 10 };
        
        // Parameter info
        stackPanel.Children.Add(new TextBlock
        {
            Text = parameter.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray)
        });
        
        // Current value
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"Current Value: {parameter.Value}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        
        // Default value
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"Default Value: {parameter.DefaultValue}",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray)
        });
        
        // Min/Max range
        if (parameter.MinValue != float.MinValue || parameter.MaxValue != float.MaxValue)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Range: {parameter.MinValue} to {parameter.MaxValue}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray)
            });
        }
        
        // New value input
        var valueTextBox = new TextBox
        {
            Header = "New Value",
            Text = parameter.Value.ToString(),
            PlaceholderText = "Enter new value"
        };
        stackPanel.Children.Add(valueTextBox);
        
        // Validation message
        var validationTextBlock = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Red),
            Visibility = Visibility.Collapsed
        };
        stackPanel.Children.Add(validationTextBlock);
        
        // Reset button
        var resetButton = new Button
        {
            Content = "Reset to Default",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 10, 0, 0)
        };
        resetButton.Click += (s, e) =>
        {
            valueTextBox.Text = parameter.DefaultValue.ToString();
        };
        stackPanel.Children.Add(resetButton);
        
        dialog.Content = stackPanel;
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            if (float.TryParse(valueTextBox.Text, out var newValue))
            {
                // Validate
                var validation = await _parameterService.ValidateParameterAsync(parameter.Name, newValue);
                
                if (validation.IsValid)
                {
                    // Set parameter
                    var success = await _parameterService.SetParameterAsync(parameter.Name, newValue);
                    
                    if (success)
                    {
                        ShowMessage($"Parameter '{parameter.Name}' updated successfully", false);
                    }
                    else
                    {
                        ShowMessage($"Failed to update parameter '{parameter.Name}'", true);
                    }
                }
                else
                {
                    ShowMessage($"Validation failed: {validation.ErrorMessage}", true);
                }
            }
            else
            {
                ShowMessage("Invalid value format", true);
            }
        }
    }
    
    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".param");
            picker.FileTypeFilter.Add(".params");
            picker.FileTypeFilter.Add(".txt");
            
            // Get the window handle for the picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSingleFileAsync();
            
            if (file != null)
            {
                ShowLoadingIndicator();
                LoadingTextBlock.Text = "Importing parameters...";
                
                var success = await _parameterService.ImportParametersAsync(file.Path);
                
                HideLoadingIndicator();
                
                if (success)
                {
                    ShowMessage($"Parameters imported from {file.Name}", false);
                    await LoadParametersAsync(); // Refresh list
                }
                else
                {
                    ShowMessage("Failed to import parameters", true);
                }
            }
        }
        catch (Exception ex)
        {
            HideLoadingIndicator();
            ShowMessage($"Import error: {ex.Message}", true);
        }
    }
    
    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Parameter File", new List<string> { ".param" });
            picker.SuggestedFileName = $"parameters_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            // Get the window handle for the picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSaveFileAsync();
            
            if (file != null)
            {
                ShowLoadingIndicator();
                LoadingTextBlock.Text = "Exporting parameters...";
                
                var success = await _parameterService.ExportParametersAsync(file.Path);
                
                HideLoadingIndicator();
                
                if (success)
                {
                    ShowMessage($"Parameters exported to {file.Name}", false);
                }
                else
                {
                    ShowMessage("Failed to export parameters", true);
                }
            }
        }
        catch (Exception ex)
        {
            HideLoadingIndicator();
            ShowMessage($"Export error: {ex.Message}", true);
        }
    }
    
    private void ShowLoadingIndicator()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        EmptyPanel.Visibility = Visibility.Collapsed;
        ParameterListView.Visibility = Visibility.Collapsed;
        LoadingProgressBar.Visibility = Visibility.Visible;
        LoadingProgressBar.Value = 0;
    }
    
    private void HideLoadingIndicator()
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ParameterListView.Visibility = Visibility.Visible;
        LoadingProgressBar.Visibility = Visibility.Collapsed;
    }
    
    private void ShowEmptyState()
    {
        EmptyPanel.Visibility = Visibility.Visible;
        ParameterListView.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Collapsed;
    }
    
    private void HideEmptyState()
    {
        EmptyPanel.Visibility = Visibility.Collapsed;
        ParameterListView.Visibility = Visibility.Visible;
    }
    
    private void ShowMessage(string message, bool isError)
    {
        MessageTextBlock.Text = message;
        MessageTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            isError ? Microsoft.UI.Colors.Red : Microsoft.UI.Colors.Green);
        
        // Clear message after 5 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            MessageTextBlock.Text = "";
            timer.Stop();
        };
        timer.Start();
    }
}
