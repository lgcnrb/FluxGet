namespace FluxGet.Core.Models;

public class DownloadProgress
{
    public int TaskId { get; set; }
    
    public double Progress { get; set; }
    
    public double Speed { get; set; }
    
    public long DownloadedBytes { get; set; }
    
    public long TotalBytes { get; set; }
    
    public TimeSpan Elapsed { get; set; }
    
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    
    public int ActiveChunks { get; set; }
    
    public List<double> SpeedHistory { get; set; } = new();
}
