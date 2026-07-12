using FluxGet.Core.Models;

namespace FluxGet.Core.Services;

public interface IResumeService
{
    Task<bool> CheckRangeSupportAsync(string url);
    
    Task ResumeDownloadAsync(DownloadTask task, CancellationToken cancellationToken = default);
    
    Task SaveProgressAsync(DownloadTask task, IEnumerable<DownloadChunk> chunks);
}
