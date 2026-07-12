using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Models;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxGet.UI.ViewModels;

public partial class QueueViewModel : ObservableObject
{
    private readonly IQueueService _queueService;
    private readonly IDownloadService _downloadService;
    private readonly ILogger<QueueViewModel> _logger;
    
    [ObservableProperty]
    private ObservableCollection<DownloadTask> _queueItems = new();
    
    [ObservableProperty]
    private int _maxConcurrent = 3;
    
    [ObservableProperty]
    private int _currentActive;
    
    [ObservableProperty]
    private bool _isProcessing;
    
    public QueueViewModel(
        IQueueService queueService,
        IDownloadService downloadService,
        ILogger<QueueViewModel> logger)
    {
        _queueService = queueService;
        _downloadService = downloadService;
        _logger = logger;
        MaxConcurrent = queueService.MaxConcurrent;
    }
    
    [RelayCommand]
    private async Task LoadQueueAsync()
    {
        try
        {
            var pending = _queueService.GetPendingTasks();
            var active = _queueService.GetActiveTasks();
            
            QueueItems.Clear();
            
            foreach (var item in active)
            {
                QueueItems.Add(item);
            }
            
            foreach (var item in pending)
            {
                QueueItems.Add(item);
            }
            
            CurrentActive = _queueService.ActiveCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load queue");
        }
    }
    
    [RelayCommand]
    private async Task UpdateMaxConcurrentAsync()
    {
        try
        {
            _queueService.MaxConcurrent = MaxConcurrent;
            _logger.LogInformation("Max concurrent downloads updated to {MaxConcurrent}", MaxConcurrent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update max concurrent");
        }
    }
    
    [RelayCommand]
    private async Task MoveUpAsync(DownloadTask task)
    {
        try
        {
            await _queueService.MoveUpAsync(task);
            await LoadQueueAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move task up");
        }
    }
    
    [RelayCommand]
    private async Task MoveDownAsync(DownloadTask task)
    {
        try
        {
            await _queueService.MoveDownAsync(task);
            await LoadQueueAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move task down");
        }
    }
    
    [RelayCommand]
    private async Task RemoveFromQueueAsync(DownloadTask task)
    {
        try
        {
            await _queueService.RemoveFromQueueAsync(task);
            QueueItems.Remove(task);
            CurrentActive = _queueService.ActiveCount;
            
            _logger.LogInformation("Removed from queue: {FileName}", task.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from queue");
        }
    }
    
    [RelayCommand]
    private async Task ClearQueueAsync()
    {
        try
        {
            await _queueService.ClearQueueAsync();
            QueueItems.Clear();
            CurrentActive = 0;
            
            _logger.LogInformation("Queue cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear queue");
        }
    }
}
