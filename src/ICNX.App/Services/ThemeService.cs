using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Controls;

namespace ICNX.App.Services;

/// <summary>
/// Service for managing application themes and appearance
/// </summary>
public class ThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private readonly Application _application;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
        _application = Application.Current ?? throw new InvalidOperationException("Application.Current is null");
    }

    /// <summary>
    /// Apply the specified theme to the application
    /// </summary>
    public void ApplyTheme(ThemeMode themeMode)
    {
        try
        {
            _logger.LogInformation("Applying theme: {ThemeMode}", themeMode);

            // Method 1: Update Avalonia's built-in theme system first
            var themeVariant = themeMode switch
            {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                ThemeMode.HighContrast => ThemeVariant.Light, // Will be customized below
                ThemeMode.System => GetSystemTheme(),
                _ => ThemeVariant.Dark
            };

            _application.RequestedThemeVariant = themeVariant;

            // Method 2: Apply custom theme colors safely
            ApplyThemeColorsSafely(themeMode);

            _logger.LogInformation("Theme applied successfully: {ThemeMode}", themeMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme: {ThemeMode}", themeMode);
        }
    }

    private void ForceStyleSystemReload()
    {
        try
        {
            // Store current styles
            var currentStyles = new List<Avalonia.Styling.IStyle>();
            foreach (var style in _application.Styles)
            {
                currentStyles.Add(style);
            }

            // Method 1: Force theme variant toggle to trigger complete refresh
            var currentVariant = _application.RequestedThemeVariant;
            _application.RequestedThemeVariant = currentVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;

            // Method 2: Clear and re-add styles with delays
            Dispatcher.UIThread.Post(() =>
            {
                _application.Styles.Clear();

                Dispatcher.UIThread.Post(() =>
                {
                    // Re-add styles
                    foreach (var style in currentStyles)
                    {
                        _application.Styles.Add(style);
                    }

                    // Restore correct theme variant
                    _application.RequestedThemeVariant = currentVariant;

                    // Force immediate style application
                    if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        foreach (var window in desktop.Windows)
                        {
                            // Force complete layout recalculation
                            window.InvalidateMeasure();
                            window.InvalidateArrange();
                            window.UpdateLayout();
                        }
                    }
                }, DispatcherPriority.Send);

            }, DispatcherPriority.Send);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during style system reload");
        }
    }

    private void ForceAdvancedUIRefresh()
    {
        try
        {
            // Multiple refresh techniques with proper timing
            Dispatcher.UIThread.Post(() =>
            {
                if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    foreach (var window in desktop.Windows)
                    {
                        // Technique 1: Force complete layout recalculation
                        window.InvalidateMeasure();
                        window.InvalidateArrange();
                        window.InvalidateVisual();

                        // Technique 2: Force re-styling of all child elements
                        ForceRestylingRecursive(window);

                        // Technique 3: Update layout immediately
                        window.UpdateLayout();
                    }
                }

                // Technique 4: Secondary pass after layout
                Dispatcher.UIThread.Post(() =>
                {
                    if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        foreach (var window in desktop.Windows)
                        {
                            InvalidateVisualRecursive(window);
                            window.UpdateLayout();
                        }
                    }

                    // Technique 5: Final pass to catch any remaining elements
                    Dispatcher.UIThread.Post(() =>
                    {
                        ForceGlobalResourceRefresh();
                    }, DispatcherPriority.Background);

                }, DispatcherPriority.Render);
            }, DispatcherPriority.Send);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during advanced UI refresh");
        }
    }

    private void ForceRestylingRecursive(Avalonia.Visual visual)
    {
        try
        {
            if (visual is Avalonia.Controls.Control control)
            {
                // Force style re-evaluation by temporarily changing and restoring a property
                var currentMargin = control.Margin;
                control.Margin = new Avalonia.Thickness(currentMargin.Left + 0.001);
                control.Margin = currentMargin;
            }

            foreach (var child in visual.GetVisualChildren())
            {
                ForceRestylingRecursive(child);
            }
        }
        catch
        {
            // Ignore individual element errors
        }
    }

    private void ForceGlobalResourceRefresh()
    {
        try
        {
            // Force all windows to re-query resources
            if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    // Trigger resource system refresh by temporarily modifying window properties
                    var currentOpacity = window.Opacity;
                    window.Opacity = currentOpacity - 0.001;
                    window.Opacity = currentOpacity;

                    // Force final visual update
                    window.InvalidateVisual();
                    window.UpdateLayout();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during global resource refresh");
        }
    }

    /// <summary>
    /// Apply the specified accent color to the application
    /// </summary>
    public void ApplyAccentColor(string accentColor)
    {
        try
        {
            _logger.LogInformation("Applying accent color: {AccentColor}", accentColor);

            var color = Avalonia.Media.Color.Parse(accentColor);

            // Update accent color resources
            _application.Resources["AccentPrimary"] = color;
            _application.Resources["AccentPrimaryBrush"] = new Avalonia.Media.SolidColorBrush(color);

            // Also update related accent colors
            _application.Resources["BorderFocus"] = color;
            _application.Resources["BorderFocusBrush"] = new Avalonia.Media.SolidColorBrush(color);

            // Update progress gradient with new accent color
            var progressGradient = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative)
            };
            progressGradient.GradientStops.Add(new Avalonia.Media.GradientStop(color, 0));
            // Create a slightly darker version for the end color
            var endColor = Avalonia.Media.Color.FromRgb(
                (byte)Math.Max(0, color.R - 30),
                (byte)Math.Max(0, color.G - 30),
                (byte)Math.Max(0, color.B - 30)
            );
            progressGradient.GradientStops.Add(new Avalonia.Media.GradientStop(endColor, 1));
            _application.Resources["ProgressGradientBrush"] = progressGradient;

            // Force UI refresh
            InvalidateVisualTree();

            _logger.LogInformation("Accent color applied successfully: {AccentColor}", accentColor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply accent color: {AccentColor}", accentColor);
        }
    }

    private ThemeVariant GetSystemTheme()
    {
        // In a real implementation, you would check the system theme
        // For now, default to Dark
        return ThemeVariant.Dark;
    }

    private void ApplyThemeColorsSafely(ThemeMode themeMode)
    {
        var colors = GetThemeColors(themeMode);

        // Update resources directly without clearing them
        // This prevents temporary resource unavailability
        foreach (var (key, color) in colors)
        {
            _application.Resources[key] = color;
        }

        // Update all brush resources with new instances
        foreach (var (key, color) in colors)
        {
            var brushKey = key + "Brush";
            _application.Resources[brushKey] = new Avalonia.Media.SolidColorBrush(color);
        }

        // Update gradient brushes
        UpdateGradientBrushes(colors);

        // Force a simple visual refresh without complex invalidation
        Dispatcher.UIThread.Post(() =>
        {
            if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    window.InvalidateVisual();
                }
            }
        }, DispatcherPriority.Background);
    }

    private void ApplyThemeColors(ThemeMode themeMode)
    {
        var colors = GetThemeColors(themeMode);

        // Method 1: Clear existing resources completely
        ClearExistingThemeResources();

        // Method 2: Apply new resources with forced invalidation
        Dispatcher.UIThread.Post(() =>
        {
            // Update all color resources
            foreach (var (key, color) in colors)
            {
                _application.Resources[key] = color;
            }

            // Update all brush resources with new instances
            foreach (var (key, color) in colors)
            {
                var brushKey = key + "Brush";
                _application.Resources[brushKey] = new Avalonia.Media.SolidColorBrush(color);
            }

            // Method 3: Force resource system to recognize changes
            ForceResourceSystemRefresh();

            // Method 4: Update gradient brushes
            UpdateGradientBrushes(colors);

            // Method 5: Ensure all basic brush resources exist
            EnsureBasicBrushResources(colors);

        }, DispatcherPriority.Send);
    }

    private void ForceResourceSystemRefresh()
    {
        try
        {
            // Force Avalonia's resource system to refresh by triggering resource queries
            if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    // Force all styled elements to re-query their resources
                    ForceResourceRequery(window);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resource system refresh");
        }
    }

    private void ForceResourceRequery(Avalonia.Visual visual)
    {
        try
        {
            if (visual is Avalonia.StyledElement styledElement)
            {
                // Force visual refresh
                visual.InvalidateVisual();

                // Try to force style recalculation by touching style-related properties
                var currentClasses = styledElement.Classes.ToList();
                styledElement.Classes.Clear();
                foreach (var cls in currentClasses)
                {
                    styledElement.Classes.Add(cls);
                }
            }

            foreach (var child in visual.GetVisualChildren())
            {
                ForceResourceRequery(child);
            }
        }
        catch
        {
            // Ignore individual element errors
        }
    }

    private void ClearExistingThemeResources()
    {
        var themeResourceKeys = new[]
        {
            // Color keys
            "BackgroundPrimary", "BackgroundSecondary", "BackgroundTertiary",
            "SurfaceBase", "SurfaceElevated", "SurfaceGlass", "SurfaceCard",
            "TextPrimary", "TextSecondary", "TextMuted", "TextDisabled",
            "BorderDefault", "BorderElevated", "BorderFocus",
            "HoverOverlay", "PressedOverlay", "SelectedOverlay",
            "GradientStart", "GradientMiddle", "GradientEnd",
            
            // Brush keys
            "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush", "TextDisabledBrush",
            "SurfaceBaseBrush", "SurfaceElevatedBrush", "SurfaceGlassBrush", "SurfaceCardBrush",
            "BorderDefaultBrush", "BorderElevatedBrush", "BorderFocusBrush",
            "AccentPrimaryBrush", "HoverOverlayBrush", "PressedOverlayBrush", "SelectedOverlayBrush",
            
            // Gradient brushes
            "BackgroundGradientBrush", "HeaderGlassBrush", "ProgressGradientBrush"
        };

        foreach (var key in themeResourceKeys)
        {
            if (_application.Resources.ContainsKey(key))
            {
                _application.Resources.Remove(key);
            }
        }
    }

    private void EnsureBasicBrushResources(Dictionary<string, Avalonia.Media.Color> colors)
    {
        // Ensure all essential brush resources are available
        var essentialBrushes = new[]
        {
            "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush", "TextDisabledBrush",
            "SurfaceBaseBrush", "SurfaceElevatedBrush", "SurfaceGlassBrush", "SurfaceCardBrush",
            "BorderDefaultBrush", "BorderElevatedBrush", "BorderFocusBrush",
            "AccentPrimaryBrush", "HoverOverlayBrush", "PressedOverlayBrush", "SelectedOverlayBrush"
        };

        foreach (var brushKey in essentialBrushes)
        {
            var colorKey = brushKey.Replace("Brush", "");
            if (colors.ContainsKey(colorKey))
            {
                _application.Resources[brushKey] = new Avalonia.Media.SolidColorBrush(colors[colorKey]);
            }
        }
    }

    private void UpdateGradientBrushes(Dictionary<string, Avalonia.Media.Color> colors)
    {
        // Update background gradient
        if (colors.ContainsKey("BackgroundPrimary") && colors.ContainsKey("BackgroundSecondary"))
        {
            var backgroundGradient = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative)
            };
            backgroundGradient.GradientStops.Add(new Avalonia.Media.GradientStop(colors["BackgroundPrimary"], 0));
            backgroundGradient.GradientStops.Add(new Avalonia.Media.GradientStop(colors["BackgroundSecondary"], 1));
            _application.Resources["BackgroundGradientBrush"] = backgroundGradient;
        }

        // Update header glass brush
        if (colors.ContainsKey("SurfaceGlass"))
        {
            var headerGlass = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(0, 1, Avalonia.RelativeUnit.Relative)
            };
            var glassColor = colors["SurfaceGlass"];
            headerGlass.GradientStops.Add(new Avalonia.Media.GradientStop(Avalonia.Media.Color.FromArgb(128, glassColor.R, glassColor.G, glassColor.B), 0));
            headerGlass.GradientStops.Add(new Avalonia.Media.GradientStop(Avalonia.Media.Color.FromArgb(64, glassColor.R, glassColor.G, glassColor.B), 1));
            _application.Resources["HeaderGlassBrush"] = headerGlass;
        }

        // Update progress gradient
        if (colors.ContainsKey("AccentPrimary"))
        {
            var progressGradient = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative)
            };
            var accentColor = colors["AccentPrimary"];
            progressGradient.GradientStops.Add(new Avalonia.Media.GradientStop(accentColor, 0));
            // Create a slightly darker version for the end color
            var endColor = Avalonia.Media.Color.FromRgb(
                (byte)Math.Max(0, accentColor.R - 30),
                (byte)Math.Max(0, accentColor.G - 30),
                (byte)Math.Max(0, accentColor.B - 30)
            );
            progressGradient.GradientStops.Add(new Avalonia.Media.GradientStop(endColor, 1));
            _application.Resources["ProgressGradientBrush"] = progressGradient;
        }
    }

    private void InvalidateVisualTree()
    {
        // Ensure this runs on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            // Force all windows to refresh their visual tree
            if (_application.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    // Force immediate visual refresh
                    window.InvalidateVisual();
                    InvalidateVisualRecursive(window);
                }
            }

            // Also trigger a secondary update to ensure all resources are applied
            Dispatcher.UIThread.Post(() =>
            {
                if (_application.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    foreach (var window in desktop.Windows)
                    {
                        window.InvalidateVisual();
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }, Avalonia.Threading.DispatcherPriority.Send);
    }

    private void ForceCompleteUIRefresh()
    {
        try
        {
            // Method 1: Standard invalidation
            InvalidateVisualTree();

            // Method 2: Force theme variant change and back
            Dispatcher.UIThread.Post(() =>
            {
                var currentVariant = _application.RequestedThemeVariant;
                _application.RequestedThemeVariant = currentVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;

                Dispatcher.UIThread.Post(() =>
                {
                    _application.RequestedThemeVariant = currentVariant;

                    // Method 3: Force layout refresh on all windows
                    if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        foreach (var window in desktop.Windows)
                        {
                            window.InvalidateMeasure();
                            window.InvalidateArrange();
                            window.InvalidateVisual();
                        }
                    }
                }, DispatcherPriority.Background);
            }, DispatcherPriority.Send);

            // Method 4: If all else fails, prompt for restart
            Dispatcher.UIThread.Post(async () =>
            {
                await System.Threading.Tasks.Task.Delay(500); // Wait for other methods to take effect

                if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.Windows.Count > 0)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow != null)
                    {
                        // Check if the theme change was successful by examining current background
                        // If not, offer restart option
                        var shouldRestart = await ShouldRestartForTheme(mainWindow);
                        if (shouldRestart)
                        {
                            await PromptForRestart(mainWindow);
                        }
                    }
                }
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during UI refresh");
        }
    }

    private async System.Threading.Tasks.Task<bool> ShouldRestartForTheme(Window mainWindow)
    {
        try
        {
            // Simple heuristic: if we can't detect theme change after reasonable time, suggest restart
            await System.Threading.Tasks.Task.Delay(200);

            // For now, always return false to avoid restart prompts unless specifically needed
            // You can implement more sophisticated detection here
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async System.Threading.Tasks.Task PromptForRestart(Window parentWindow)
    {
        try
        {
            var result = await ShowRestartDialog(parentWindow);

            if (result)
            {
                RestartApplication();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing restart dialog");
        }
    }

    private async System.Threading.Tasks.Task<bool> ShowRestartDialog(Window parentWindow)
    {
        // Create a simple dialog window
        var dialog = new Window
        {
            Title = "Theme Change",
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.BorderOnly
        };

        bool result = false;
        var stackPanel = new StackPanel { Margin = new Avalonia.Thickness(20) };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "The theme change may require a restart to take full effect. Would you like to restart now?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 20)
        });

        var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

        var restartButton = new Button { Content = "Restart Now", Margin = new Avalonia.Thickness(0, 0, 10, 0) };
        var laterButton = new Button { Content = "Later" };

        restartButton.Click += (s, e) => { result = true; dialog.Close(); };
        laterButton.Click += (s, e) => { result = false; dialog.Close(); };

        buttonPanel.Children.Add(restartButton);
        buttonPanel.Children.Add(laterButton);
        stackPanel.Children.Add(buttonPanel);

        dialog.Content = stackPanel;

        await dialog.ShowDialog(parentWindow);
        return result;
    }

    private void RestartApplication()
    {
        try
        {
            var currentExecutable = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(currentExecutable))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = currentExecutable,
                    UseShellExecute = true
                });

                if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart application");
        }
    }

    private void InvalidateVisualRecursive(Avalonia.Visual visual)
    {
        visual.InvalidateVisual();

        foreach (var child in visual.GetVisualChildren())
        {
            InvalidateVisualRecursive(child);
        }
    }

    private Dictionary<string, Avalonia.Media.Color> GetThemeColors(ThemeMode themeMode)
    {
        return themeMode switch
        {
            ThemeMode.Light => new Dictionary<string, Avalonia.Media.Color>
            {
                // Background Colors
                ["BackgroundPrimary"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["BackgroundSecondary"] = Avalonia.Media.Color.Parse("#FFF8FAFC"),
                ["BackgroundTertiary"] = Avalonia.Media.Color.Parse("#FFF1F5F9"),

                // Surface Colors
                ["SurfaceBase"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["SurfaceElevated"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["SurfaceGlass"] = Avalonia.Media.Color.Parse("#F0FFFFFF"),
                ["SurfaceCard"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),

                // Text Colors
                ["TextPrimary"] = Avalonia.Media.Color.Parse("#FF0F172A"),
                ["TextSecondary"] = Avalonia.Media.Color.Parse("#FF334155"),
                ["TextMuted"] = Avalonia.Media.Color.Parse("#FF64748B"),
                ["TextDisabled"] = Avalonia.Media.Color.Parse("#FF94A3B8"),

                // Border Colors
                ["BorderDefault"] = Avalonia.Media.Color.Parse("#FFE2E8F0"),
                ["BorderElevated"] = Avalonia.Media.Color.Parse("#FFCBD5E1"),
                ["BorderFocus"] = Avalonia.Media.Color.Parse("#FF3B82F6"),

                // Interactive States
                ["HoverOverlay"] = Avalonia.Media.Color.Parse("#10000000"),
                ["PressedOverlay"] = Avalonia.Media.Color.Parse("#20000000"),
                ["SelectedOverlay"] = Avalonia.Media.Color.Parse("#20000000"),

                // Gradient Colors
                ["GradientStart"] = Avalonia.Media.Color.Parse("#FF6366F1"),
                ["GradientMiddle"] = Avalonia.Media.Color.Parse("#FF8B5CF6"),
                ["GradientEnd"] = Avalonia.Media.Color.Parse("#FFEC4899"),
            },

            ThemeMode.Dark => new Dictionary<string, Avalonia.Media.Color>
            {
                // Background Colors
                ["BackgroundPrimary"] = Avalonia.Media.Color.Parse("#FF1A1A2E"),
                ["BackgroundSecondary"] = Avalonia.Media.Color.Parse("#FF16213E"),
                ["BackgroundTertiary"] = Avalonia.Media.Color.Parse("#FF0F1419"),

                // Surface Colors
                ["SurfaceBase"] = Avalonia.Media.Color.Parse("#FF2A2D3A"),
                ["SurfaceElevated"] = Avalonia.Media.Color.Parse("#FF3A3D4A"),
                ["SurfaceGlass"] = Avalonia.Media.Color.Parse("#4D2A2D3A"),
                ["SurfaceCard"] = Avalonia.Media.Color.Parse("#8A2A2D3A"),

                // Text Colors
                ["TextPrimary"] = Avalonia.Media.Color.Parse("#FFF1F5F9"),
                ["TextSecondary"] = Avalonia.Media.Color.Parse("#FFCBD5E1"),
                ["TextMuted"] = Avalonia.Media.Color.Parse("#FF94A3B8"),
                ["TextDisabled"] = Avalonia.Media.Color.Parse("#FF64748B"),

                // Border Colors
                ["BorderDefault"] = Avalonia.Media.Color.Parse("#FF243041"),
                ["BorderElevated"] = Avalonia.Media.Color.Parse("#FF374151"),
                ["BorderFocus"] = Avalonia.Media.Color.Parse("#FF3B82F6"),

                // Interactive States
                ["HoverOverlay"] = Avalonia.Media.Color.Parse("#20FFFFFF"),
                ["PressedOverlay"] = Avalonia.Media.Color.Parse("#30FFFFFF"),
                ["SelectedOverlay"] = Avalonia.Media.Color.Parse("#40FFFFFF"),

                // Gradient Colors
                ["GradientStart"] = Avalonia.Media.Color.Parse("#FF6366F1"),
                ["GradientMiddle"] = Avalonia.Media.Color.Parse("#FF8B5CF6"),
                ["GradientEnd"] = Avalonia.Media.Color.Parse("#FFEC4899"),
            },

            ThemeMode.HighContrast => new Dictionary<string, Avalonia.Media.Color>
            {
                // Background Colors
                ["BackgroundPrimary"] = Avalonia.Media.Color.Parse("#FF000000"),
                ["BackgroundSecondary"] = Avalonia.Media.Color.Parse("#FF1C1C1C"),
                ["BackgroundTertiary"] = Avalonia.Media.Color.Parse("#FF2D2D30"),

                // Surface Colors
                ["SurfaceBase"] = Avalonia.Media.Color.Parse("#FF000000"),
                ["SurfaceElevated"] = Avalonia.Media.Color.Parse("#FF1C1C1C"),
                ["SurfaceGlass"] = Avalonia.Media.Color.Parse("#FF000000"),
                ["SurfaceCard"] = Avalonia.Media.Color.Parse("#FF1C1C1C"),

                // Text Colors
                ["TextPrimary"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["TextSecondary"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["TextMuted"] = Avalonia.Media.Color.Parse("#FFCCCCCC"),
                ["TextDisabled"] = Avalonia.Media.Color.Parse("#FF808080"),

                // Border Colors
                ["BorderDefault"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["BorderElevated"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["BorderFocus"] = Avalonia.Media.Color.Parse("#FFFFFF00"),

                // Interactive States
                ["HoverOverlay"] = Avalonia.Media.Color.Parse("#40FFFFFF"),
                ["PressedOverlay"] = Avalonia.Media.Color.Parse("#60FFFFFF"),
                ["SelectedOverlay"] = Avalonia.Media.Color.Parse("#80FFFFFF"),

                // Gradient Colors (simplified for high contrast)
                ["GradientStart"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["GradientMiddle"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
                ["GradientEnd"] = Avalonia.Media.Color.Parse("#FFFFFFFF"),
            },

            _ => GetThemeColors(ThemeMode.Dark) // Default to dark theme
        };
    }
}
