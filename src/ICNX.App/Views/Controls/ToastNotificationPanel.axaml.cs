using Avalonia.Controls;
using ICNX.App.Services;

namespace ICNX.App.Views.Controls;

public partial class ToastNotificationPanel : UserControl
{
    public ToastNotificationService? NotificationService => DataContext as ToastNotificationService;
    
    public ToastNotificationPanel()
    {
        InitializeComponent();
    }
}