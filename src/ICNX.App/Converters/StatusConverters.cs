using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ICNX.Core.Models;

namespace ICNX.App.Converters;

/// <summary>
/// Converts DownloadStatus to appropriate brush color
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Queued => new SolidColorBrush(Color.Parse("#FF64748B")), // Gray
                DownloadStatus.Started => new SolidColorBrush(Color.Parse("#FF3B82F6")), // Blue
                DownloadStatus.Downloading => new SolidColorBrush(Color.Parse("#FF3B82F6")), // Blue
                DownloadStatus.Paused => new SolidColorBrush(Color.Parse("#FFEAB308")), // Yellow
                DownloadStatus.Resumed => new SolidColorBrush(Color.Parse("#FF3B82F6")), // Blue
                DownloadStatus.Completed => new SolidColorBrush(Color.Parse("#FF22C55E")), // Green
                DownloadStatus.Failed => new SolidColorBrush(Color.Parse("#FFEF4444")), // Red
                DownloadStatus.Cancelled => new SolidColorBrush(Color.Parse("#FFEAB308")), // Yellow
                _ => new SolidColorBrush(Color.Parse("#FF64748B"))
            };
        }
        return new SolidColorBrush(Color.Parse("#FF64748B"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts progress percentage to width percentage for progress bars
/// </summary>
public class ProgressToWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            return $"{Math.Max(0, Math.Min(100, progress))}%";
        }
        return "0%";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string to visibility (visible if not empty)
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value?.ToString());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts status to boolean for completed items
/// </summary>
public class StatusToCompletedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DownloadStatus status && status == DownloadStatus.Completed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts status to boolean for failed items
/// </summary>
public class StatusToFailedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DownloadStatus status && status == DownloadStatus.Failed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts status to boolean for active items
/// </summary>
public class StatusToActiveConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DownloadStatus status && status is 
            DownloadStatus.Queued or 
            DownloadStatus.Started or 
            DownloadStatus.Downloading or 
            DownloadStatus.Paused or 
            DownloadStatus.Resumed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to visibility-like boolean for Avalonia
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Check if parameter is "Inverse" to invert the logic
            var inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
            
            if (inverse)
                boolValue = !boolValue;
                
            return boolValue; // Return bool directly for IsVisible binding
        }
        
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true;
    }
}