using FluxGet.Core.Services;
using FluxGet.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage.Pickers;

namespace FluxGet.UI.Views;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsViewModel _viewModel;
    
    public SettingsPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<SettingsViewModel>();
        DataContext = _viewModel;
        
        UpdateSpeedLimitText();
        UpdateExtensionStatus();
    }
    
    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
        folderPicker.FileTypeFilter.Add("*");
        
        var window = App.MainWindow;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        
        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            _viewModel.DefaultDownloadPath = folder.Path;
        }
    }
    
    private void SpeedLimitSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (SpeedLimitText != null)
        {
            UpdateSpeedLimitText();
        }
    }
    
    private void UpdateSpeedLimitText()
    {
        if (_viewModel.GlobalSpeedLimit > 0)
        {
            var kb = _viewModel.GlobalSpeedLimit;
            if (kb >= 1024)
            {
                SpeedLimitText.Text = $"{kb} KB/s ({kb / 1024.0:F1} MB/s)";
            }
            else
            {
                SpeedLimitText.Text = $"{kb} KB/s";
            }
        }
        else
        {
            SpeedLimitText.Text = "Unlimited";
        }
    }
    
    private void UpdateExtensionStatus()
    {
        if (_viewModel.IsBrowserExtensionRunning)
        {
            ExtensionStatusText.Text = "Status: Active - Server running";
        }
        else
        {
            ExtensionStatusText.Text = "Status: Inactive";
        }
    }
    
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);
        await ShowInfoDialog("Saved", "Settings saved successfully.");
    }
    
    private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetDefaultsCommand.Execute(null);
        UpdateSpeedLimitText();
    }
    
    private async Task ShowInfoDialog(string title, string message)
    {
        try
        {
            await new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Width = 400
            }.ShowAsync();
        }
        catch
        {
            // Skip if dialog is already open or XamlRoot is invalid
        }
    }
}
