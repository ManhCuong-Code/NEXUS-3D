using System.Windows;

namespace NEXUS3D.App.Views;

public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusTextBlock.Text = message;
        });
    }
}

