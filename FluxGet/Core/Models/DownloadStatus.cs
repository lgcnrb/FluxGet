namespace FluxGet.Core.Models;

public enum DownloadStatus
{
    Pending = 0,
    Downloading = 1,
    Paused = 2,
    Completed = 3,
    Error = 4,
    Cancelled = 5,
    Queued = 6
}
