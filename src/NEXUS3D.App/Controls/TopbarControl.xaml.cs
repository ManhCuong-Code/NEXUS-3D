using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NEXUS3D.App.Controls;

public partial class TopbarControl : UserControl
{
    public TopbarControl()
    {
        InitializeComponent();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                var win = Window.GetWindow(this);
                win?.DragMove();
            }
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win is not null) win.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        var win = Window.GetWindow(this);
        if (win is not null)
        {
            win.WindowState = win.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        win?.Close();
    }
}
