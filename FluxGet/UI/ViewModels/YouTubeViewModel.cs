using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Helpers;
using FluxGet.Core.Models;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxGet.UI.ViewModels;

public partial class YouTubeViewModel : ObservableObject
{
    private readonly YouTubeService _youTubeService;
    private readonly IDownloadService _downloadService;
    private readonly IQueueService _queueService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<YouTubeViewModel> _logger;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private YouTubeInfo? _videoInfo;

    [ObservableProperty]
    private string _savePath;

    [ObservableProperty]
    private bool _isMp4Mode = true;

    [ObservableProperty]
    private bool _isFetching;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _hasFfmpeg;

    [ObservableProperty]
    private bool _hasYtDlp;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusText = string.Empty;

    [ObservableProperty]
    private int _selectedResolutionIndex = -1;

    [ObservableProperty]
    private ObservableCollection<ResolutionItem> _resolutions = new();

    [ObservableProperty]
    private string _mp4SizeText = string.Empty;

    [ObservableProperty]
    private string _mp3SizeText = string.Empty;

    [ObservableProperty]
    private string _selectedFormatText = string.Empty;

    [ObservableProperty]
    private string _selectedSizeText = string.Empty;

    [ObservableProperty]
    private bool _showToolsWarning;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public YouTubeViewModel(
        YouTubeService youTubeService,
        IDownloadService downloadService,
        IQueueService queueService,
        SettingsService settingsService,
        ILogger<YouTubeViewModel> logger)
    {
        _youTubeService = youTubeService;
        _downloadService = downloadService;
        _queueService = queueService;
        _settingsService = settingsService;
        _logger = logger;

        _savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        CheckToolsStatus();
    }

    public void CheckToolsStatus()
    {
        var ytdlpPath = _settingsService.YtDlpPath;
        HasYtDlp = !string.IsNullOrEmpty(ytdlpPath) && File.Exists(ytdlpPath);
        ShowToolsWarning = !HasYtDlp;

        var ffmpegPath = _settingsService.FfmpegPath;
        HasFfmpeg = !string.IsNullOrEmpty(ffmpegPath) && File.Exists(ffmpegPath);
    }

    [RelayCommand]
    private async Task FetchInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            ShowError("Please enter a YouTube URL.");
            return;
        }

        if (!YouTubeService.IsYouTubeUrl(Url))
        {
            ShowError("Invalid YouTube URL. Must be a youtube.com or youtu.be link.");
            return;
        }

        if (!HasYtDlp)
        {
            ShowError("yt-dlp is not installed. Please install it from Settings > Tools.");
            return;
        }

        IsFetching = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            VideoInfo = await _youTubeService.GetVideoInfoAsync(Url);
            Resolutions.Clear();
            SelectedResolutionIndex = -1;

            var allVideoFormats = VideoInfo.Formats
                .Where(f => f.HasVideo && f.FileSize > 0 && !string.IsNullOrEmpty(f.Resolution))
                .ToList();

            var audioFormats = VideoInfo.Formats
                .Where(f => f.HasAudio && !f.HasVideo && f.FileSize > 0)
                .OrderByDescending(f => f.FileSize)
                .ToList();

            var bestMergedMp4 = allVideoFormats
                .Where(f => f.HasAudio && f.Extension == "mp4")
                .OrderByDescending(f => f.FileSize)
                .FirstOrDefault();

            Mp4SizeText = bestMergedMp4 != null
                ? $"Approximately {FileHelper.FormatBytes(bestMergedMp4.FileSize)}"
                : "";

            var bestVideo = allVideoFormats.Where(f => f.Extension is "mp4" or "webm")
                .OrderByDescending(f => f.FileSize).FirstOrDefault();
            if (bestVideo != null && audioFormats.Any())
                Mp4SizeText = $"Approximately {FileHelper.FormatBytes(bestVideo.FileSize + audioFormats[0].FileSize)}";

            Mp3SizeText = audioFormats.Any() ? $"Approximately {FileHelper.FormatBytes(audioFormats[0].FileSize)}" : "";

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

                if (!HasFfmpeg && !hasMergedFormat)
                    continue;

                var suffix = hasMergedFormat ? "" : " (will be merged automatically)";
                Resolutions.Add(new ResolutionItem
                {
                    Height = height,
                    HasMergedFormat = hasMergedFormat,
                    DisplayText = $"{height}p - {FileHelper.FormatBytes(estimatedSize)}{suffix}"
                });
            }

            if (Resolutions.Count > 0)
                SelectedResolutionIndex = 0;

            IsMp4Mode = true;
            UpdatePreviewInfo();
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsFetching = false;
        }
    }

    partial void OnIsMp4ModeChanged(bool value)
    {
        UpdatePreviewInfo();
    }

    partial void OnSelectedResolutionIndexChanged(int value)
    {
        UpdatePreviewInfo();
    }

    private void UpdatePreviewInfo()
    {
        if (VideoInfo == null) return;

        if (IsMp4Mode)
        {
            if (SelectedResolutionIndex >= 0 && SelectedResolutionIndex < Resolutions.Count)
            {
                var res = Resolutions[SelectedResolutionIndex];
                SelectedFormatText = $"MP4 {res.Height}p";

                var sizeText = res.DisplayText;
                var dashIdx = sizeText.IndexOf(" - ");
                if (dashIdx >= 0)
                {
                    var sizePart = sizeText[(dashIdx + 3)..];
                    var parenIdx = sizePart.IndexOf('(');
                    SelectedSizeText = parenIdx >= 0 ? sizePart[..parenIdx].Trim() : sizePart.Trim();
                }
            }
        }
        else
        {
            SelectedFormatText = "MP3 Audio Only";

            var bestAudio = VideoInfo.Formats
                .Where(f => f.HasAudio && !f.HasVideo && f.FileSize > 0)
                .OrderByDescending(f => f.FileSize)
                .FirstOrDefault();
            if (bestAudio != null)
            {
                SelectedSizeText = FileHelper.FormatBytes(bestAudio.FileSize);
            }
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (VideoInfo == null)
        {
            ShowError("Please fetch video info first.");
            return;
        }

        int? selectedHeight = null;
        bool hasMergedFormat = false;

        if (IsMp4Mode)
        {
            if (SelectedResolutionIndex >= 0 && SelectedResolutionIndex < Resolutions.Count)
            {
                var res = Resolutions[SelectedResolutionIndex];
                selectedHeight = res.Height;
                hasMergedFormat = res.HasMergedFormat;
            }
            else
            {
                ShowError("Please select a resolution.");
                return;
            }

            if (!HasFfmpeg && !hasMergedFormat)
            {
                ShowError("For the selected resolution, video and audio are downloaded separately. ffmpeg is required for merging. Please install ffmpeg from Settings > Tools or select a lower resolution.");
                return;
            }
        }

        IsDownloading = true;
        HasError = false;
        DownloadProgress = 0;
        DownloadStatusText = "Starting...";

        try
        {
            var fileName = FileHelper.SanitizeFileName(VideoInfo.Title);
            var outputPath = Path.Combine(SavePath, fileName);

            var progress = new Progress<double>(pct =>
            {
                DownloadProgress = pct;
                DownloadStatusText = $"%{pct:F1}";
            });

            if (!IsMp4Mode)
            {
                DownloadStatusText = "Downloading audio...";
                var tempPath = outputPath + ".webm";
                outputPath += ".mp3";

                await _youTubeService.DownloadAudioAsync(Url, tempPath, progress);

                DownloadStatusText = "Converting to MP3...";
                await _youTubeService.ConvertToMp3Async(tempPath, outputPath);
            }
            else
            {
                DownloadStatusText = hasMergedFormat
                    ? "Downloading video..."
                    : "Downloading video and audio, merging...";
                outputPath += ".mp4";
                await _youTubeService.DownloadVideoByHeightAsync(Url, outputPath, selectedHeight!.Value, progress);
            }

            var task = await _downloadService.AddDownloadAsync(Url, SavePath, DownloadCategory.Video);
            task.FileName = Path.GetFileName(outputPath);
            task.FilePath = outputPath;
            task.Status = DownloadStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            await _downloadService.SaveAsync(task);

            DownloadProgress = 100;
            DownloadStatusText = "Completed!";

            _logger.LogInformation("YouTube download completed: {FileName}", task.FileName);
        }
        catch (Exception ex)
        {
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
            IsDownloading = false;
        }
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }
}

public class ResolutionItem
{
    public int Height { get; set; }
    public bool HasMergedFormat { get; set; }
    public string DisplayText { get; set; } = string.Empty;
}
