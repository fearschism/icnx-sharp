using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ICNX.App.Services;

/// <summary>
/// Service for handling cross-platform folder selection dialogs
/// </summary>
public class FolderPickerService
{
    /// <summary>
    /// Opens a folder picker dialog and returns the selected folder path
    /// </summary>
    /// <param name="title">Title for the dialog</param>
    /// <param name="suggestedStartLocation">Starting directory for the picker</param>
    /// <returns>Selected folder path or null if cancelled</returns>
    public static async Task<string?> SelectFolderAsync(string title = "Select Folder", string? suggestedStartLocation = null)
    {
        try
        {
            // Get the main window from the application lifetime
            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (mainWindow?.StorageProvider == null)
                return null;

            var options = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            // Set suggested start location if provided
            if (!string.IsNullOrEmpty(suggestedStartLocation))
            {
                try
                {
                    var startFolder = await mainWindow.StorageProvider.TryGetFolderFromPathAsync(suggestedStartLocation);
                    if (startFolder != null)
                    {
                        options.SuggestedStartLocation = startFolder;
                    }
                }
                catch
                {
                    // Ignore errors when setting start location
                }
            }

            var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(options);

            return result.Count > 0 ? result[0].Path.LocalPath : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Opens a folder picker specifically for download directories
    /// </summary>
    /// <param name="currentDirectory">Current download directory</param>
    /// <returns>Selected folder path or null if cancelled</returns>
    public static async Task<string?> SelectDownloadFolderAsync(string? currentDirectory = null)
    {
        var startLocation = currentDirectory ??
                           System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

        return await SelectFolderAsync("Select Download Directory", startLocation);
    }

    /// <summary>
    /// Checks if folder picker functionality is available
    /// </summary>
    /// <returns>True if folder picker can be used</returns>
    public static bool IsAvailable()
    {
        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        return mainWindow?.StorageProvider?.CanPickFolder == true;
    }
}
