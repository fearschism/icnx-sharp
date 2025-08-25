using Avalonia.Controls;
using Avalonia.Interactivity;
using ICNX.App.ViewModels;

namespace ICNX.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
    
    private void OnSettingsSidebarButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is SettingsViewModel viewModel)
        {
            // Remove selected class from all sidebar buttons
            GeneralButton.Classes.Remove("selected");
            RetryButton.Classes.Remove("selected");
            AppearanceButton.Classes.Remove("selected");
            ImportExportButton.Classes.Remove("selected");
            
            // Add selected class to clicked button and update view model
            button.Classes.Add("selected");
            
            if (button.Tag is string tag && int.TryParse(tag, out int index))
            {
                viewModel.SelectedSettingsIndex = index;
            }
        }
    }
}