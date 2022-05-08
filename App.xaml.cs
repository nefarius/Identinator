using System.Windows;
using Bluegrams.Application;
using Identinator.Properties;

namespace Identinator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            PortableJsonSettingsProvider.SettingsFileName = "Identinator.config";
            PortableJsonSettingsProvider.ApplyProvider(Settings.Default);

            base.OnStartup(e);
        }
    }
}
