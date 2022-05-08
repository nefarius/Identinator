using System.Windows;
using MahApps.Metro.SimpleChildWindow;

namespace Identinator.ChildWindows;

/// <summary>
///     Interaction logic for DriverInstallerChildWindow.xaml
/// </summary>
public partial class DriverInstallerChildWindow : ChildWindow
{
    private readonly MainWindow _parent;

    public DriverInstallerChildWindow(MainWindow parent)
    {
        _parent = parent;

        InitializeComponent();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async void Install_OnClick(object sender, RoutedEventArgs e)
    {
        await _parent.InstallDriver();
        Close();
    }
}