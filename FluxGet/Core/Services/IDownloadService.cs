using FluxGet.Core.Models;

namespace FluxGet.Core.Services;

public interface IDownloadService
{
    IObservable<DownloadProgress> ProgressChanged { get; }
    
    int MaxConcurrent { get; set; }
    
    Task<DownloadTask> AddDownloadAsync(string url, string? savePath = null, DownloadCategory category = DownloadCategory.General);
    
    Task StartDownloadAsync(DownloadTask task);
    
    Task PauseDownloadAsync(DownloadTask task);
    
    Task ResumeDownloadAsync(DownloadTask task);
    
    Task CancelDownloadAsync(DownloadTask task);
    
    Task RemoveDownloadAsync(DownloadTask task);
    
    IReadOnlyList<DownloadTask> GetAllDownloads();
    
    DownloadTask? GetById(int id);
    
    Task PauseAllAsync();
    
    Task ResumeAllAsync();
    
    Task ClearCompletedAsync();
    
    void SetDownloadSpeedLimit(int taskId, long bytesPerSecond);
    
    void AdjustChunkCount(DownloadTask task, int newChunkCount);
    
    Task LoadAllAsync();
    
    Task SaveAsync(DownloadTask task);
    
    Task SaveChunksAsync(DownloadTask task);
}
