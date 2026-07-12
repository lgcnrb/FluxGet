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
        // yt-dlp durumu
        var ytdlpPath = _settingsService.YtDlpPath;
        if (IsToolValid(ytdlpPath))
        {
            YtDlpStatusText.Text = "Yuklu";
            YtDlpStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            YtDlpPathText.Text = ytdlpPath;
            
            var version = GetToolVersion(ytdlpPath);
            if (version != null)
            {
                YtDlpVersionText.Text = $"Surum: {version}";
                YtDlpVersionText.Visibility = Visibility.Visible;
            }
            else
            {
                YtDlpVersionText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            YtDlpStatusText.Text = "Yuklu degil";
            YtDlpStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            YtDlpPathText.Text = "Asagidaki butonla dosyayi secin";
            YtDlpVersionText.Visibility = Visibility.Collapsed;
        }
        
        // ffmpeg durumu
        var ffmpegPath = _settingsService.FfmpegPath;
        if (IsToolValid(ffmpegPath))
        {
            FfmpegStatusText.Text = "Yuklu";
            FfmpegStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            FfmpegPathText.Text = ffmpegPath;
            
            var version = GetToolVersion(ffmpegPath);
            if (version != null)
            {
                FfmpegVersionText.Text = $"Surum: {version}";
                FfmpegVersionText.Visibility = Visibility.Visible;
            }
            else
            {
                FfmpegVersionText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            FfmpegStatusText.Text = "Yuklu degil";
            FfmpegStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            FfmpegPathText.Text = "Asagidaki butonla dosyayi secin (MP3 icin gerekli)";
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
        var filePath = await PickExeFile("yt-dlp.exe Sec");
        if (filePath == null) return;
        
        if (!File.Exists(filePath))
        {
            await ShowInfoDialog("Hata", "Secilen dosya bulunamadi.");
            return;
        }
        
        _settingsService.YtDlpPath = filePath;
        _settingsService.Save();
        
        await RefreshToolsStatusAsync();
        
        if (IsToolValid(filePath))
        {
            await ShowInfoDialog("Basarili", $"yt-dlp yolu kaydedildi:\n{filePath}");
        }
        else
        {
            await ShowInfoDialog("Uyari", "Dosya kaydedildi ancak dogrulanamadi. Yine de devam edebilirsiniz.");
        }
    }
    
    private async void FfmpegSelectButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = await PickExeFile("ffmpeg.exe Sec (bin klasorundeki)");
        if (filePath == null) return;
        
        if (!File.Exists(filePath))
        {
            await ShowInfoDialog("Hata", "Secilen dosya bulunamadi.");
            return;
        }
        
        _settingsService.FfmpegPath = filePath;
        _settingsService.Save();
        
        await RefreshToolsStatusAsync();
        
        if (IsToolValid(filePath))
        {
            await ShowInfoDialog("Basarili", $"ffmpeg yolu kaydedildi:\n{filePath}");
        }
        else
        {
            await ShowInfoDialog("Uyari", "Dosya kaydedildi ancak dogrulanamadi. Yine de devam edebilirsiniz.");
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
        btn.Content = "Kontrol ediliyor...";
        
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
                await ShowInfoDialog("Kontrol", "GitHub'dan surum bilgisi alinamadi.");
            }
            else if (currentVersion != null && currentVersion.Contains(latestVersion.Replace("yt-dlp ", "").Replace("v", "")))
            {
                await ShowInfoDialog("Guncel", $"yt-dlp zaten guncel: {currentVersion}");
            }
            else
            {
                var result = await new ContentDialog
                {
                    Title = "Yeni Surum Mevcut",
                    Content = $"Guncel surum: {latestVersion}\nMevcut: {currentVersion ?? "Bilinmiyor"}\n\nGitHub'dan indirmek ister misiniz?",
                    PrimaryButtonText = "Indir",
                    CloseButtonText = "Iptal",
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
            await ShowInfoDialog("Hata", $"Kontrol hatasi: {ex.Message}");
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Yeni Surum var mi?";
        }
    }
    
    private async void FfmpegCheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false;
        btn.Content = "Kontrol ediliyor...";
        
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
                await ShowInfoDialog("Kontrol", "GitHub'dan surum bilgisi alinamadi.");
            }
            else if (currentVersion != null && currentVersion.Contains(latestVersion.Replace("n", "")))
            {
                await ShowInfoDialog("Guncel", $"ffmpeg zaten guncel: {currentVersion}");
            }
            else
            {
                var result = await new ContentDialog
                {
                    Title = "Yeni Surum Mevcut",
                    Content = $"Guncel surum: {latestVersion}\nMevcut: {currentVersion ?? "Bilinmiyor"}\n\nGitHub'dan indirmek ister misiniz?",
                    PrimaryButtonText = "Indir",
                    CloseButtonText = "Iptal",
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
            await ShowInfoDialog("Hata", $"Kontrol hatasi: {ex.Message}");
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Yeni Surum var mi?";
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
                CloseButtonText = "Tamam",
                XamlRoot = App.MainWindow.Content.XamlRoot
            }.ShowAsync();
        }
        catch
        {
            // Dialog zaten aciksa veya XamlRoot gecersizse atla
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
