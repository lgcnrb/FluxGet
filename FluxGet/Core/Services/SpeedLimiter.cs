using System.Threading;

namespace FluxGet.Core.Services;

public class SpeedLimiter : ISpeedLimiter, IDisposable
{
    private int _bytesPerSecond;
    private long _availableTokens;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;
    
    public int BytesPerSecond
    {
        get => _bytesPerSecond;
        set
        {
            _bytesPerSecond = value;
            _availableTokens = value / 10;
        }
    }
    
    public bool IsEnabled { get; set; }
    
    public SpeedLimiter(int maxBytesPerSecond = 0)
    {
        _bytesPerSecond = maxBytesPerSecond;
        _availableTokens = maxBytesPerSecond / 10;
        IsEnabled = maxBytesPerSecond > 0;
        
        _timer = new Timer(RefillTokens, null, 0, 100); // 100ms interval
    }
    
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || _bytesPerSecond <= 0)
            return;
        
        while (Interlocked.Read(ref _availableTokens) <= 0)
        {
            if (_disposed) return;
            await Task.Delay(50, cancellationToken);
        }
        
        Interlocked.Decrement(ref _availableTokens);
    }
    
    private void RefillTokens(object? state)
    {
        if (!IsEnabled || _disposed)
            return;
        
        var tokensToAdd = _bytesPerSecond / 10; // Tokens per 100ms
        Interlocked.Add(ref _availableTokens, tokensToAdd);
        
        // Cap tokens at 2x rate for burst
        var maxTokens = _bytesPerSecond * 2 / 10;
        if (Interlocked.Read(ref _availableTokens) > maxTokens)
        {
            Interlocked.Exchange(ref _availableTokens, maxTokens);
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _semaphore?.Dispose();
    }
}
