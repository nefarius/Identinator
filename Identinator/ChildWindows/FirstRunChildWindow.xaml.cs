using System.Windows;
using MahApps.Metro.SimpleChildWindow;

namespace Identinator.ChildWindows;

/// <summary>
///     Interaction logic for FirstRunChildWindow.xaml
/// </summary>
public partial class FirstRunChildWindow : ChildWindow
{
    public FirstRunChildWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close(CloseReason.Ok);
    }
}