using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using FluxGet.Core.Data;
using FluxGet.Core.Helpers;
using FluxGet.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FluxGet.Core.Services;

public partial class DownloadService : IDownloadService, IDisposable
{
    private readonly IResumeService _resumeService;
    private readonly ISpeedLimiter _globalSpeedLimiter;
    private readonly ILogger<DownloadService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _chunkCancellations = new();
    private readonly ConcurrentDictionary<int, DownloadTask> _allDownloads = new();
    private readonly ConcurrentDictionary<int, ISpeedLimiter> _downloadSpeedLimiters = new();
    private readonly Subject<DownloadProgress> _progressSubject = new();
    private readonly ConcurrentDictionary<int, List<double>> _speedHistory = new();
    private readonly ConcurrentDictionary<int, HttpClient> _downloadHttpClients = new();
    private readonly ConcurrentDictionary<int, object> _chunkFileLocks = new();
    private readonly ConcurrentDictionary<int, long> _taskLastUiUpdateTicks = new();
    
    private int _nextId = 1;
    private int _maxConcurrent = 3;
    
    public IObservable<DownloadProgress> ProgressChanged => _progressSubject.AsObservable();
    
    public int MaxConcurrent
    {
        get => _maxConcurrent;
        set
        {
            _maxConcurrent = Math.Max(1, value);
        }
    }
    
    public DownloadService(
        IResumeService resumeService,
        ISpeedLimiter globalSpeedLimiter,
        IServiceScopeFactory scopeFactory,
        ILogger<DownloadService> logger)
    {
        _resumeService = resumeService;
        _globalSpeedLimiter = globalSpeedLimiter;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    
    #region Public API
    
    public async Task LoadAllAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var tasks = await db.DownloadTasks
                .Include(t => t.Chunks)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            
            foreach (var task in tasks)
            {
                task.Chunks ??= new List<DownloadChunk>();
                _allDownloads[task.Id] = task;
                
                if (task.Id >= _nextId)
                    _nextId = task.Id + 1;
                
                if (task.Status == DownloadStatus.Downloading || task.Status == DownloadStatus.Queued)
                    task.Status = DownloadStatus.Pending;
            }
            
            _logger.LogInformation("Loaded {Count} downloads from database", tasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load downloads from database");
        }
    }
    
    public async Task SaveAsync(DownloadTask task)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            task.UpdatedAt = DateTime.UtcNow;
            
            var existing = await db.DownloadTasks.FindAsync(task.Id);
            if (existing != null)
            {
                db.Entry(existing).CurrentValues.SetValues(task);
            }
            else
            {
                db.DownloadTasks.Add(task);
            }
            
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save download: {FileName}", task.FileName);
        }
    }
    
    public async Task SaveChunksAsync(DownloadTask task)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            foreach (var chunk in task.Chunks)
            {
                var existing = await db.DownloadChunks.FindAsync(chunk.Id);
                if (existing != null)
                {
                    existing.DownloadedBytes = chunk.DownloadedBytes;
                    existing.Status = chunk.Status;
                    existing.RetryCount = chunk.RetryCount;
                    existing.ErrorMessage = chunk.ErrorMessage;
                }
            }
            
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save chunk states: {FileName}", task.FileName);
        }
    }
    
    private async Task DeleteFromDbAsync(int taskId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var chunks = await db.DownloadChunks.Where(c => c.DownloadTaskId == taskId).ToListAsync();
            db.DownloadChunks.RemoveRange(chunks);
            
            var task = await db.DownloadTasks.FindAsync(taskId);
            if (task != null)
            {
                db.DownloadTasks.Remove(task);
            }
            
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete from database: {TaskId}", taskId);
        }
    }
    
    private static HttpClient CreateDownloadHttpClient(DownloadTask task)
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = Math.Max(task.ChunkCount, 4),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            UseCookies = true,
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        client.DefaultRequestHeaders.Add("User-Agent", task.UserAgent ?? "FluxGet/2.0");
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
        
        if (!string.IsNullOrEmpty(task.Referrer))
            client.DefaultRequestHeaders.Referrer = new Uri(task.Referrer);
        
        if (!string.IsNullOrEmpty(task.Cookies))
            client.DefaultRequestHeaders.Add("Cookie", task.Cookies);
        
        return client;
    }
    
    private static string DetectProtocol(string url)
    {
        if (url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)) return "FTP";
        if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)) return "Magnet";
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "HTTPS";
        return "HTTP";
    }
    
    public async Task<DownloadTask> AddDownloadAsync(string url, string? savePath = null, DownloadCategory category = DownloadCategory.General)
    {
        var protocol = DetectProtocol(url);
        
        var (fileName, fileSize, supportsRange, contentType) = await GetFileInfoAsync(url);
        
        if (string.IsNullOrEmpty(fileName))
            fileName = "download";
        
        // Clean invalid characters from filename
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        
        // Auto-detect category
        if (category == DownloadCategory.General)
        {
            category = Core.Helpers.UrlHelper.DetectCategory(url, contentType);
        }
        
        var filePath = savePath ?? Path.Combine(GetDefaultDownloadPath(), fileName);
        
        // If savePath is a directory (or has no extension), append the filename
        if (string.IsNullOrEmpty(filePath) || Directory.Exists(filePath) || !Path.HasExtension(filePath))
        {
            filePath = Path.Combine(filePath, fileName);
        }
        
        filePath = GetUniqueFilePath(filePath);
        
        var task = new DownloadTask
        {
            Id = Interlocked.Increment(ref _nextId),
            Url = url,
            OriginalUrl = url,
            FileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            ContentType = contentType,
            Category = category,
            Status = DownloadStatus.Pending,
            SupportsResume = supportsRange,
            Protocol = protocol,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        if (fileSize > 0 && supportsRange)
        {
            task.ChunkCount = CalculateChunkCount(fileSize);
        }
        
        task.UrlHash = ComputeHash(url);
        
        _allDownloads[task.Id] = task;
        
        await SaveAsync(task);
        
        _logger.LogInformation("Download prepared: {FileName} ({FileSize}, {ChunkCount} chunks, Protocol={Protocol})",
            fileName, FileHelper.FormatBytes(fileSize), task.ChunkCount, protocol);
        
        return task;
    }
    
    public async Task StartDownloadAsync(DownloadTask task)
    {
        if (task.Status == DownloadStatus.Downloading)
            return;
        
        task.Status = DownloadStatus.Downloading;
        task.StartedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        task.RetryCount = 0;
        
        await SaveAsync(task);
        
        var maxRetries = task.MaxRetries;
        Exception? lastException = null;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            var cts = new CancellationTokenSource();
            HttpClient? httpClient;
            
            try
            {
                httpClient = CreateDownloadHttpClient(task);
                _downloadHttpClients[task.Id] = httpClient;
                _chunkCancellations[task.Id] = cts;
                
                if (task.SpeedLimit > 0)
                {
                    var downloadLimiter = new SpeedLimiter((int)task.SpeedLimit);
                    _downloadSpeedLimiters[task.Id] = downloadLimiter;
                }
                
                if (attempt > 0)
                {
                    task.RetryCount = attempt;
                    task.LastRetryAt = DateTime.UtcNow;
                    task.Status = DownloadStatus.Downloading;
                    await SaveAsync(task);
                    
                    _logger.LogWarning("Retrying ({Attempt}/{Max}): {FileName}",
                        attempt, maxRetries, task.FileName);
                }
                else
                {
                    _logger.LogInformation("Download started: {FileName} ({ChunkCount} chunks)", task.FileName, task.ChunkCount);
                }
                
                await StartDownloadInternalAsync(task, httpClient, cts.Token);
                
                // Success
                lastException = null;
                break;
            }
            catch (OperationCanceledException)
            {
                if (task.Status == DownloadStatus.Downloading)
                {
                    task.Status = DownloadStatus.Paused;
                    _logger.LogInformation("Download paused: {FileName}", task.FileName);
                }
                lastException = null;
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Download error ({Attempt}/{Max}): {FileName} - {Error}",
                    attempt + 1, maxRetries + 1, task.FileName, ex.Message);
                
                if (attempt < maxRetries)
                {
                    var delay = CalculateRetryDelay(attempt + 1);
                    _logger.LogInformation("{Delay}s until retry...", delay.TotalSeconds);
                    await Task.Delay(delay, CancellationToken.None);
                }
            }
            finally
            {
                CleanupDownload(task.Id);
                cts.Dispose();
            }
        }
        
        if (lastException != null)
        {
            task.Status = DownloadStatus.Error;
            task.ErrorCode = lastException.Message;
            _logger.LogError(lastException, "Download permanently failed: {FileName}", task.FileName);
        }
        
        task.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(task);
        await SaveChunksAsync(task);
        EmitProgress(task);
    }
    
    private void CleanupDownload(int taskId)
    {
        _chunkCancellations.TryRemove(taskId, out _);
        _downloadSpeedLimiters.TryRemove(taskId, out _);
        _chunkFileLocks.TryRemove(taskId, out _);
        _taskLastUiUpdateTicks.TryRemove(taskId, out _);
        
        if (_downloadHttpClients.TryRemove(taskId, out var client))
        {
            client.Dispose();
        }
    }
    
    public async Task PauseDownloadAsync(DownloadTask task)
    {
        if (task.Status != DownloadStatus.Downloading)
            return;
        
        if (_chunkCancellations.TryGetValue(task.Id, out var cts))
        {
            task.Status = DownloadStatus.Paused;
            task.UpdatedAt = DateTime.UtcNow;
            await cts.CancelAsync();
            await SaveAsync(task);
            await SaveChunksAsync(task);
            _logger.LogInformation("Download paused: {FileName}", task.FileName);
        }
    }
    
    public async Task ResumeDownloadAsync(DownloadTask task)
    {
        if (task.Status == DownloadStatus.Paused || task.Status == DownloadStatus.Error)
        {
            task.RetryCount = 0;
            task.ErrorCode = null;
            _ = Task.Run(async () =>
            {
                try
                {
                    await StartDownloadAsync(task);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resuming download: {FileName}", task.FileName);
                }
            });
        }
        await Task.CompletedTask;
    }
    
    public async Task CancelDownloadAsync(DownloadTask task)
    {
        if (_chunkCancellations.TryGetValue(task.Id, out var cts))
        {
            task.Status = DownloadStatus.Cancelled;
            await cts.CancelAsync();
        }
        else
        {
            task.Status = DownloadStatus.Cancelled;
        }
        
        if (File.Exists(task.FilePath))
        {
            try { File.Delete(task.FilePath); }
            catch (Exception ex) {             _logger.LogWarning(ex, "Could not delete file: {FilePath}", task.FilePath); }
        }
        
        task.Chunks?.Clear();
        task.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(task);
    }
    
    public async Task RemoveDownloadAsync(DownloadTask task)
    {
        await CancelDownloadAsync(task);
        _allDownloads.TryRemove(task.Id, out _);
        _speedHistory.TryRemove(task.Id, out _);
        await DeleteFromDbAsync(task.Id);
    }
    
    public IReadOnlyList<DownloadTask> GetAllDownloads()
    {
        return _allDownloads.Values.OrderByDescending(t => t.CreatedAt).ToList().AsReadOnly();
    }
    
    public DownloadTask? GetById(int id)
    {
        _allDownloads.TryGetValue(id, out var task);
        return task;
    }
    
    public async Task PauseAllAsync()
    {
        var active = _allDownloads.Values.Where(t => t.Status == DownloadStatus.Downloading).ToList();
        foreach (var task in active)
        {
            await PauseDownloadAsync(task);
        }
    }
    
    public async Task ResumeAllAsync()
    {
        var paused = _allDownloads.Values.Where(t => t.Status == DownloadStatus.Paused).ToList();
        foreach (var task in paused)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await StartDownloadAsync(task);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resuming download: {FileName}", task.FileName);
                }
            });
            await Task.Delay(200);
        }
    }
    
    public async Task ClearCompletedAsync()
    {
        var completed = _allDownloads.Values
            .Where(t => t.Status == DownloadStatus.Completed || t.Status == DownloadStatus.Cancelled)
            .ToList();
        
        foreach (var task in completed)
        {
            _allDownloads.TryRemove(task.Id, out _);
            _speedHistory.TryRemove(task.Id, out _);
            await DeleteFromDbAsync(task.Id);
        }
    }
    
    public void SetDownloadSpeedLimit(int taskId, long bytesPerSecond)
    {
        if (_downloadSpeedLimiters.TryGetValue(taskId, out var limiter))
        {
            limiter.BytesPerSecond = (int)bytesPerSecond;
            limiter.IsEnabled = bytesPerSecond > 0;
        }
        
        if (_allDownloads.TryGetValue(taskId, out var task))
        {
            task.SpeedLimit = bytesPerSecond;
            _ = Task.Run(async () =>
            {
                try
                {
                    await SaveAsync(task);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save speed limit for {FileName}", task.FileName);
                }
            });
        }
    }
    
    public void AdjustChunkCount(DownloadTask task, int newChunkCount)
    {
        newChunkCount = Math.Clamp(newChunkCount, 1, 16);
        if (task.ChunkCount != newChunkCount)
        {
            _logger.LogInformation("Chunk count changed: {FileName}: {Old} -> {New}",
                task.FileName, task.ChunkCount, newChunkCount);
            task.ChunkCount = newChunkCount;
        }
    }
    
    #endregion
    
    #region Private - Download Logic
    
    private async Task StartDownloadInternalAsync(DownloadTask task, HttpClient httpClient, CancellationToken cancellationToken)
    {
        task.Chunks ??= new List<DownloadChunk>();
        var hasChunkProgress = task.Chunks.Any(c => c.DownloadedBytes > 0 && c.Status != ChunkStatus.Completed);
        
        _logger.LogInformation("StartDownloadInternal: SupportsResume={Supports}, ChunkCount={Chunks}, FileSize={Size}, HasProgress={HasProgress}",
            task.SupportsResume, task.ChunkCount, FileHelper.FormatBytes(task.FileSize), hasChunkProgress);
        
        if (task.SupportsResume && task.ChunkCount > 1 && task.FileSize > 0)
        {
            if (!hasChunkProgress)
            {
                _logger.LogInformation("Starting chunk download: {ChunkCount} chunks, {FileSize}", task.ChunkCount, FileHelper.FormatBytes(task.FileSize));
                await CreateChunksAsync(task);
            }
            else
            {
                _logger.LogInformation("Resuming chunk download: {FileName}", task.FileName);
            }
            
            await DownloadWithChunksAsync(task, httpClient, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Single connection download (Range={Supports}, Size={Size})", task.SupportsResume, FileHelper.FormatBytes(task.FileSize));
            await DownloadSingleAsync(task, httpClient, cancellationToken);
        }
        
        if (task.Status == DownloadStatus.Downloading)
        {
            if (!string.IsNullOrEmpty(task.ExpectedHash) && !string.IsNullOrEmpty(task.HashAlgorithm))
            {
                var isValid = await VerifyFileHashAsync(task);
                if (!isValid)
                {
                    throw new InvalidOperationException($"Hash verification failed: {task.FileName}");
                }
            }
            
            task.Status = DownloadStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.DownloadedBytes = task.FileSize;
            task.Chunks?.Clear();
            
            await SaveAsync(task);
            
            var duration = task.StartedAt.HasValue ? DateTime.UtcNow - task.StartedAt.Value : TimeSpan.Zero;
            _logger.LogInformation("Download completed: {FileName} ({Duration})", task.FileName, duration.ToString(@"mm\:ss"));
        }
    }
    
    private async Task CreateChunksAsync(DownloadTask task)
    {
        var chunkSize = task.FileSize / task.ChunkCount;
        
        if (!File.Exists(task.FilePath))
        {
            var dir = Path.GetDirectoryName(task.FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            
            using var fs = new FileStream(task.FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            fs.SetLength(task.FileSize);
        }
        
        task.Chunks.Clear();
        for (int i = 0; i < task.ChunkCount; i++)
        {
            var startByte = (long)i * chunkSize;
            var endByte = (i == task.ChunkCount - 1) ? task.FileSize - 1 : ((long)(i + 1) * chunkSize) - 1;
            
            var chunk = new DownloadChunk
            {
                DownloadTaskId = task.Id,
                ChunkIndex = i,
                StartByte = startByte,
                EndByte = endByte,
                DownloadedBytes = 0,
                Status = ChunkStatus.Pending
            };
            
            task.Chunks.Add(chunk);
        }
    }
    
    private async Task DownloadWithChunksAsync(DownloadTask task, HttpClient httpClient, CancellationToken cancellationToken)
    {
        task.ActiveChunks = task.ChunkCount;
        task.Chunks ??= new List<DownloadChunk>();
        
        var semaphore = new SemaphoreSlim(task.ChunkCount, task.ChunkCount);
        var chunkTasks = task.Chunks
            .Where(c => c.Status != ChunkStatus.Completed)
            .Select(chunk => DownloadChunkSafeAsync(task, chunk, httpClient, semaphore, cancellationToken))
            .ToList();
        
        await Task.WhenAll(chunkTasks);
        
        if (task.Chunks.All(c => c.Status == ChunkStatus.Completed))
        {
            task.DownloadedBytes = task.FileSize;
        }
        
        task.ActiveChunks = 0;
    }
    
    private async Task DownloadChunkSafeAsync(DownloadTask task, DownloadChunk chunk, HttpClient httpClient, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Chunk failed, retry sequentially
            var chunkRetryCount = 0;
            var maxChunkRetries = 3;
            
            while (chunkRetryCount <= maxChunkRetries)
            {
                try
                {
                    await DownloadChunkAsync(task, chunk, httpClient, cancellationToken);
                    return;
                }
                catch (OperationCanceledException)
                {
                    chunk.Status = ChunkStatus.Pending;
                    throw;
                }
                catch (Exception ex)
                {
                    chunkRetryCount++;
                    chunk.RetryCount = chunkRetryCount;
                    chunk.ErrorMessage = ex.Message;
                    
                    if (chunkRetryCount <= maxChunkRetries)
                    {
                        var delay = CalculateRetryDelay(chunkRetryCount);
                        _logger.LogWarning(ex, "Chunk {ChunkIndex} failed, retrying in {Delay}s ({Retry}/{Max}): {FileName}",
                            chunk.ChunkIndex, delay.TotalSeconds, chunkRetryCount, maxChunkRetries, task.FileName);
                        
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        chunk.Status = ChunkStatus.Error;
                        _logger.LogError(ex, "Chunk {ChunkIndex} permanently failed: {FileName}", chunk.ChunkIndex, task.FileName);
                        throw;
                    }
                }
            }
        }
        finally
        {
            semaphore.Release();
            task.Chunks ??= new List<DownloadChunk>();
            task.ActiveChunks = task.Chunks.Count(c => c.Status == ChunkStatus.Downloading);
        }
    }
    
    private async Task DownloadChunkAsync(DownloadTask task, DownloadChunk chunk, HttpClient httpClient, CancellationToken cancellationToken)
    {
        chunk.Status = ChunkStatus.Downloading;
        chunk.StartedAt = DateTime.UtcNow;
        
        _logger.LogInformation("Starting chunk {Index} download: {StartByte}-{EndByte} (Range)", chunk.ChunkIndex, chunk.StartByte, chunk.EndByte);
        
        var request = new HttpRequestMessage(HttpMethod.Get, task.Url);
        var rangeStart = chunk.StartByte + chunk.DownloadedBytes;
        request.Headers.Range = new RangeHeaderValue(rangeStart, chunk.EndByte);
        
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        _logger.LogInformation("Chunk {Index} response received: {StatusCode}, ContentLength={Length}", 
            chunk.ChunkIndex, response.StatusCode, response.Content.Headers.ContentLength);
        
        response.EnsureSuccessStatusCode();
        
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        // File lock - for parallel writes to same file
        var fileLock = _chunkFileLocks.GetOrAdd(task.Id, _ => new object());
        
        var buffer = new byte[65536]; // 64KB buffer
        long chunkTotalRead = chunk.DownloadedBytes;
        var stopwatch = Stopwatch.StartNew();
        var lastDbSave = stopwatch.Elapsed;
        
        var downloadLimiter = _downloadSpeedLimiters.TryGetValue(task.Id, out var dl) ? dl : null;
        
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await _globalSpeedLimiter.WaitAsync(cancellationToken);
            
            if (downloadLimiter != null)
            {
                await downloadLimiter.WaitAsync(cancellationToken);
            }
            
            lock (fileLock)
            {
                using var fileStream = new FileStream(task.FilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                fileStream.Seek(chunk.StartByte + chunkTotalRead, SeekOrigin.Begin);
                fileStream.Write(buffer, 0, bytesRead);
            }
            
            chunkTotalRead += bytesRead;
            chunk.DownloadedBytes = chunkTotalRead;
            
            // Task-level UI throttle: update once every 500ms for all chunks
            var nowTicks = Stopwatch.GetTimestamp();
            var lastTicks = _taskLastUiUpdateTicks.GetValueOrDefault(task.Id, 0);
            var elapsedMs = (nowTicks - lastTicks) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs >= 500)
            {
                _taskLastUiUpdateTicks[task.Id] = nowTicks;
                
                task.DownloadedBytes = (task.Chunks ?? new List<DownloadChunk>()).Sum(c => c.DownloadedBytes);
                task.Speed = task.DownloadedBytes / stopwatch.Elapsed.TotalSeconds;
                RecordSpeedHistory(task);
                
                if (task.Speed > 0)
                {
                    var remaining = task.FileSize - task.DownloadedBytes;
                    task.EstimatedCompletion = DateTime.UtcNow.AddSeconds(remaining / task.Speed);
                }
                
                _logger.LogDebug("Chunk {Index}: {Read}/{Total} bytes ({Speed}), Task: {TaskDownloaded}/{TaskSize}",
                    chunk.ChunkIndex, chunkTotalRead, chunk.EndByte - chunk.StartByte + 1, FileHelper.FormatBytes((long)task.Speed),
                    FileHelper.FormatBytes(task.DownloadedBytes), FileHelper.FormatBytes(task.FileSize));
                
                EmitProgress(task);
            }
            
            // Save chunk states to DB every 10 seconds
            if ((stopwatch.Elapsed - lastDbSave).TotalSeconds >= 10)
            {
                _ = SaveChunksAsync(task);
                lastDbSave = stopwatch.Elapsed;
            }
        }
        
        _logger.LogInformation("Chunk {Index} completed: {TotalBytes} bytes", chunk.ChunkIndex, chunkTotalRead);
        
        chunk.Status = ChunkStatus.Completed;
        chunk.CompletedAt = DateTime.UtcNow;
    }
    
    private async Task DownloadSingleAsync(DownloadTask task, HttpClient httpClient, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting single connection download: {FileName}, DownloadedBytes={Downloaded}, SupportsResume={Resume}",
            task.FileName, task.DownloadedBytes, task.SupportsResume);
        
        var request = new HttpRequestMessage(HttpMethod.Get, task.Url);
        
        if (task.DownloadedBytes > 0 && task.SupportsResume)
        {
            request.Headers.Range = new RangeHeaderValue(task.DownloadedBytes, null);
            _logger.LogInformation("Range header added: {Start}-*", task.DownloadedBytes);
        }
        
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        _logger.LogInformation("Response received: {StatusCode}, ContentLength={Length}", 
            response.StatusCode, response.Content.Headers.ContentLength);
        
        response.EnsureSuccessStatusCode();
        
        if (task.FileSize == 0)
        {
            task.FileSize = response.Content.Headers.ContentLength ?? 0;
        }
        
        var fileMode = task.DownloadedBytes > 0 ? FileMode.Append : FileMode.Create;
        
        var dir = Path.GetDirectoryName(task.FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(task.FilePath, fileMode, FileAccess.Write, FileShare.None);
        
        var buffer = new byte[65536]; // 64KB buffer
        long sessionBytesRead = 0;
        long totalRead = task.DownloadedBytes;
        var stopwatch = Stopwatch.StartNew();
        
        var downloadLimiter = _downloadSpeedLimiters.TryGetValue(task.Id, out var dl) ? dl : null;
        
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await _globalSpeedLimiter.WaitAsync(cancellationToken);
            
            if (downloadLimiter != null)
            {
                await downloadLimiter.WaitAsync(cancellationToken);
            }
            
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            sessionBytesRead += bytesRead;
            totalRead += bytesRead;
            
            // Task-level UI throttle: update every 500ms
            var nowTicks = Stopwatch.GetTimestamp();
            var lastTicks = _taskLastUiUpdateTicks.GetValueOrDefault(task.Id, 0);
            var elapsedMs = (nowTicks - lastTicks) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs >= 500)
            {
                _taskLastUiUpdateTicks[task.Id] = nowTicks;
                
                task.DownloadedBytes = totalRead;
                task.Speed = sessionBytesRead / stopwatch.Elapsed.TotalSeconds;
                RecordSpeedHistory(task);
                
                if (task.Speed > 0)
                {
                    var remaining = task.FileSize - task.DownloadedBytes;
                    task.EstimatedCompletion = DateTime.UtcNow.AddSeconds(remaining / task.Speed);
                }
                
                EmitProgress(task);
            }
        }
        
        task.DownloadedBytes = totalRead;
        EmitProgress(task);
    }
    
    #endregion
    
    #region Private - File Info & Hash
    
    private async Task<(string FileName, long FileSize, bool SupportsRange, string ContentType)> GetFileInfoAsync(string url)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "FluxGet/2.0");
            client.Timeout = TimeSpan.FromSeconds(30);
            
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);
            
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var acceptRanges = false;
            if (response.Headers.TryGetValues("Accept-Ranges", out var rangeValues))
            {
                acceptRanges = rangeValues.Any(v => v.Contains("bytes", StringComparison.OrdinalIgnoreCase));
            }
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            
            var fileName = "";
            if (response.Content.Headers.ContentDisposition != null)
            {
                fileName = response.Content.Headers.ContentDisposition.FileNameStar
                    ?? response.Content.Headers.ContentDisposition.FileName?.Trim('"')
                    ?? "";
            }
            
            if (string.IsNullOrEmpty(fileName))
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    fileName = Path.GetFileName(uri.LocalPath);
                }
            }
            
            if (string.IsNullOrEmpty(fileName))
                fileName = "download";
            
            // Protocol redirect check
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
            if (finalUrl != url)
            {
                _logger.LogInformation("URL redirect: {Original} -> {Final}", url, finalUrl);
            }
            
            return (fileName, contentLength, acceptRanges, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File info could not be retrieved: {Url}", url);
            
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var fileName = Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrEmpty(fileName)) fileName = "download";
                    return (fileName, 0, false, "");
                }
                return ("download", 0, false, "");
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get file info: {innerEx.Message}");
                return ("download", 0, false, "");
            }
        }
    }
    
    private async Task<bool> VerifyFileHashAsync(DownloadTask task)
    {
        try
        {
            using var stream = File.OpenRead(task.FilePath);
            HashAlgorithm? hashAlgorithm = task.HashAlgorithm?.ToUpper() switch
            {
                "SHA256" => SHA256.Create(),
                "SHA1" => SHA1.Create(),
                "MD5" => MD5.Create(),
                _ => null
            };
            
            using var _ = hashAlgorithm as IDisposable;
            
            if (hashAlgorithm == null) return true;
            
            var hashBytes = await hashAlgorithm.ComputeHashAsync(stream);
            var computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            
            var isValid = computedHash == task.ExpectedHash?.ToLower();
            
            _logger.LogInformation("Hash verification: {FileName}: {Result}", task.FileName, isValid ? "PASSED" : "FAILED");
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hash verification failed: {FileName}", task.FileName);
            return false;
        }
    }
    
    #endregion
    
    #region Private - Helpers
    
    private static int CalculateChunkCount(long fileSize)
    {
        return fileSize switch
        {
            < 1024 * 1024 => 1,
            < 10 * 1024 * 1024 => 2,
            < 50 * 1024 * 1024 => 4,
            < 200 * 1024 * 1024 => 6,
            < 1024 * 1024 * 1024 => 8,
            _ => 8
        };
    }
    
    private static TimeSpan CalculateRetryDelay(int retryCount)
    {
        var baseDelay = Math.Pow(2, retryCount);
        var jitter = Random.Shared.NextDouble() * 0.5;
        return TimeSpan.FromSeconds(baseDelay + jitter);
    }
    
    private void RecordSpeedHistory(DownloadTask task)
    {
        var history = _speedHistory.GetOrAdd(task.Id, _ => new List<double>());
        lock (history)
        {
            history.Add(task.Speed);
            if (history.Count > 60)
            {
                history.RemoveAt(0);
            }
            task.SpeedHistory = [.. history];
        }
    }
    
    private void EmitProgress(DownloadTask task)
    {
        _progressSubject.OnNext(new DownloadProgress
        {
            TaskId = task.Id,
            Progress = task.Progress,
            Speed = task.Speed,
            DownloadedBytes = task.DownloadedBytes,
            TotalBytes = task.FileSize,
            Elapsed = task.StartedAt.HasValue ? DateTime.UtcNow - task.StartedAt.Value : TimeSpan.Zero,
            EstimatedTimeRemaining = task.EstimatedCompletion.HasValue
                ? task.EstimatedCompletion.Value - DateTime.UtcNow
                : null
        });
    }
    
    private static string GetDefaultDownloadPath()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(path))
        {
            try { Directory.CreateDirectory(path); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to create download directory: {ex.Message}"); }
        }
        return path;
    }
    
    private static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;
        
        var dir = Path.GetDirectoryName(filePath) ?? "";
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        int counter = 1;
        
        while (File.Exists(filePath))
        {
            filePath = Path.Combine(dir, $"{name} ({counter}){ext}");
            counter++;
            if (counter > 100) break;
        }
        
        return filePath;
    }
    
    private static string ComputeHash(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    #endregion
    
    public void Dispose()
    {
        foreach (var client in _downloadHttpClients.Values)
        {
            client.Dispose();
        }
        
        foreach (var cts in _chunkCancellations.Values)
        {
            cts.Dispose();
        }
        
        foreach (var limiter in _downloadSpeedLimiters.Values)
        {
            if (limiter is SpeedLimiter sl) sl.Dispose();
        }
        
        _progressSubject?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
