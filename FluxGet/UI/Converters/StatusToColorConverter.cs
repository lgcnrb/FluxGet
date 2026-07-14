using FluxGet.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FluxGet.UI.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // String input (from StatusText property)
        if (value is string status)
        {
            return status switch
            {
                "Completed" => new SolidColorBrush(ColorHelper.FromArgb(255, 76, 199, 100)),
                "Downloading" => new SolidColorBrush(ColorHelper.FromArgb(255, 96, 205, 255)),
                "Paused" => new SolidColorBrush(ColorHelper.FromArgb(255, 255, 179, 71)),
                "Error" => new SolidColorBrush(ColorHelper.FromArgb(255, 255, 100, 100)),
                "Pending" => new SolidColorBrush(ColorHelper.FromArgb(255, 180, 180, 180)),
                "Queued" => new SolidColorBrush(ColorHelper.FromArgb(255, 160, 160, 220)),
                "Cancelled" => new SolidColorBrush(ColorHelper.FromArgb(255, 140, 140, 140)),
                _ => new SolidColorBrush(ColorHelper.FromArgb(255, 140, 140, 140))
            };
        }
        
        // Enum input (from DownloadStatus property)
        if (value is DownloadStatus downloadStatus)
        {
            return downloadStatus switch
            {
                DownloadStatus.Pending => new SolidColorBrush(ColorHelper.FromArgb(255, 180, 180, 180)),
                DownloadStatus.Downloading => new SolidColorBrush(ColorHelper.FromArgb(255, 96, 205, 255)),
                DownloadStatus.Paused => new SolidColorBrush(ColorHelper.FromArgb(255, 255, 179, 71)),
                DownloadStatus.Completed => new SolidColorBrush(ColorHelper.FromArgb(255, 76, 199, 100)),
                DownloadStatus.Error => new SolidColorBrush(ColorHelper.FromArgb(255, 255, 100, 100)),
                DownloadStatus.Cancelled => new SolidColorBrush(ColorHelper.FromArgb(255, 140, 140, 140)),
                DownloadStatus.Queued => new SolidColorBrush(ColorHelper.FromArgb(255, 160, 160, 220)),
                _ => new SolidColorBrush(ColorHelper.FromArgb(255, 140, 140, 140))
            };
        }
        
        return new SolidColorBrush(Colors.Gray);
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
