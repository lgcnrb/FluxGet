using System.Collections.Concurrent;
using FluxGet.Core.Models;
using Microsoft.Extensions.Logging;

namespace FluxGet.Core.Services;

public class QueueService : IQueueService, IDisposable
{
    private readonly IDownloadService _downloadService;
    private readonly ILogger<QueueService> _logger;
    
    private readonly object _lock = new();
    private readonly List<DownloadTask> _pendingQueue = new();
    private readonly ConcurrentDictionary<int, DownloadTask> _activeDownloads = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _taskCancellations = new();
    
    private int _maxConcurrent = 3;
    private int _isProcessing;
    
    public int MaxConcurrent
    {
        get => _maxConcurrent;
        set
        {
            _maxConcurrent = Math.Max(1, value);
            _downloadService.MaxConcurrent = value;
            _ = ProcessQueueAsync();
        }
    }
    
    public IReadOnlyList<DownloadTask> Queue
    {
        get
        {
            lock (_lock)
            {
                return _pendingQueue.OrderBy(t => t.Priority).ThenBy(t => t.QueueOrder).ToList().AsReadOnly();
            }
        }
    }
    
    public int ActiveCount => _activeDownloads.Count;
    
    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingQueue.Count;
            }
        }
    }
    
    public QueueService(IDownloadService downloadService, ILogger<QueueService> logger)
    {
        _downloadService = downloadService;
        _logger = logger;
    }
    
    public async Task EnqueueAsync(DownloadTask task, int priority = -1)
    {
        if (priority >= 0)
            task.Priority = priority;
        
        task.Status = DownloadStatus.Queued;
        
        lock (_lock)
        {
            task.QueueOrder = _pendingQueue.Count;
            _pendingQueue.Add(task);
            SortQueue();
        }
        
        _logger.LogInformation("Added to queue: {FileName} (Priority: {Priority}, Order: {Order})",
            task.FileName, task.Priority, task.QueueOrder);
        
        await ProcessQueueAsync();
    }
    
    public async Task<DownloadTask?> DequeueAsync()
    {
        DownloadTask? task = null;
        
        lock (_lock)
        {
            if (_pendingQueue.Count > 0)
            {
                task = _pendingQueue[0];
                _pendingQueue.RemoveAt(0);
                ReindexQueue();
            }
        }
        
        return await Task.FromResult(task);
    }
    
    public async Task RemoveFromQueueAsync(DownloadTask task)
    {
        lock (_lock)
        {
            _pendingQueue.RemoveAll(t => t.Id == task.Id);
            ReindexQueue();
        }
        
        _logger.LogInformation("Removed from queue: {FileName}", task.FileName);
        await Task.CompletedTask;
    }
    
    public async Task MoveUpAsync(DownloadTask task)
    {
        lock (_lock)
        {
            var index = _pendingQueue.FindIndex(t => t.Id == task.Id);
            if (index > 0)
            {
                if (_pendingQueue[index].Priority == _pendingQueue[index - 1].Priority)
                {
                    (_pendingQueue[index].QueueOrder, _pendingQueue[index - 1].QueueOrder) =
                        (_pendingQueue[index - 1].QueueOrder, _pendingQueue[index].QueueOrder);
                }
                else
                {
                    _pendingQueue[index].Priority++;
                }
                SortQueue();
            }
        }
        
        await Task.CompletedTask;
    }
    
    public async Task MoveDownAsync(DownloadTask task)
    {
        lock (_lock)
        {
            var index = _pendingQueue.FindIndex(t => t.Id == task.Id);
            if (index >= 0 && index < _pendingQueue.Count - 1)
            {
                if (_pendingQueue[index].Priority == _pendingQueue[index + 1].Priority)
                {
                    (_pendingQueue[index].QueueOrder, _pendingQueue[index + 1].QueueOrder) =
                        (_pendingQueue[index + 1].QueueOrder, _pendingQueue[index].QueueOrder);
                }
                else
                {
                    _pendingQueue[index].Priority--;
                }
                SortQueue();
            }
        }
        
        await Task.CompletedTask;
    }
    
    public async Task SetPriorityAsync(DownloadTask task, int priority)
    {
        task.Priority = Math.Clamp(priority, 0, 10);
        
        lock (_lock)
        {
            SortQueue();
        }
        
        _logger.LogInformation("Priority changed: {FileName} -> {Priority}", task.FileName, task.Priority);
        await Task.CompletedTask;
    }
    
    public async Task ClearQueueAsync()
    {
        lock (_lock)
        {
            foreach (var task in _pendingQueue)
            {
                task.Status = DownloadStatus.Pending;
            }
            _pendingQueue.Clear();
        }
        
        _logger.LogInformation("Queue cleared");
        await Task.CompletedTask;
    }
    
    public async Task PauseQueueAsync()
    {
        var active = _activeDownloads.Values.ToList();
        foreach (var task in active)
        {
            await _downloadService.PauseDownloadAsync(task);
            _activeDownloads.TryRemove(task.Id, out _);
        }
        
        lock (_lock)
        {
            foreach (var task in _pendingQueue)
            {
                task.Status = DownloadStatus.Pending;
            }
        }
        
        _logger.LogInformation("Queue paused");
    }
    
    public async Task ResumeQueueAsync()
    {
        await ProcessQueueAsync();
    }
    
    public int GetQueueCount()
    {
        lock (_lock)
        {
            return _pendingQueue.Count;
        }
    }
    
    public DownloadTask? GetNextTask()
    {
        lock (_lock)
        {
            return _pendingQueue.FirstOrDefault();
        }
    }
    
    public List<DownloadTask> GetPendingTasks()
    {
        lock (_lock)
        {
            return _pendingQueue.OrderBy(t => t.Priority).ThenBy(t => t.QueueOrder).ToList();
        }
    }
    
    public List<DownloadTask> GetActiveTasks()
    {
        return _activeDownloads.Values.ToList();
    }
    
    private async Task ProcessQueueAsync()
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
            return;
        
        try
        {
            while (_activeDownloads.Count < _maxConcurrent)
            {
                DownloadTask? nextTask = null;
                
                lock (_lock)
                {
                    if (_pendingQueue.Count == 0) break;
                    nextTask = _pendingQueue[0];
                    _pendingQueue.RemoveAt(0);
                    ReindexQueue();
                }
                
                if (nextTask == null) break;
                
                _activeDownloads[nextTask.Id] = nextTask;
                
                var cts = new CancellationTokenSource();
                _taskCancellations[nextTask.Id] = cts;
                
                _logger.LogInformation("Starting download: {FileName} (Priority: {Priority})",
                    nextTask.FileName, nextTask.Priority);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _downloadService.StartDownloadAsync(nextTask);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during download: {FileName}", nextTask.FileName);
                    }
                    finally
                    {
                        _activeDownloads.TryRemove(nextTask.Id, out _);
                        _taskCancellations.TryRemove(nextTask.Id, out var innerCts);
                        innerCts?.Dispose();
                    }
                });
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
            
            // Pending items in queue and capacity available, continue
            bool hasPending;
            lock (_lock) { hasPending = _pendingQueue.Count > 0; }
            
            if (hasPending && _activeDownloads.Count < _maxConcurrent)
            {
                _ = ProcessQueueAsync();
            }
        }
    }
    
    private void SortQueue()
    {
        _pendingQueue.Sort((a, b) =>
        {
            var cmp = b.Priority.CompareTo(a.Priority);
            return cmp != 0 ? cmp : a.QueueOrder.CompareTo(b.QueueOrder);
        });
        ReindexQueue();
    }
    
    private void ReindexQueue()
    {
        for (int i = 0; i < _pendingQueue.Count; i++)
        {
            _pendingQueue[i].QueueOrder = i;
        }
    }
    
    public void Dispose()
    {
        foreach (var cts in _taskCancellations.Values)
        {
            cts.Dispose();
        }
    }
}
