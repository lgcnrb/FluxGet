using FluxGet.Core.Helpers;
using FluxGet.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace FluxGet.UI.Views;

public sealed partial class ToolsPage : Page
{
    private readonly ToolsViewModel _vm;
    
    public ToolsPage()
    {
        InitializeComponent();
        _vm = App.GetService<ToolsViewModel>();
        DataContext = _vm;
        
        Loaded += async (s, e) => await RefreshAsync();
    }
    
    private async Task RefreshAsync()
    {
        await _vm.RefreshToolsStatusAsync();
        
        YtDlpStatusText.Text = _vm.YtDlpStatusText;
        YtDlpStatusText.Foreground = _vm.YtDlpStatusBrush;
        YtDlpPathText.Text = _vm.YtDlpPathText;
        YtDlpVersionText.Text = _vm.YtDlpVersionText;
        YtDlpVersionText.Visibility = _vm.YtDlpVersionVisible ? Visibility.Visible : Visibility.Collapsed;
        
        FfmpegStatusText.Text = _vm.FfmpegStatusText;
        FfmpegStatusText.Foreground = _vm.FfmpegStatusBrush;
        FfmpegPathText.Text = _vm.FfmpegPathText;
        FfmpegVersionText.Text = _vm.FfmpegVersionText;
        FfmpegVersionText.Visibility = _vm.FfmpegVersionVisible ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private async void YtDlpSelectButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = await PickExeFile("Select yt-dlp.exe");
        if (filePath == null) return;
        
        if (!System.IO.File.Exists(filePath))
        {
            await DialogHelper.ShowInfoAsync("Error", "Selected file not found.");
            return;
        }
        
        _vm.SetYtDlpPath(filePath);
        await RefreshAsync();
        
        if (_vm.IsToolValid(filePath))
            await DialogHelper.ShowInfoAsync("Success", $"yt-dlp path saved:\n{filePath}");
        else
            await DialogHelper.ShowInfoAsync("Warning", "File saved but could not be verified. You can still continue.");
    }
    
    private async void FfmpegSelectButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = await PickExeFile("Select ffmpeg.exe (from bin folder)");
        if (filePath == null) return;
        
        if (!System.IO.File.Exists(filePath))
        {
            await DialogHelper.ShowInfoAsync("Error", "Selected file not found.");
            return;
        }
        
        _vm.SetFfmpegPath(filePath);
        await RefreshAsync();
        
        if (_vm.IsToolValid(filePath))
            await DialogHelper.ShowInfoAsync("Success", $"ffmpeg path saved:\n{filePath}");
        else
            await DialogHelper.ShowInfoAsync("Warning", "File saved but could not be verified. You can still continue.");
    }
    
    private async void YtDlpDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new System.Uri("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"));
    }
    
    private async void FfmpegDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new System.Uri("https://github.com/BtbN/FFmpeg-Builds/releases"));
    }
    
    private async void YtDlpCheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false;
        btn.Content = "Checking...";
        
        try
        {
            var latestVersion = await _vm.CheckYtDlpUpdateAsync();
            var currentVersion = _vm.GetToolVersion(_vm.YtDlpPathText);
            
            if (string.IsNullOrEmpty(latestVersion))
                await DialogHelper.ShowInfoAsync("Check", "Version info could not be retrieved from GitHub.");
            else if (currentVersion != null && currentVersion.Contains(latestVersion.Replace("yt-dlp ", "").Replace("v", "")))
                await DialogHelper.ShowInfoAsync("Up to date", $"yt-dlp is already up to date: {currentVersion}");
            else
            {
                var result = await DialogHelper.ShowConfirmAsync(
                    "New Version Available",
                    $"Current version: {latestVersion}\nInstalled: {currentVersion ?? "Unknown"}\n\nWould you like to download from GitHub?",
                    "Download", "Cancel", 400);
                
                if (result == ContentDialogResult.Primary)
                    await Launcher.LaunchUriAsync(new System.Uri("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"));
            }
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowInfoAsync("Error", $"Check error: {ex.Message}");
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Check for Update";
        }
    }
    
    private async void FfmpegCheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false;
        btn.Content = "Checking...";
        
        try
        {
            var latestVersion = await _vm.CheckFfmpegUpdateAsync();
            var currentVersion = _vm.GetToolVersion(_vm.FfmpegPathText);
            
            if (string.IsNullOrEmpty(latestVersion))
                await DialogHelper.ShowInfoAsync("Check", "Version info could not be retrieved from GitHub.");
            else if (currentVersion != null && currentVersion.Contains(latestVersion.Replace("n", "")))
                await DialogHelper.ShowInfoAsync("Up to date", $"ffmpeg is already up to date: {currentVersion}");
            else
            {
                var result = await DialogHelper.ShowConfirmAsync(
                    "New Version Available",
                    $"Current version: {latestVersion}\nInstalled: {currentVersion ?? "Unknown"}\n\nWould you like to download from GitHub?",
                    "Download", "Cancel", 400);
                
                if (result == ContentDialogResult.Primary)
                    await Launcher.LaunchUriAsync(new System.Uri("https://github.com/BtbN/FFmpeg-Builds/releases"));
            }
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowInfoAsync("Error", $"Check error: {ex.Message}");
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Check for Update";
        }
    }
    
    private void OpenToolsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.OpenToolsFolder();
    }
    
    private static async Task<string?> PickExeFile(string commitText)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = commitText;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
        
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
