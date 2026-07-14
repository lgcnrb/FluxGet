using FluxGet.Core.Data;
using FluxGet.Core.Services;
using FluxGet.UI.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace FluxGet
{
    public partial class App : Application
    {
        private Window? _window;
        
        public static IServiceProvider Services { get; private set; } = null!;
        public static Window MainWindow { get; private set; } = null!;
        
        public App()
        {
            InitializeComponent();
            ConfigureDI();
        }
        
        private void ConfigureDI()
        {
            var services = new ServiceCollection();
            
            // Logging
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            
            // Database
            services.AddDbContext<AppDbContext>();
            
            // Services
            services.AddSingleton<ISpeedLimiter>(new SpeedLimiter(0));
            services.AddSingleton<SettingsService>();
            services.AddSingleton<IResumeService, ResumeService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<IQueueService, QueueService>();
            services.AddSingleton<IBrowserExtensionService, BrowserExtensionService>();
            services.AddSingleton<DownloadNotificationService>();
            services.AddSingleton<YouTubeService>();
            
            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<NewDownloadViewModel>();
            services.AddTransient<DownloadDetailViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<QueueViewModel>();
            
            Services = services.BuildServiceProvider();
        }
        
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();
            
            // Stop server when app closes
            _window.Closed += OnMainWindowClosed;
            
            // Initialize database - ensure schema matches model
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FluxGet",
                "downloads.db");
            var shmPath = dbPath + "-shm";
            var walPath = dbPath + "-wal";
            
            bool needsRecreate = false;
            if (!File.Exists(dbPath))
            {
                needsRecreate = true;
            }
            else
            {
                // Check schema validity
                try
                {
                    using (var scope = Services.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await dbContext.Database.EnsureCreatedAsync();
                        await dbContext.DownloadTasks.Select(d => d.Cookies).FirstOrDefaultAsync();
                    }
                }
                catch
                {
                    needsRecreate = true;
                }
            }
            
            if (needsRecreate)
            {
                try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }
                try { if (File.Exists(shmPath)) File.Delete(shmPath); } catch { }
                try { if (File.Exists(walPath)) File.Delete(walPath); } catch { }
                
                using (var scope = Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await dbContext.Database.EnsureCreatedAsync();
                }
            }
            
            // Load settings
            var settingsService = Services.GetRequiredService<SettingsService>();
            settingsService.Load();
            
            var settingsVm = Services.GetRequiredService<SettingsViewModel>();
            SettingsViewModel.Initialize(settingsService);
            settingsVm.ApplyLoadedSettings();
            
            // Load downloads from database
            var downloadService = Services.GetRequiredService<IDownloadService>();
            await downloadService.LoadAllAsync();
            
            // Notification service - show notification on download completed/error
            var notificationService = Services.GetRequiredService<DownloadNotificationService>();
            var downloadSvc = Services.GetRequiredService<IDownloadService>();
            downloadSvc.ProgressChanged.Subscribe(progress =>
            {
                var task = downloadSvc.GetById(progress.TaskId);
                if (task == null) return;
                
                if (task.Status == Core.Models.DownloadStatus.Completed)
                    notificationService.NotifyCompleted(task);
                else if (task.Status == Core.Models.DownloadStatus.Error)
                    notificationService.NotifyError(task);
            });
            
            // Wire browser extension events
            var browserExtension = Services.GetRequiredService<IBrowserExtensionService>();
            browserExtension.DownloadRequested += OnBrowserDownloadRequested;
            browserExtension.YouTubeDownloadRequested += OnYouTubeDownloadRequested;
            
            // Auto-start server (always)
            try
            {
                await browserExtension.StartAsync();
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Browser extension server auto-started");
            }
            catch (Exception ex)
            {
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogWarning(ex, "Failed to auto-start browser extension");
            }
        }
        
        private void OnBrowserDownloadRequested(object? sender, DownloadRequest e)
        {
            if (string.IsNullOrWhiteSpace(e.Url))
                return;
            
            _ = HandleBrowserDownloadAsync(e.Url);
        }
        
        private async Task HandleBrowserDownloadAsync(string url)
        {
            try
            {
                var downloadService = Services.GetRequiredService<IDownloadService>();
                var task = await downloadService.AddDownloadAsync(url, null, Core.Models.DownloadCategory.General);
                
                var queueService = Services.GetRequiredService<IQueueService>();
                await queueService.EnqueueAsync(task);
                
                // Update on UI thread
                var mainVm = Services.GetRequiredService<MainViewModel>();
                _window?.DispatcherQueue.TryEnqueue(() =>
                {
                    mainVm.Downloads.Insert(0, task);
                    mainVm.UpdateStats();
                    mainVm.ApplyFilter();
                });
                
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Browser download started: {Url}", url);
            }
            catch (Exception ex)
            {
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to start browser download");
            }
        }
        
        private void OnYouTubeDownloadRequested(object? sender, YouTubeDownloadRequest e)
        {
            if (string.IsNullOrWhiteSpace(e.Url))
                return;
            
            _ = HandleYouTubeDownloadAsync(e.Url, e.Resolution, e.Format);
        }
        
        private async Task HandleYouTubeDownloadAsync(string url, int resolution = 720, string format = "mp4")
        {
            try
            {
                var youTubeService = Services.GetRequiredService<YouTubeService>();
                var settingsService = Services.GetRequiredService<SettingsService>();
                var downloadService = Services.GetRequiredService<IDownloadService>();
                
                var downloadPath = settingsService.DefaultDownloadPath;
                if (string.IsNullOrEmpty(downloadPath))
                    downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                
                bool isAudio = format == "mp3";
                string ext = isAudio ? "mp3" : "mp4";
                var safeName = $"YouTube_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";
                var outputPath = Path.Combine(downloadPath, safeName);
                
                // Get video info - title and size
                YouTubeInfo? info = null;
                try
                {
                    info = await youTubeService.GetVideoInfoAsync(url);
                    if (info != null && !string.IsNullOrEmpty(info.Title))
                    {
                        var title = info.Title;
                        foreach (var c in Path.GetInvalidFileNameChars())
                            title = title.Replace(c, '_');
                        title = title.Substring(0, Math.Min(title.Length, 80));
                        safeName = $"{title}.{ext}";
                        outputPath = Path.Combine(downloadPath, safeName);
                    }
                }
                catch { }
                
                // Use size info if available
                long fileSize = info?.Formats?.FirstOrDefault()?.FileSize ?? 0;
                
                var task = await downloadService.AddDownloadAsync(url, outputPath, Core.Models.DownloadCategory.Video);
                task.FileName = safeName;
                task.Status = Core.Models.DownloadStatus.Downloading;
                task.FileSize = fileSize;
                task.UpdatedAt = DateTime.UtcNow;
                
                var mainVm = Services.GetRequiredService<MainViewModel>();
                _window?.DispatcherQueue.TryEnqueue(() =>
                {
                    mainVm.Downloads.Insert(0, task);
                    mainVm.UpdateStats();
                    mainVm.ApplyFilter();
                });
                
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogInformation("YouTube download started from browser: {Url} ({Resolution}p {Format})", url, resolution, format);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var progress = new Progress<double>(pct =>
                        {
                            if (task.FileSize > 0)
                            {
                                task.DownloadedBytes = (long)(task.FileSize * pct / 100);
                            }
                            else
                            {
                                // If no size info, convert percentage to bytes (estimated)
                                task.DownloadedBytes = (long)(pct * 1024 * 1024); // Assume 1MB = 1%
                            }
                            task.UpdatedAt = DateTime.UtcNow;
                        });
                        
                        if (isAudio)
                        {
                            await youTubeService.DownloadAudioAsync(url, outputPath, progress: progress);
                        }
                        else
                        {
                            await youTubeService.DownloadVideoByHeightAsync(url, outputPath, resolution, progress: progress);
                        }
                        
                        // Download completed - update file size
                        if (File.Exists(outputPath))
                        {
                            var fileInfo = new FileInfo(outputPath);
                            task.FileSize = fileInfo.Length;
                            task.DownloadedBytes = fileInfo.Length;
                        }
                        
                        task.Status = Core.Models.DownloadStatus.Completed;
                        task.UpdatedAt = DateTime.UtcNow;
                        task.FilePath = outputPath;
                        
                        _window?.DispatcherQueue.TryEnqueue(() =>
                        {
                            mainVm.UpdateStats();
                            mainVm.ApplyFilter();
                        });
                        
                        logger.LogInformation("YouTube download completed: {FileName}", safeName);
                    }
                    catch (Exception ex)
                    {
                        task.Status = Core.Models.DownloadStatus.Error;
                        task.ErrorCode = ex.Message;
                        task.UpdatedAt = DateTime.UtcNow;
                        
                        _window?.DispatcherQueue.TryEnqueue(() =>
                        {
                            mainVm.UpdateStats();
                            mainVm.ApplyFilter();
                        });
                        
                        logger.LogError(ex, "YouTube download failed: {Url}", url);
                    }
                });
            }
            catch (Exception ex)
            {
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to start YouTube download from browser");
            }
        }
        
        public static T GetService<T>() where T : class
        {
            return Services.GetRequiredService<T>();
        }
        
        private async void OnMainWindowClosed(object sender, WindowEventArgs args)
        {
            // Stop server when app closes
            try
            {
                var browserExtension = Services.GetRequiredService<IBrowserExtensionService>();
                await browserExtension.StopAsync();
                
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Browser extension server stopped");
            }
            catch { }
        }
    }
}
