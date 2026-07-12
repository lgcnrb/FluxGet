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
            ShowError("Lutfen bir YouTube URL'si girin.");
            return;
        }
        
        if (!YouTubeService.IsYouTubeUrl(url))
        {
            ShowError("Gecersiz YouTube URL'si. youtube.com veya youtu.be linki olmali.");
            return;
        }
        
        var toolsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxGet", "tools");
        var ytdlpPath = Path.Combine(toolsDir, "yt-dlp.exe");
        if (!File.Exists(ytdlpPath))
        {
            ShowError("yt-dlp yuklu degil. Lutfen Ayarlar > Araclar bolumunden yukleyin.");
            return;
        }
        
        ShowLoading("Video bilgisi aliniyor...");
        FetchInfoButton.IsEnabled = false;
        
        try
        {
            _videoInfo = await _youTubeService.GetVideoInfoAsync(url);
            
            HideLoading();
            
            // Sol kolon: Video bilgileri
            VideoTitleText.Text = _videoInfo.Title;
            VideoUploaderText.Text = _videoInfo.Uploader;
            VideoDurationText.Text = _videoInfo.Duration;
            VideoInfoBorder.Visibility = Visibility.Visible;
            
            // Sag kolon: Video onizleme
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
            
            // Tum video formatlarini topla
            var allVideoFormats = _videoInfo.Formats
                .Where(f => f.HasVideo && f.FileSize > 0 && !string.IsNullOrEmpty(f.Resolution))
                .ToList();
            
            var audioFormats = _videoInfo.Formats
                .Where(f => f.HasAudio && !f.HasVideo && f.FileSize > 0)
                .OrderByDescending(f => f.FileSize)
                .ToList();
            
            // MP4 boyutu (birlestik format varsa en buyuk, yoksa video+ses toplami)
            var bestMergedMp4 = allVideoFormats
                .Where(f => f.HasAudio && f.Extension == "mp4")
                .OrderByDescending(f => f.FileSize)
                .FirstOrDefault();
            
            if (bestMergedMp4 != null)
            {
                Mp4SizeText.Text = $"Yaklasik {FormatBytes(bestMergedMp4.FileSize)}";
            }
            else
            {
                var bestVideo = allVideoFormats.Where(f => f.Extension is "mp4" or "webm")
                    .OrderByDescending(f => f.FileSize).FirstOrDefault();
                if (bestVideo != null && audioFormats.Any())
                    Mp4SizeText.Text = $"Yaklasik {FormatBytes(bestVideo.FileSize + audioFormats[0].FileSize)}";
                else
                    Mp4SizeText.Text = "";
            }
            
            Mp3SizeText.Text = audioFormats.Any() ? $"Yaklasik {FormatBytes(audioFormats[0].FileSize)}" : "";
            
            // Cozunurlukleri doldur
            ResolutionComboBox.Items.Clear();
            
            // Grublari cozunurluge gore olustur
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
                
                // Boyut hesapla
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
                
                // ffmpeg yoksa sadece birlesik formatlari goster
                if (!_hasFfmpeg && !hasMergedFormat)
                    continue;
                
                var suffix = hasMergedFormat ? "" : " (otomatik birlestirilecek)";
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
            
            // Onizleme guncelle
            UpdatePreviewInfo();
        }
        catch (Exception ex)
        {
            ShowError($"Hata: {ex.Message}");
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
                
                // Tahmini boyutu ComboBox'tan al
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
            SelectedFormatText.Text = "MP3 Sadece Ses";
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
            ShowError("Once video bilgisi getirin.");
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
                ShowError("Lutfen bir cozunurluk secin.");
                return;
            }
            
            // ffmpeg yoksa ve birlesik format yoksa hata ver
            if (!_hasFfmpeg && !hasMergedFormat)
            {
                ShowError("Secilen cozunurluk icin video ve ses ayri indirilir. Birlestirme icin ffmpeg gerekli. Lutfen Ayarlar > Araclar'dan ffmpeg yukleyin veya daha dusuk bir cozunurluk secin.");
                return;
            }
        }
        
        DownloadButton.IsEnabled = false;
        ProgressBorder.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        DownloadProgressBar.IsIndeterminate = false;
        DownloadPercentText.Text = "%0";
        DownloadStatusText.Text = "Baslatiliyor...";
        
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
                DownloadStatusText.Text = "Ses indiriliyor...";
                var tempPath = outputPath + ".webm";
                outputPath += ".mp3";
                
                await _youTubeService.DownloadAudioAsync(url, tempPath, progress);
                
                DownloadStatusText.Text = "MP3'e donusturuluyor...";
                DownloadProgressBar.IsIndeterminate = true;
                
                await _youTubeService.ConvertToMp3Async(tempPath, outputPath);
                
                DownloadProgressBar.IsIndeterminate = false;
            }
            else
            {
                DownloadStatusText.Text = hasMergedFormat
                    ? "Video indiriliyor..."
                    : "Video ve ses indiriliyor, birlestiriliyor...";
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
            DownloadStatusText.Text = "Tamamlandi!";
            
            var notificationService = App.GetService<DownloadNotificationService>();
            notificationService.NotifyCompleted(task);
        }
        catch (Exception ex)
        {
            ProgressBorder.Visibility = Visibility.Collapsed;
            var errorMsg = ex.Message;
            if (errorMsg.Contains("ffmpeg"))
                errorMsg = "ffmpeg yuklu degil. Video ve ses ayri indirildi ama birlestirilemedi. Ayarlar > Araclar'dan ffmpeg yukleyin.";
            else if (errorMsg.Contains("403"))
                errorMsg = "YouTube indirmeyi engelledi. Lutfen baska bir video deneyin.";
            else if (errorMsg.Contains("HTTP"))
                errorMsg = $"Indirme hatasi: {errorMsg.Split('\n')[0]}";
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
        <= 0 => "Bilinmiyor",
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
