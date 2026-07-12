namespace FluxGet.Core.Services;

public interface ISpeedLimiter : IDisposable
{
    int BytesPerSecond { get; set; }
    
    bool IsEnabled { get; set; }
    
    Task WaitAsync(CancellationToken cancellationToken = default);
}
