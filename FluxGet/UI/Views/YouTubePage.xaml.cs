using FluxGet.Core.Models;
using FluxGet.Core.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FluxGet.UI.Views;

public sealed partial class YouTubePage : Page
{
    private readonly YouTubeService _youTubeService;
    private readonly SettingsService _settingsService;
    private YouTubeInfo? _videoInfo;
    private string _savePath;
    private bool _isMp4Mode = true;
    private bool _hasFfmpeg;
    
    public YouTubePage()
    {
        InitializeComponent();
        _youTubeService = App.GetService<YouTubeService>();
        _settingsService = App.GetService<SettingsService>();
        _savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        SavePathText.Text = _savePath;
        
        UpdateFfmpegStatus();
        UpdateFormatCardStyles();
    }
    
    private void UpdateFfmpegStatus()
    {
        var ffmpegPath = _settingsService.FfmpegPath;
        _hasFfmpeg = !string.IsNullOrEmpty(ffmpegPath) && File.Exists(ffmpegPath);
    }
    
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        CheckToolsStatus();
    }
    
    private void CheckToolsStatus()
    {
        var ytdlpPath = _settingsService.YtDlpPath;
        
        if (string.IsNullOrEmpty(ytdlpPath) || !File.Exists(ytdlpPath))
        {
            ToolsInfoBar.Severity = InfoBarSeverity.Warning;
            ToolsInfoBar.IsOpen = true;
            ToolsInfoBar.Visibility = Visibility.Visible;
        }
        else
        {
            ToolsInfoBar.Visibility = Visibility.Collapsed;
        }
        
        UpdateFfmpegStatus();
    }
    
    private void GoToSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        (App.MainWindow as MainWindow)?.NavigateToSettings();
    }
    
    private void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _ = FetchInfoAsync();
        }
    }
    
    private async void FetchInfoButton_Click(object sender, RoutedEventArgs e)
    {
        await FetchInfoAsync();
    }
    
    private async Task FetchInfoAsync()
    {
        var url = UrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            ShowError("Please enter a YouTube URL.");
            return;
        }
        
        if (!YouTubeService.IsYouTubeUrl(url))
        {
            ShowError("Invalid YouTube URL. Must be a youtube.com or youtu.be link.");
            return;
        }
        
        var toolsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxGet", "tools");
        var ytdlpPath = Path.Combine(toolsDir, "yt-dlp.exe");
        if (!File.Exists(ytdlpPath))
        {
            ShowError("yt-dlp is not installed. Please install it from Settings > Tools.");
            return;
        }
        
        ShowLoading("Fetching video info...");
        FetchInfoButton.IsEnabled = false;
        
        try
        {
            _videoInfo = await _youTubeService.GetVideoInfoAsync(url);
            
            HideLoading();
            
            // Left column: Video info
            VideoTitleText.Text = _videoInfo.Title;
            VideoUploaderText.Text = _videoInfo.Uploader;
            VideoDurationText.Text = _videoInfo.Duration;
            VideoInfoBorder.Visibility = Visibility.Visible;
            
            // Right column: Video preview
            VideoPreviewBorder.Visibility = Visibility.Visible;
            PreviewTitleText.Text = _videoInfo.Title;
            PreviewUploaderText.Text = _videoInfo.Uploader;
            PreviewDurationText.Text = _videoInfo.Duration;
            
            if (!string.IsNullOrEmpty(_videoInfo.ThumbnailUrl))
            {
                try
                {
                    ThumbnailImage.Source = new BitmapImage(new Uri(_videoInfo.ThumbnailUrl));
                }
                catch { }
            }
            
            // Collect all video formats
            var allVideoFormats = _videoInfo.Formats
                .Where(f => f.HasVideo && f.FileSize > 0 && !string.IsNullOrEmpty(f.Resolution))
                .ToList();
            
            var audioFormats = _videoInfo.Formats
                .Where(f => f.HasAudio && !f.HasVideo && f.FileSize > 0)
                .OrderByDescending(f => f.FileSize)
                .ToList();
            
            // MP4 size (largest merged format, or video+audio sum)
            var bestMergedMp4 = allVideoFormats
                .Where(f => f.HasAudio && f.Extension == "mp4")
                .OrderByDescending(f => f.FileSize)
                .FirstOrDefault();
            
            if (bestMergedMp4 != null)
            {
                Mp4SizeText.Text = $"Approximately {FormatBytes(bestMergedMp4.FileSize)}";
            }
            else
            {
                var bestVideo = allVideoFormats.Where(f => f.Extension is "mp4" or "webm")
                    .OrderByDescending(f => f.FileSize).FirstOrDefault();
                if (bestVideo != null && audioFormats.Any())
                    Mp4SizeText.Text = $"Approximately {FormatBytes(bestVideo.FileSize + audioFormats[0].FileSize)}";
                else
                    Mp4SizeText.Text = "";
            }
            
            Mp3SizeText.Text = audioFormats.Any() ? $"Approximately {FormatBytes(audioFormats[0].FileSize)}" : "";
            
            // Fill resolutions
            ResolutionComboBox.Items.Clear();
            
            // Group by resolution
            var allByHeight = allVideoFormats
                .Select(f =>
                {
                    var parts = f.Resolution.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var h))
                        return (Height: h, Format: f);
                    return (Height: 0, Format: f);
                })
                .Where(x => x.Height > 0)
                .GroupBy(x => x.Height)
                .OrderByDescending(g => g.Key)
                .ToList();
            
            foreach (var group in allByHeight)
            {
                var height = group.Key;
                var hasMergedFormat = group.Any(x => x.Format.HasAudio);
                
                // Calculate size
                long estimatedSize;
                if (hasMergedFormat)
                {
                    var bestMerged = group.Where(x => x.Format.HasAudio)
                        .OrderByDescending(x => x.Format.FileSize).First().Format;
                    estimatedSize = bestMerged.FileSize;
                }
                else
                {
                    var bestVideoOnly = group.OrderByDescending(x => x.Format.FileSize).First().Format;
                    var audioSize = audioFormats.FirstOrDefault()?.FileSize ?? 0;
                    estimatedSize = bestVideoOnly.FileSize + audioSize;
                }
                
                // If no ffmpeg, only show merged formats
                if (!_hasFfmpeg && !hasMergedFormat)
                    continue;
                
                var suffix = hasMergedFormat ? "" : " (will be merged automatically)";
                ResolutionComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{height}p - {FormatBytes(estimatedSize)}{suffix}",
                    Tag = new ResolutionInfo { Height = height, HasMergedFormat = hasMergedFormat }
                });
            }
            
            if (ResolutionComboBox.Items.Count > 0)
                ResolutionComboBox.SelectedIndex = 0;
            
            _isMp4Mode = true;
            UpdateFormatCardStyles();
            DownloadButton.Visibility = Visibility.Visible;
            
            // Update preview
            UpdatePreviewInfo();
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            FetchInfoButton.IsEnabled = true;
        }
    }
    
    private void Mp4Card_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isMp4Mode = true;
        UpdateFormatCardStyles();
    }
    
    private void Mp3Card_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isMp4Mode = false;
        UpdateFormatCardStyles();
    }
    
    private void UpdateFormatCardStyles()
    {
        var activeBrush = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
        var defaultBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        
        Mp4Card.BorderBrush = _isMp4Mode ? activeBrush : defaultBrush;
        Mp4Card.BorderThickness = _isMp4Mode ? new Thickness(2) : new Thickness(1);
        
        Mp3Card.BorderBrush = !_isMp4Mode ? activeBrush : defaultBrush;
        Mp3Card.BorderThickness = !_isMp4Mode ? new Thickness(2) : new Thickness(1);
        
        ResolutionPanel.Visibility = _isMp4Mode ? Visibility.Visible : Visibility.Collapsed;
        
        UpdatePreviewInfo();
    }
    
    private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePreviewInfo();
    }
    
    private void UpdatePreviewInfo()
    {
        if (_videoInfo == null) return;
        
        if (_isMp4Mode)
        {
            if (ResolutionComboBox.SelectedItem is ComboBoxItem item && item.Tag is ResolutionInfo resInfo)
            {
                SelectedFormatText.Text = $"MP4 {resInfo.Height}p";
                SelectedFormatText.Foreground = new SolidColorBrush(Colors.LightBlue);
                SelectedSizeBadge.Visibility = Visibility.Visible;
                
                // Get estimated size from ComboBox
                var sizeText = item.Content.ToString() ?? "";
                var dashIdx = sizeText.IndexOf(" - ");
                if (dashIdx >= 0)
                {
                    var sizePart = sizeText.Substring(dashIdx + 3);
                    var parenIdx = sizePart.IndexOf('(');
                    SelectedSizeText.Text = parenIdx >= 0 ? sizePart.Substring(0, parenIdx).Trim() : sizePart.Trim();
                }
            }
        }
        else
        {
            SelectedFormatText.Text = "MP3 Audio Only";
            SelectedFormatText.Foreground = new SolidColorBrush(Colors.LightGreen);
            
            var bestAudio = _videoInfo.Formats
                .Where(f => f.HasAudio && !f.HasVideo && f.FileSize > 0)
                .OrderByDescending(f => f.FileSize)
                .FirstOrDefault();
            if (bestAudio != null)
            {
                SelectedSizeBadge.Visibility = Visibility.Visible;
                SelectedSizeText.Text = FormatBytes(bestAudio.FileSize);
            }
        }
    }
    
    private async void ChangePathButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
        folderPicker.FileTypeFilter.Add("*");
        
        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            _savePath = folder.Path;
            SavePathText.Text = _savePath;
        }
    }
    
    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_videoInfo == null)
        {
            ShowError("Please fetch video info first.");
            return;
        }
        
        int? selectedHeight = null;
        bool hasMergedFormat = false;
        
        if (_isMp4Mode)
        {
            if (ResolutionComboBox.SelectedItem is ComboBoxItem item && item.Tag is ResolutionInfo resInfo)
            {
                selectedHeight = resInfo.Height;
                hasMergedFormat = resInfo.HasMergedFormat;
            }
            else
            {
                ShowError("Please select a resolution.");
                return;
            }
            
                // If no ffmpeg and no merged format, show error
                if (!_hasFfmpeg && !hasMergedFormat)
                {
                    ShowError("For the selected resolution, video and audio are downloaded separately. ffmpeg is required for merging. Please install ffmpeg from Settings > Tools or select a lower resolution.");
                return;
            }
        }
        
        DownloadButton.IsEnabled = false;
        ProgressBorder.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        DownloadProgressBar.IsIndeterminate = false;
        DownloadPercentText.Text = "%0";
        DownloadStatusText.Text = "Starting...";
        
        var downloadService = App.GetService<IDownloadService>();
        var url = UrlBox.Text.Trim();
        
        try
        {
            var fileName = SanitizeFileName(_videoInfo.Title);
            var outputPath = Path.Combine(_savePath, fileName);
            
            var progress = new Progress<double>(pct =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    DownloadProgressBar.Value = pct;
                    DownloadPercentText.Text = $"%{pct:F1}";
                });
            });
            
            if (!_isMp4Mode)
            {
                DownloadStatusText.Text = "Downloading audio...";
                var tempPath = outputPath + ".webm";
                outputPath += ".mp3";
                
                await _youTubeService.DownloadAudioAsync(url, tempPath, progress);
                
                DownloadStatusText.Text = "Converting to MP3...";
                DownloadProgressBar.IsIndeterminate = true;
                
                await _youTubeService.ConvertToMp3Async(tempPath, outputPath);
                
                DownloadProgressBar.IsIndeterminate = false;
            }
            else
            {
                DownloadStatusText.Text = hasMergedFormat
                    ? "Downloading video..."
                    : "Downloading video and audio, merging...";
                outputPath += ".mp4";
                await _youTubeService.DownloadVideoByHeightAsync(url, outputPath, selectedHeight!.Value, progress);
            }
            
            var task = await downloadService.AddDownloadAsync(url, _savePath, DownloadCategory.Video);
            task.FileName = Path.GetFileName(outputPath);
            task.FilePath = outputPath;
            task.Status = DownloadStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            await downloadService.SaveAsync(task);
            
            DownloadProgressBar.Value = 100;
            DownloadPercentText.Text = "%100";
            DownloadStatusText.Text = "Completed!";
            
            var notificationService = App.GetService<DownloadNotificationService>();
            notificationService.NotifyCompleted(task);
        }
        catch (Exception ex)
        {
            ProgressBorder.Visibility = Visibility.Collapsed;
            var errorMsg = ex.Message;
            if (errorMsg.Contains("ffmpeg"))
                errorMsg = "ffmpeg is not installed. Video and audio were downloaded separately but could not be merged. Please install ffmpeg from Settings > Tools.";
            else if (errorMsg.Contains("403"))
                errorMsg = "YouTube blocked the download. Please try a different video.";
            else if (errorMsg.Contains("HTTP"))
                errorMsg = $"Download error: {errorMsg.Split('\n')[0]}";
            ShowError(errorMsg);
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }
    }
    
    private void ShowLoading(string text)
    {
        StatusPanel.Visibility = Visibility.Visible;
        StatusRing.IsActive = true;
        StatusText.Text = text;
        ErrorInfoBar.Visibility = Visibility.Collapsed;
        VideoInfoBorder.Visibility = Visibility.Collapsed;
        VideoPreviewBorder.Visibility = Visibility.Collapsed;
    }
    
    private void HideLoading()
    {
        StatusPanel.Visibility = Visibility.Collapsed;
        StatusRing.IsActive = false;
    }
    
    private void ShowError(string message)
    {
        ErrorInfoBar.Title = message;
        ErrorInfoBar.Severity = InfoBarSeverity.Error;
        ErrorInfoBar.IsOpen = true;
        ErrorInfoBar.Visibility = Visibility.Visible;
        RetryDownloadButton.Visibility = Visibility.Visible;
        StatusPanel.Visibility = Visibility.Collapsed;
    }
    
    private async void RetryDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var toolsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxGet", "tools");
        
        ErrorInfoBar.Visibility = Visibility.Collapsed;
        
        var ytdlpPath = Path.Combine(toolsDir, "yt-dlp.exe");
        try
        {
            if (File.Exists(ytdlpPath))
                File.Delete(ytdlpPath);
        }
        catch { }
        
        await FetchInfoAsync();
    }
    
    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Length > 100 ? name[..100] : name;
    }
    
    private static string FormatBytes(long bytes) => bytes switch
    {
        <= 0 => "Unknown",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

internal class ResolutionInfo
{
    public int Height { get; set; }
    public bool HasMergedFormat { get; set; }
}
