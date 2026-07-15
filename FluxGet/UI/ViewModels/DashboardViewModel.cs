using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Helpers;
using FluxGet.Core.Models;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxGet.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private readonly IQueueService _queueService;
    private readonly ILogger<DashboardViewModel> _logger;

    [ObservableProperty]
    private int _totalDownloads;

    [ObservableProperty]
    private int _activeDownloads;

    [ObservableProperty]
    private int _completedDownloads;

    [ObservableProperty]
    private string _totalSpeedText = "0 B/s";

    [ObservableProperty]
    private ObservableCollection<DownloadTask> _recentDownloads = new();

    [ObservableProperty]
    private ObservableCollection<DownloadTask> _activeDownloadsList = new();

    [ObservableProperty]
    private ObservableCollection<DownloadTask> _completedDownloadsList = new();

    [ObservableProperty]
    private bool _hasRecentDownloads;

    [ObservableProperty]
    private bool _hasActiveDownloads;

    [ObservableProperty]
    private bool _hasCompletedDownloads;

    [ObservableProperty]
    private string _clipboardUrl = string.Empty;

    [ObservableProperty]
    private bool _showClipboardBanner;

    public DashboardViewModel(
        IDownloadService downloadService,
        IQueueService queueService,
        ILogger<DashboardViewModel> logger)
    {
        _downloadService = downloadService;
        _queueService = queueService;
        _logger = logger;
    }

    public void RefreshData()
    {
        var downloads = _downloadService.GetAllDownloads();

        TotalDownloads = downloads.Count;
        ActiveDownloads = downloads.Count(t => t.Status == DownloadStatus.Downloading);
        CompletedDownloads = downloads.Count(t => t.Status == DownloadStatus.Completed);

        var totalSpeed = downloads.Where(t => t.Status == DownloadStatus.Downloading).Sum(t => t.Speed);
        TotalSpeedText = FileHelper.FormatSpeed(totalSpeed);

        var recent = downloads.Take(5).ToList();
        RecentDownloads = new ObservableCollection<DownloadTask>(recent);
        HasRecentDownloads = recent.Count > 0;

        var active = downloads.Where(t => t.Status == DownloadStatus.Downloading).ToList();
        ActiveDownloadsList = new ObservableCollection<DownloadTask>(active);
        HasActiveDownloads = active.Count > 0;

        var completed = downloads.Where(t => t.Status == DownloadStatus.Completed).Take(5).ToList();
        CompletedDownloadsList = new ObservableCollection<DownloadTask>(completed);
        HasCompletedDownloads = completed.Count > 0;
    }

    [RelayCommand]
    private async Task LoadDownloadsAsync()
    {
        var downloads = _downloadService.GetAllDownloads();
        foreach (var download in downloads)
        {
            if (!RecentDownloads.Any(d => d.Id == download.Id))
            {
                RecentDownloads.Add(download);
            }
        }
        RefreshData();
        await Task.CompletedTask;
    }

    public void ShowClipboardBannerFor(string url)
    {
        ClipboardUrl = url;
        ShowClipboardBanner = true;
    }

    public void DismissClipboardBanner()
    {
        ShowClipboardBanner = false;
        ClipboardUrl = string.Empty;
    }
}
