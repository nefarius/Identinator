using System;
using System.Windows;
using Bluegrams.Application;
using Identinator.Properties;

namespace Identinator;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

        PortableJsonSettingsProvider.SettingsFileName = "Identinator.config";
        PortableJsonSettingsProvider.ApplyProvider(Settings.Default);

        base.OnStartup(e);
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.ExceptionObject.ToString(), "Unhandled exception occurred", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}