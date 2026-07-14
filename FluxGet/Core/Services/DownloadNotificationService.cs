using FluxGet.Core.Models;
using Microsoft.Extensions.Logging;

namespace FluxGet.Core.Services;

public class DownloadNotificationService
{
    private readonly ILogger<DownloadNotificationService> _logger;
    
    public DownloadNotificationService(ILogger<DownloadNotificationService> logger)
    {
        _logger = logger;
    }
    
    public void NotifyCompleted(DownloadTask task)
    {
        try
        {
            var xml = $"""
                <toast>
                    <visual>
                        <binding template="ToastGeneric">
                            <text>Download Completed</text>
                            <text>{EscapeXml(task.FileName)}</text>
                            <text>{FormatBytes(task.FileSize)} - {EscapeXml(task.FilePath)}</text>
                        </binding>
                    </visual>
                </toast>
                """;
            
            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("FluxGet").Show(toast);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not show notification: {FileName}", task.FileName);
        }
    }
    
    public void NotifyError(DownloadTask task)
    {
        try
        {
            var xml = $"""
                <toast>
                    <visual>
                        <binding template="ToastGeneric">
                            <text>Download Error</text>
                            <text>{EscapeXml(task.FileName)}</text>
                            <text>{EscapeXml(task.ErrorCode ?? "Unknown error")}</text>
                        </binding>
                    </visual>
                </toast>
                """;
            
            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("FluxGet").Show(toast);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not show notification: {FileName}", task.FileName);
        }
    }
    
    private static string FormatBytes(long bytes) => bytes switch
    {
        0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
    
    private static string EscapeXml(string input) => input
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");
}
