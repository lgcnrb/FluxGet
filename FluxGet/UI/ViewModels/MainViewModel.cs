using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Models;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxGet.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private readonly IQueueService _queueService;
    private readonly ILogger<MainViewModel> _logger;
    
    [ObservableProperty]
    private ObservableCollection<DownloadTask> _downloads = new();
    
    [ObservableProperty]
    private ObservableCollection<DownloadTask> _filteredDownloads = new();
    
    [ObservableProperty]
    private DownloadCategory? _selectedCategory;
    
    [ObservableProperty]
    private DownloadStatus? _selectedStatus;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private int _totalDownloads;
    
    [ObservableProperty]
    private int _activeDownloads;
    
    [ObservableProperty]
    private int _completedDownloads;
    
    [ObservableProperty]
    private int _pendingDownloads;
    
    [ObservableProperty]
    private double _totalSpeed;
    
    public MainViewModel(
        IDownloadService downloadService,
        IQueueService queueService,
        ILogger<MainViewModel> logger)
    {
        _downloadService = downloadService;
        _queueService = queueService;
        _logger = logger;
        
        // ProgressChanged'i dinle
        _downloadService.ProgressChanged.Subscribe(OnProgressChanged);
    }
    
    private void OnProgressChanged(DownloadProgress progress)
    {
        var dq = App.MainWindow?.DispatcherQueue;
        if (dq != null && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(() => UpdateStats());
        }
        else
        {
            UpdateStats();
        }
    }
    
    /// <summary>
    /// Indirmeleri DownloadService'den yukle (sayfa yuklenirken cagrilir)
    /// </summary>
    [RelayCommand]
    private async Task LoadDownloadsAsync()
    {
        try
        {
            var downloads = _downloadService.GetAllDownloads();
            
            foreach (var download in downloads)
            {
                if (!Downloads.Any(d => d.Id == download.Id))
                {
                    Downloads.Add(download);
                }
            }
            
            UpdateStats();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load downloads");
        }
    }
    
    /// <summary>
    /// Yeni indirme ekle: URL → HEAD istegi → dosya bilgisi → kuyruga ekle
    /// </summary>
    [RelayCommand]
    private async Task AddDownloadAsync(string url)
    {
        try
        {
            var task = await _downloadService.AddDownloadAsync(url);
            
            Downloads.Insert(0, task);
            UpdateStats();
            ApplyFilter();
            
            await _queueService.EnqueueAsync(task);
            
            _logger.LogInformation("Download added to queue: {FileName}", task.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add download");
        }
    }
    
    /// <summary>
    /// Yeni indirme ekle: URL + kaydet yolu + kategori
    /// </summary>
    public async Task AddDownloadWithOptionsAsync(string url, string? savePath, DownloadCategory category)
    {
        try
        {
            var task = await _downloadService.AddDownloadAsync(url, savePath, category);
            
            Downloads.Insert(0, task);
            UpdateStats();
            ApplyFilter();
            
            await _queueService.EnqueueAsync(task);
            
            _logger.LogInformation("Download added to queue: {FileName} (Category: {Category})", task.FileName, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add download with options");
        }
    }
    
    [RelayCommand]
    private async Task StartDownloadAsync(DownloadTask task)
    {
        try
        {
            await _queueService.EnqueueAsync(task);
            UpdateStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start download");
        }
    }
    
    [RelayCommand]
    private async Task PauseDownloadAsync(DownloadTask task)
    {
        try
        {
            await _downloadService.PauseDownloadAsync(task);
            UpdateStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause download");
        }
    }
    
    [RelayCommand]
    private async Task ResumeDownloadAsync(DownloadTask task)
    {
        try
        {
            await _downloadService.ResumeDownloadAsync(task);
            UpdateStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume download");
        }
    }
    
    [RelayCommand]
    private async Task CancelDownloadAsync(DownloadTask task)
    {
        try
        {
            await _downloadService.CancelDownloadAsync(task);
            UpdateStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel download");
        }
    }
    
    [RelayCommand]
    private async Task RemoveDownloadAsync(DownloadTask task)
    {
        try
        {
            await _downloadService.RemoveDownloadAsync(task);
            Downloads.Remove(task);
            UpdateStats();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove download");
        }
    }
    
    [RelayCommand]
    private async Task PauseAllAsync()
    {
        try
        {
            await _downloadService.PauseAllAsync();
            
            foreach (var task in Downloads.Where(t => t.Status == DownloadStatus.Downloading))
            {
                task.Status = DownloadStatus.Paused;
            }
            
            UpdateStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause all downloads");
        }
    }
    
    [RelayCommand]
    private async Task ResumeAllAsync()
    {
        try
        {
            // Duraklatilmis indirmeleri kuyruga ekle
            var paused = Downloads.Where(t => t.Status == DownloadStatus.Paused || t.Status == DownloadStatus.Pending).ToList();
            foreach (var task in paused)
            {
                await _queueService.EnqueueAsync(task);
            }
            await _queueService.ResumeQueueAsync();
            UpdateStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume all downloads");
        }
    }
    
    [RelayCommand]
    private async Task ClearCompletedAsync()
    {
        try
        {
            await _downloadService.ClearCompletedAsync();
            
            var completed = Downloads.Where(t => 
                t.Status == DownloadStatus.Completed || 
                t.Status == DownloadStatus.Cancelled).ToList();
            
            foreach (var task in completed)
            {
                Downloads.Remove(task);
            }
            
            UpdateStats();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear completed downloads");
        }
    }
    
    /// <summary>Indirme bazli hiz limiti ayarla</summary>
    [RelayCommand]
    private void SetSpeedLimit(object? parameters)
    {
        if (parameters is object[] arr && arr.Length == 2 && arr[0] is DownloadTask task && arr[1] is long limit)
        {
            _downloadService.SetDownloadSpeedLimit(task.Id, limit);
            task.SpeedLimit = limit;
            _logger.LogInformation("Speed limit set for {FileName}: {Limit}", task.FileName, FormatBytes(limit));
        }
    }
    
    /// <summary>Chunk sayisini dinamik olarak degistir</summary>
    [RelayCommand]
    private void AdjustChunks(object? parameters)
    {
        if (parameters is object[] arr && arr.Length == 2 && arr[0] is DownloadTask task && arr[1] is int count)
        {
            _downloadService.AdjustChunkCount(task, count);
        }
    }
    
    /// <summary>Dosyayi ac</summary>
    [RelayCommand]
    private async Task OpenFileAsync(DownloadTask task)
    {
        try
        {
            if (File.Exists(task.FilePath))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(task.FilePath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file: {FilePath}", task.FilePath);
        }
    }
    
    /// <summary>Klasoru ac</summary>
    [RelayCommand]
    private async Task OpenFolderAsync(DownloadTask task)
    {
        try
        {
            var folder = Path.GetDirectoryName(task.FilePath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(folder));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder: {FilePath}", task.FilePath);
        }
    }
    
    partial void OnSelectedCategoryChanged(DownloadCategory? value)
    {
        ApplyFilter();
    }
    
    partial void OnSelectedStatusChanged(DownloadStatus? value)
    {
        ApplyFilter();
    }
    
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }
    
    public void ApplyFilter()
    {
        var filtered = Downloads.AsEnumerable();
        
        if (SelectedCategory.HasValue)
        {
            filtered = filtered.Where(t => t.Category == SelectedCategory.Value);
        }
        
        if (SelectedStatus.HasValue)
        {
            filtered = filtered.Where(t => t.Status == SelectedStatus.Value);
        }
        
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(t => 
                t.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                t.Url.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }
        
        FilteredDownloads.Clear();
        foreach (var task in filtered)
        {
            FilteredDownloads.Add(task);
        }
    }
    
    public void UpdateStats()
    {
        TotalDownloads = Downloads.Count;
        ActiveDownloads = Downloads.Count(t => t.Status == DownloadStatus.Downloading);
        CompletedDownloads = Downloads.Count(t => t.Status == DownloadStatus.Completed);
        PendingDownloads = Downloads.Count(t => t.Status == DownloadStatus.Pending);
        TotalSpeed = Downloads.Where(t => t.Status == DownloadStatus.Downloading).Sum(t => t.Speed);
    }
    
    private static string FormatBytes(long bytes) => bytes switch
    {
        0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
