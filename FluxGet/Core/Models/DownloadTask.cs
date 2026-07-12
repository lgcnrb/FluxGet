using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FluxGet.Core.Models;

public class DownloadTask : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged(string propertyName)
    {
        var handler = PropertyChanged;
        if (handler == null) return;
        
        try
        {
            var dq = App.MainWindow?.DispatcherQueue;
            if (dq != null && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(() => handler.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
            else
            {
                handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        catch
        {
            handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Url { get; set; } = string.Empty;
    
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string FilePath { get; set; } = string.Empty;
    
    private long _fileSize;
    public long FileSize
    {
        get => _fileSize;
        set { _fileSize = value; OnPropertyChanged(nameof(FileSize)); OnPropertyChanged(nameof(SizeText)); OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(EtaText)); }
    }
    
    private long _downloadedBytes;
    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set { _downloadedBytes = value; OnPropertyChanged(nameof(DownloadedBytes)); OnPropertyChanged(nameof(SizeText)); OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(EtaText)); }
    }
    
    private DownloadStatus _status = DownloadStatus.Pending;
    public DownloadStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(IsDownloading)); OnPropertyChanged(nameof(IsPaused)); OnPropertyChanged(nameof(IsCompleted)); OnPropertyChanged(nameof(IsError)); OnPropertyChanged(nameof(IsQueued)); }
    }
    
    public DownloadCategory Category { get; set; } = DownloadCategory.General;
    
    public string? ContentType { get; set; }
    
    private double _speed;
    public double Speed
    {
        get => _speed;
        set { _speed = value; OnPropertyChanged(nameof(Speed)); OnPropertyChanged(nameof(SpeedText)); OnPropertyChanged(nameof(EtaText)); }
    }
    
    private long _speedLimit;
    public long SpeedLimit
    {
        get => _speedLimit;
        set { _speedLimit = value; OnPropertyChanged(nameof(SpeedLimit)); OnPropertyChanged(nameof(SpeedLimitText)); }
    }
    
    private int _priority = 5;
    public int Priority
    {
        get => _priority;
        set { _priority = value; OnPropertyChanged(nameof(Priority)); OnPropertyChanged(nameof(PriorityText)); }
    }
    
    public int MaxRetries { get; set; } = 3;
    
    private int _retryCount;
    public int RetryCount
    {
        get => _retryCount;
        set { _retryCount = value; OnPropertyChanged(nameof(RetryCount)); OnPropertyChanged(nameof(RetryText)); }
    }
    
    public string? ExpectedHash { get; set; }
    
    public string? HashAlgorithm { get; set; }
    
    public string? OriginalUrl { get; set; }
    
    public DateTime? StartedAt { get; set; }
    
    public int QueueOrder { get; set; }
    
    public string? Referrer { get; set; }
    
    public string? UserAgent { get; set; }
    
    public string? Cookies { get; set; }
    
    private int _chunkCount = 1;
    public int ChunkCount
    {
        get => _chunkCount;
        set { _chunkCount = value; OnPropertyChanged(nameof(ChunkCount)); }
    }
    
    private int _activeChunks;
    public int ActiveChunks
    {
        get => _activeChunks;
        set { _activeChunks = value; OnPropertyChanged(nameof(ActiveChunks)); OnPropertyChanged(nameof(StatusText)); }
    }
    
    public bool SupportsResume { get; set; }
    
    public string? UrlHash { get; set; }
    
    public ICollection<DownloadChunk> Chunks { get; set; } = new List<DownloadChunk>();
    
    public List<double> SpeedHistory { get; set; } = new();
    
    public DateTime? EstimatedCompletion { get; set; }
    
    public string? ErrorCode { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    public DateTime? LastRetryAt { get; set; }
    
    public string? Protocol { get; set; }
    
    public long TotalChunksDownloaded { get; set; }
    
    public TimeSpan? ElapsedTime => StartedAt.HasValue ? DateTime.UtcNow - StartedAt.Value : null;
    
    public string ElapsedText
    {
        get
        {
            var elapsed = ElapsedTime;
            if (elapsed == null) return "";
            return elapsed.Value.TotalHours >= 1
                ? $"{(int)elapsed.Value.TotalHours}sa {elapsed.Value.Minutes}dk"
                : elapsed.Value.TotalMinutes >= 1
                    ? $"{(int)elapsed.Value.TotalMinutes}dk {elapsed.Value.Seconds}sn"
                    : $"{(int)elapsed.Value.TotalSeconds}sn";
        }
    }
    
    // ---- Computed Properties ----
    
    public double Progress => FileSize > 0 ? (double)DownloadedBytes / FileSize * 100 : 0;
    
    public string ProgressText => Progress >= 100 ? "%100" : $"%{Progress:F1}";
    
    public string StatusText => Status switch
    {
        DownloadStatus.Pending => "Beklemede",
        DownloadStatus.Downloading => ActiveChunks > 1 ? $"Indiriliyor ({ActiveChunks} chunk)" : "Indiriliyor",
        DownloadStatus.Paused => "Duraklatildi",
        DownloadStatus.Completed => "Tamamlandi",
        DownloadStatus.Error => $"Hata{(RetryCount > 0 ? $" (Deneme {RetryCount}/{MaxRetries})" : "")}{(ErrorCode != null ? $" - {ErrorCode}" : "")}",
        DownloadStatus.Cancelled => "Iptal Edildi",
        DownloadStatus.Queued => $"Kuyrukta ({QueueOrder})",
        _ => "Bilinmiyor"
    };
    
    public string SpeedText => Speed switch
    {
        0 => "",
        < 1024 => $"{Speed:F0} B/s",
        < 1024 * 1024 => $"{Speed / 1024:F1} KB/s",
        _ => $"{Speed / (1024 * 1024):F2} MB/s"
    };
    
    public string SizeText => FileSize switch
    {
        0 => "",
        < 1024 => $"{DownloadedBytes} / {FileSize} B",
        < 1024 * 1024 => $"{DownloadedBytes / 1024.0:F1} / {FileSize / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{DownloadedBytes / (1024.0 * 1024):F1} / {FileSize / (1024.0 * 1024):F1} MB",
        _ => $"{DownloadedBytes / (1024.0 * 1024 * 1024):F2} / {FileSize / (1024.0 * 1024 * 1024):F2} GB"
    };
    
    public string EtaText
    {
        get
        {
            if (Speed <= 0 || FileSize <= 0 || DownloadedBytes >= FileSize) return "";
            var remaining = FileSize - DownloadedBytes;
            var eta = TimeSpan.FromSeconds(remaining / Speed);
            return eta.TotalHours >= 1
                ? $"{(int)eta.TotalHours}sa {eta.Minutes}dk"
                : eta.TotalMinutes >= 1
                    ? $"{(int)eta.TotalMinutes}dk {eta.Seconds}sn"
                    : $"{(int)eta.TotalSeconds}sn";
        }
    }
    
    public string SpeedLimitText => SpeedLimit switch
    {
        0 => "Sinirsiz",
        < 1024 => $"{SpeedLimit} B/s",
        < 1024 * 1024 => $"{SpeedLimit / 1024.0:F1} KB/s",
        _ => $"{SpeedLimit / (1024.0 * 1024):F2} MB/s"
    };
    
    public string PriorityText => Priority switch
    {
        >= 9 => "Cok Yuksek",
        >= 7 => "Yuksek",
        >= 4 => "Normal",
        >= 2 => "Dusuk",
        _ => "Cok Dusuk"
    };
    
    public string RetryText => RetryCount > 0 ? $"Yeniden deneniyor ({RetryCount}/{MaxRetries})..." : "";
    
    public bool IsDownloading => Status == DownloadStatus.Downloading;
    public bool IsPaused => Status == DownloadStatus.Paused;
    public bool IsCompleted => Status == DownloadStatus.Completed;
    public bool IsError => Status == DownloadStatus.Error;
    public bool IsQueued => Status == DownloadStatus.Queued;
    
    [NotMapped]
    public string DisplayUrl => OriginalUrl ?? Url;
    
    [NotMapped]
    public string CategoryIcon => Category switch
    {
        DownloadCategory.Video => "\uE714",
        DownloadCategory.Music => "\uE8D6",
        DownloadCategory.Document => "\uE8A5",
        DownloadCategory.Archive => "\uF012",
        DownloadCategory.Image => "\uE8B9",
        DownloadCategory.Software => "\uE74E",
        _ => "\uE896"
    };
}
