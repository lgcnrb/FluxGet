using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Models;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxGet.UI.ViewModels;

public partial class DownloadDetailViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private readonly ILogger<DownloadDetailViewModel> _logger;
    
    [ObservableProperty]
    private DownloadTask? _currentTask;
    
    [ObservableProperty]
    private string _fileName = string.Empty;
    
    [ObservableProperty]
    private string _url = string.Empty;
    
    [ObservableProperty]
    private string _filePath = string.Empty;
    
    [ObservableProperty]
    private long _fileSize;
    
    [ObservableProperty]
    private long _downloadedBytes;
    
    [ObservableProperty]
    private double _progress;
    
    [ObservableProperty]
    private double _speed;
    
    [ObservableProperty]
    private DownloadStatus _status;
    
    [ObservableProperty]
    private DownloadCategory _category;
    
    [ObservableProperty]
    private DateTime _createdAt;
    
    [ObservableProperty]
    private DateTime? _completedAt;
    
    [ObservableProperty]
    private string _formattedFileSize = string.Empty;
    
    [ObservableProperty]
    private string _formattedDownloadedBytes = string.Empty;
    
    [ObservableProperty]
    private string _formattedSpeed = string.Empty;
    
    [ObservableProperty]
    private string _estimatedTimeRemaining = string.Empty;
    
    public DownloadDetailViewModel(IDownloadService downloadService, ILogger<DownloadDetailViewModel> logger)
    {
        _downloadService = downloadService;
        _logger = logger;
    }
    
    public void LoadTask(DownloadTask task)
    {
        CurrentTask = task;
        FileName = task.FileName;
        Url = task.Url;
        FilePath = task.FilePath;
        FileSize = task.FileSize;
        DownloadedBytes = task.DownloadedBytes;
        Progress = task.Progress;
        Speed = task.Speed;
        Status = task.Status;
        Category = task.Category;
        CreatedAt = task.CreatedAt;
        CompletedAt = task.CompletedAt;
        
        UpdateFormattedValues();
    }
    
    [RelayCommand]
    private async Task StartAsync()
    {
        if (CurrentTask == null) return;
        
        try
        {
            await _downloadService.StartDownloadAsync(CurrentTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start download");
        }
    }
    
    [RelayCommand]
    private async Task PauseAsync()
    {
        if (CurrentTask == null) return;
        
        try
        {
            await _downloadService.PauseDownloadAsync(CurrentTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause download");
        }
    }
    
    [RelayCommand]
    private async Task ResumeAsync()
    {
        if (CurrentTask == null) return;
        
        try
        {
            await _downloadService.ResumeDownloadAsync(CurrentTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume download");
        }
    }
    
    [RelayCommand]
    private async Task CancelAsync()
    {
        if (CurrentTask == null) return;
        
        try
        {
            await _downloadService.CancelDownloadAsync(CurrentTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel download");
        }
    }
    
    [RelayCommand]
    private void OpenFile()
    {
        if (CurrentTask == null || !File.Exists(CurrentTask.FilePath)) return;
        
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = CurrentTask.FilePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file");
        }
    }
    
    [RelayCommand]
    private void OpenFolder()
    {
        if (CurrentTask == null) return;
        
        try
        {
            var directory = Path.GetDirectoryName(CurrentTask.FilePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder");
        }
    }
    
    private void UpdateFormattedValues()
    {
        FormattedFileSize = Core.Helpers.FileHelper.FormatBytes(FileSize);
        FormattedDownloadedBytes = Core.Helpers.FileHelper.FormatBytes(DownloadedBytes);
        FormattedSpeed = $"{Core.Helpers.FileHelper.FormatBytes((long)Speed)}/s";
        
        if (Speed > 0 && FileSize > DownloadedBytes)
        {
            var remaining = TimeSpan.FromSeconds((FileSize - DownloadedBytes) / Speed);
            EstimatedTimeRemaining = remaining.TotalHours >= 1 
                ? $"{remaining.Hours}h {remaining.Minutes}m {remaining.Seconds}s"
                : remaining.TotalMinutes >= 1 
                    ? $"{remaining.Minutes}m {remaining.Seconds}s"
                    : $"{remaining.Seconds}s";
        }
        else
        {
            EstimatedTimeRemaining = "--";
        }
    }
}
