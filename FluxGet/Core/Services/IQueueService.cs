using FluxGet.Core.Models;

namespace FluxGet.Core.Services;

public interface IQueueService
{
    int MaxConcurrent { get; set; }
    
    IReadOnlyList<DownloadTask> Queue { get; }
    
    int ActiveCount { get; }
    
    int PendingCount { get; }
    
    Task EnqueueAsync(DownloadTask task, int priority = -1);
    
    Task<DownloadTask?> DequeueAsync();
    
    Task RemoveFromQueueAsync(DownloadTask task);
    
    Task MoveUpAsync(DownloadTask task);
    
    Task MoveDownAsync(DownloadTask task);
    
    Task SetPriorityAsync(DownloadTask task, int priority);
    
    Task ClearQueueAsync();
    
    Task PauseQueueAsync();
    
    Task ResumeQueueAsync();
    
    int GetQueueCount();
    
    DownloadTask? GetNextTask();
    
    List<DownloadTask> GetPendingTasks();
    
    List<DownloadTask> GetActiveTasks();
}
