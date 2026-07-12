using FluxGet.Core.Models;

namespace FluxGet.Core.Services;

public interface IBrowserExtensionService
{
    int Port { get; }
    
    bool IsRunning { get; }
    
    string GetApiToken();
    
    Task StartAsync();
    
    Task StopAsync();
    
    event EventHandler<DownloadRequest>? DownloadRequested;
    
    event EventHandler<YouTubeDownloadRequest>? YouTubeDownloadRequested;
}

public class YouTubeDownloadRequest
{
    public string Url { get; set; } = string.Empty;
    public int Resolution { get; set; } = 720;
    public string Format { get; set; } = "mp4";
}

public class DownloadRequest
{
    public string Url { get; set; } = string.Empty;
    
    public string? FileName { get; set; }
    
    public string? Referrer { get; set; }
    
    public int? Resolution { get; set; }
    
    public string? Format { get; set; }
}
