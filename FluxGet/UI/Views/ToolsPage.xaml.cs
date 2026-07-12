using FluxGet.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.System;

namespace FluxGet.UI.Views;

public sealed partial class ToolsPage : Page
{
    private readonly SettingsService _settingsService;
    
    public ToolsPage()
    {
        InitializeComponent();
        _settingsService = App.GetService<SettingsService>();
        
        Loaded += async (s, e) => await RefreshToolsStatusAsync();
    }
    
    private static bool IsToolValid(string exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;
        try
        {
            var fi = new FileInfo(exePath);
            return fi.Length > 100_000;
        }
        catch
        {
            return false;
        }
    }
    
    private string? GetToolVersion(string exePath)
    {
        if (!File.Exists(exePath)) return null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
    
    private async Task RefreshToolsStatusAsync()
    {
        // yt-dlp status
        var ytdlpPath = _settingsService.YtDlpPath;
        if (IsToolValid(ytdlpPath))
        {
            YtDlpStatusText.Text = "Installed";
            YtDlpStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            YtDlpPathText.Text = ytdlpPath;
            
            var version = GetToolVersion(ytdlpPath);
            if (version != null)
            {
                YtDlpVersionText.Text = $"Version: {version}";
                YtDlpVersionText.Visibility = Visibility.Visible;
            }
            else
            {
                YtDlpVersionText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            YtDlpStatusText.Text = "Not installed";
            YtDlpStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            YtDlpPathText.Text = "Select the file using the button below";
            YtDlpVersionText.Visibility = Visibility.Collapsed;
        }
        
        // ffmpeg status
        var ffmpegPath = _settingsService.FfmpegPath;
        if (IsToolValid(ffmpegPath))
        {
            FfmpegStatusText.Text = "Installed";
            FfmpegStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            FfmpegPathText.Text = ffmpegPath;
            
            var version = GetToolVersion(ffmpegPath);
            if (version != null)
            {
                FfmpegVersionText.Text = $"Version: {version}";
                FfmpegVersionText.Visibility = Visibility.Visible;
            }
            else
            {
                FfmpegVersionText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            FfmpegStatusText.Text = "Not installed";
            FfmpegStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            FfmpegPathText.Text = "Select the file using the button below (required for MP3)";
            FfmpegVersionText.Visibility = Visibility.Collapsed;
        }
    }
    
    private async Task<string?> PickExeFile(string commitText)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = commitText;
        picker.SuggestedStartLocation = PickerLocationId.Downloads;
        
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
    
    private async void YtDlpSelectButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = await PickExeFile("Select yt-dlp.exe");
        if (filePath == null) return;
        
        if (!File.Exists(filePath))
        {
            await ShowInfoDialog("Error", "Selected file not found.");
            return;
        }
        
        _settingsService.YtDlpPath = filePath;
        _settingsService.Save();
        
        await RefreshToolsStatusAsync();
        
        if (IsToolValid(filePath))
        {
            await ShowInfoDialog("Success", $"yt-dlp path saved:\n{filePath}");
        }
        else
        {
            await ShowInfoDialog("Warning", "File saved but could not be verified. You can still continue.");
        }
    }
    
    private async void FfmpegSelectButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = await PickExeFile("Select ffmpeg.exe (from bin folder)");
        if (filePath == null) return;
        
        if (!File.Exists(filePath))
        {
            await ShowInfoDialog("Error", "Selected file not found.");
            return;
        }
        
        _settingsService.FfmpegPath = filePath;
        _settingsService.Save();
        
        await RefreshToolsStatusAsync();
        
        if (IsToolValid(filePath))
        {
            await ShowInfoDialog("Success", $"ffmpeg path saved:\n{filePath}");
        }
        else
        {
            await ShowInfoDialog("Warning", "File saved but could not be verified. You can still continue.");
        }
    }
    
    private async void YtDlpDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"));
    }
    
    private async void FfmpegDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/BtbN/FFmpeg-Builds/releases"));
    }
    
    private async void YtDlpCheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false;
        btn.Content = "Checking...";
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"$resp = Invoke-RestMethod 'https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest'; $resp.tag_name\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi)!;
            var latestVersion = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();
            
            var ytdlpPath = _settingsService.YtDlpPath;
            var currentVersion = GetToolVersion(ytdlpPath);
            
            if (string.IsNullOrEmpty(latestVersion))
            {
                await ShowInfoDialog("Check", "Version info could not be retrieved from GitHub.");
            }
            else if (currentVersion != null && currentVersion.Contains(latestVersion.Replace("yt-dlp ", "").Replace("v", "")))
            {
                await ShowInfoDialog("Up to date", $"yt-dlp is already up to date: {currentVersion}");
            }
            else
            {
                var result = await new ContentDialog
                {
                    Title = "New Version Available",
                    Content = $"Current version: {latestVersion}\nInstalled: {currentVersion ?? "Unknown"}\n\nWould you like to download from GitHub?",
                    PrimaryButtonText = "Download",
                    CloseButtonText = "Cancel",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                }.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"));
                }
            }
        }
        catch (Exception ex)
        {
            await ShowInfoDialog("Error", $"Check error: {ex.Message}");
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
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"$resp = Invoke-RestMethod 'https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest'; $resp.tag_name\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi)!;
            var latestVersion = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();
            
            var ffmpegPath = _settingsService.FfmpegPath;
            var currentVersion = GetToolVersion(ffmpegPath);
            
            if (string.IsNullOrEmpty(latestVersion))
            {
                await ShowInfoDialog("Check", "Version info could not be retrieved from GitHub.");
            }
            else if (currentVersion != null && currentVersion.Contains(latestVersion.Replace("n", "")))
            {
                await ShowInfoDialog("Up to date", $"ffmpeg is already up to date: {currentVersion}");
            }
            else
            {
                var result = await new ContentDialog
                {
                    Title = "New Version Available",
                    Content = $"Current version: {latestVersion}\nInstalled: {currentVersion ?? "Unknown"}\n\nWould you like to download from GitHub?",
                    PrimaryButtonText = "Download",
                    CloseButtonText = "Cancel",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                }.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("https://github.com/BtbN/FFmpeg-Builds/releases"));
                }
            }
        }
        catch (Exception ex)
        {
            await ShowInfoDialog("Error", $"Check error: {ex.Message}");
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Check for Update";
        }
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
                XamlRoot = App.MainWindow.Content.XamlRoot
            }.ShowAsync();
        }
        catch
        {
            // Skip if dialog is already open or XamlRoot is invalid
        }
    }
    
    private void OpenToolsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var toolsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxGet", "tools");
        Directory.CreateDirectory(toolsDir);
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = toolsDir,
            UseShellExecute = true
        });
    }
}
