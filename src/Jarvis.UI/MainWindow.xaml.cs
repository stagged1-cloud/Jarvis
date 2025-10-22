using Microsoft.UI.Xaml;
using System;

namespace Jarvis.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "Jarvis AI Assistant";
    }

    public void ShowPermissionBanner(string action)
    {
        PermissionText.Text = $"Jarvis wants to: {action}. Approve?";
        PermissionBanner.Visibility = Visibility.Visible;
    }

    public void HidePermissionBanner()
    {
        PermissionBanner.Visibility = Visibility.Collapsed;
    }

    public void UpdateStatus(string status)
    {
        StatusText.Text = status;
    }
}