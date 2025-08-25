using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ICNX.App.ViewModels;

namespace ICNX.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void OnSidebarButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is MainWindowViewModel viewModel)
        {
            // Remove selected class from all sidebar buttons
            DownloadButton.Classes.Remove("selected");
            HistoryButton.Classes.Remove("selected");
            SettingsButton.Classes.Remove("selected");
            
            // Add selected class to clicked button and update view model
            if (button.Tag?.ToString() == "0")
            {
                DownloadButton.Classes.Add("selected");
                viewModel.SelectedTabIndex = 0;
            }
            else if (button.Tag?.ToString() == "1")
            {
                HistoryButton.Classes.Add("selected");
                viewModel.SelectedTabIndex = 1;
            }
            else if (button.Tag?.ToString() == "2")
            {
                SettingsButton.Classes.Add("selected");
                viewModel.SelectedTabIndex = 2;
            }
        }
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        // Set initial selected state when DataContext is set
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Default to Quick Download view
            DownloadButton.Classes.Add("selected");
            viewModel.SelectedTabIndex = 0;
        }
    }
}