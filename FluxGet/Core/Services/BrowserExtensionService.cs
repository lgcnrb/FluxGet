using System.Net;
using System.Text.Json;
using FluxGet.Core.Models;
using FluxGet.Core.Security;
using Microsoft.Extensions.Logging;

namespace FluxGet.Core.Services;

public class BrowserExtensionService : IBrowserExtensionService, IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ILogger<BrowserExtensionService> _logger;
    private readonly IDownloadService _downloadService;
    private readonly string _apiToken;
    
    public int Port { get; } = 19874;
    
    public bool IsRunning { get; private set; }
    
    public event EventHandler<DownloadRequest>? DownloadRequested;
    public event EventHandler<YouTubeDownloadRequest>? YouTubeDownloadRequested;
    
    public BrowserExtensionService(ILogger<BrowserExtensionService> logger, IDownloadService downloadService)
    {
        _logger = logger;
        _downloadService = downloadService;
        _apiToken = ApiTokenValidator.GenerateToken();
    }
    
    public string GetApiToken() => _apiToken;
    
    public async Task StartAsync()
    {
        if (IsRunning)
            return;
        
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
            
            _cts = new CancellationTokenSource();
            IsRunning = true;
            
            _logger.LogInformation("Browser extension server started on port {Port}", Port);
            
            _ = AcceptConnectionsAsync(_cts.Token);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start browser extension server");
            throw;
        }
    }
    
    public async Task StopAsync()
    {
        if (!IsRunning)
            return;
        
        _cts?.Cancel();
        _listener?.Stop();
        IsRunning = false;
        
        _logger.LogInformation("Browser extension server stopped");
        
        await Task.CompletedTask;
    }
    
    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
        }
    }
    
    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            // CORS headers - tarayici eklentisi icin zorunlu
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            
            // Preflight request
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                return;
            }
            
            // POST istekleri icin token dogrulama
            if (context.Request.HttpMethod == "POST")
            {
                var authHeader = context.Request.Headers["Authorization"];
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ") || 
                    authHeader.Substring(7).Trim() != _apiToken)
                {
                    _logger.LogWarning("Gecersiz API token ile POST istegi reddedildi.");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(
                        System.Text.Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Unauthorized\"}"));
                    return;
                }
            }
            
            if (context.Request.Url?.AbsolutePath == "/api/download" && context.Request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(context.Request.InputStream);
                var body = await reader.ReadToEndAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var request = JsonSerializer.Deserialize<DownloadRequest>(body, options);
                
                _logger.LogInformation("Browser indirme istegi alindi: {Url}", request?.Url);
                
                if (request != null && !string.IsNullOrWhiteSpace(request.Url))
                {
                    // Blob URL kontrolu - bunlar disaridan indirilemez
                    if (request.Url.StartsWith("blob:"))
                    {
                        _logger.LogWarning("Blob URL alindi, atlanıyor: {Url}", request.Url);
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        await context.Response.OutputStream.WriteAsync(
                            System.Text.Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"blob URLs cannot be downloaded externally. Use the YouTube page in the app instead.\"}"));
                        return;
                    }
                    
                    // YouTube URL kontrolu
                    if (IsYouTubeUrl(request.Url))
                    {
                        _logger.LogInformation("YouTube URL algilandi: {Url}", request.Url);
                        YouTubeDownloadRequested?.Invoke(this, new YouTubeDownloadRequest
                        {
                            Url = request.Url,
                            Resolution = request.Resolution ?? 720,
                            Format = request.Format ?? "mp4"
                        });
                        
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        await context.Response.OutputStream.WriteAsync(
                            System.Text.Encoding.UTF8.GetBytes("{\"success\":true,\"type\":\"youtube\"}"));
                        return;
                    }
                    
                    DownloadRequested?.Invoke(this, request);
                    
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(
                        System.Text.Encoding.UTF8.GetBytes("{\"success\":true}"));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(
                        System.Text.Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Invalid request\"}"));
                }
            }
            else if (context.Request.Url?.AbsolutePath == "/api/health")
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(
                    System.Text.Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"version\":\"2.0\"}"));
            }
            else if (context.Request.Url?.AbsolutePath == "/api/token" && context.Request.HttpMethod == "GET")
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(
                    System.Text.Encoding.UTF8.GetBytes($"{{\"token\":\"{_apiToken}\"}}"));
            }
            else if (context.Request.Url?.AbsolutePath == "/api/downloads" && context.Request.HttpMethod == "GET")
            {
                var downloads = _downloadService.GetAllDownloads()
                    .Where(d => d.Status == DownloadStatus.Downloading || d.Status == DownloadStatus.Queued || d.Status == DownloadStatus.Paused)
                    .OrderByDescending(d => d.Status == DownloadStatus.Downloading)
                    .ThenBy(d => d.QueueOrder)
                    .Take(10)
                    .Select(d => new
                    {
                        id = d.Id,
                        fileName = d.FileName,
                        url = d.Url,
                        status = d.Status.ToString().ToLower(),
                        progress = Math.Round(d.Progress, 1),
                        downloadedBytes = d.DownloadedBytes,
                        fileSize = d.FileSize,
                        speed = d.Speed,
                        category = d.Category.ToString().ToLower(),
                        startedAt = d.StartedAt?.ToString("HH:mm:ss") ?? ""
                    })
                    .ToList();
                
                var result = JsonSerializer.Serialize(new { downloads }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(result));
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(
                    System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Not found\"}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            try
            {
                await context.Response.OutputStream.WriteAsync(
                    System.Text.Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}"));
            }
            catch { }
        }
        finally
        {
            context.Response.Close();
        }
    }
    
    public void Dispose()
    {
        _cts?.Dispose();
        _listener?.Stop();
        _listener?.Close();
    }
    
    private bool IsYouTubeUrl(string url)
    {
        return url.Contains("youtube.com/watch") ||
               url.Contains("youtu.be/") ||
               url.Contains("youtube.com/shorts/") ||
               url.Contains("youtube.com/embed/");
    }
}
