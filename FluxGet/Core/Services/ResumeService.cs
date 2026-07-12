using System.Net.Http.Headers;
using FluxGet.Core.Models;
using Microsoft.Extensions.Logging;

namespace FluxGet.Core.Services;

public class ResumeService : IResumeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResumeService> _logger;
    
    public ResumeService(ILogger<ResumeService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FluxGet/2.0");
    }
    
    public async Task<bool> CheckRangeSupportAsync(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            
            var acceptRanges = response.Headers.Contains("Accept-Ranges");
            var contentLength = response.Content.Headers.ContentLength;
            
            _logger.LogDebug("Range support check for {Url}: AcceptRanges={AcceptRanges}, ContentLength={ContentLength}", 
                url, acceptRanges, contentLength);
            
            return acceptRanges && contentLength.HasValue && contentLength.Value > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check range support for {Url}", url);
            return false;
        }
    }
    
    public async Task ResumeDownloadAsync(DownloadTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resuming download: {FileName} from {DownloadedBytes} bytes", 
            task.FileName, task.DownloadedBytes);
        
        var request = new HttpRequestMessage(HttpMethod.Get, task.Url);
        
        if (task.DownloadedBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(task.DownloadedBytes, null);
        }
        
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var mode = task.DownloadedBytes > 0 ? FileMode.Append : FileMode.Create;
        
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(task.FilePath, mode, FileAccess.Write, FileShare.None);
        
        var buffer = new byte[8192];
        int bytesRead;
        long totalRead = task.DownloadedBytes;
        
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;
            task.DownloadedBytes = totalRead;
        }
    }
    
    public async Task SaveProgressAsync(DownloadTask task, IEnumerable<DownloadChunk> chunks)
    {
        _logger.LogDebug("Saving progress for {FileName}: {DownloadedBytes}/{FileSize} bytes", 
            task.FileName, task.DownloadedBytes, task.FileSize);
        
        await Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
