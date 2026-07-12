using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FluxGet.Core.Models;

public class DownloadChunk
{
    [Key]
    public int Id { get; set; }
    
    public int DownloadTaskId { get; set; }
    
    [ForeignKey(nameof(DownloadTaskId))]
    public DownloadTask? DownloadTask { get; set; }
    
    public long StartByte { get; set; }
    
    public long EndByte { get; set; }
    
    private long _downloadedBytes;
    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set => _downloadedBytes = value;
    }
    
    private ChunkStatus _status = ChunkStatus.Pending;
    public ChunkStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            if (DownloadTask != null)
                DownloadTask.ActiveChunks = DownloadTask.Chunks?.Count(c => c.Status == ChunkStatus.Downloading) ?? 0;
        }
    }
    
    public int ChunkIndex { get; set; }
    
    public int RetryCount { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public DateTime? StartedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    [NotMapped]
    public long TotalBytes => EndByte - StartByte + 1;
    
    [NotMapped]
    public double Progress => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
}
