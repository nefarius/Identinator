using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Identinator.ChildWindows;
using Identinator.Properties;
using Identinator.Util;
using Identinator.ViewModels;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.SimpleChildWindow;
using Nefarius.Drivers.Identinator;
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
#if !DEBUG
using Nefarius.Utilities.GitHubUpdater;
#endif
using Resourcer;

namespace Identinator;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    /// <summary>
    ///     Local to embedded files mapping for extraction.
    /// </summary>
    private static readonly Dictionary<string, Stream> EmbeddedFiles = new()
    {
        { @"drivers\nssidswap_x64\nssidswap.cat", Resource.AsStream(@"drivers\nssidswap_x64\nssidswap.cat") },
        { @"drivers\nssidswap_x64\nssidswap.inf", Resource.AsStream(@"drivers\nssidswap_x64\nssidswap.inf") },
        { @"drivers\nssidswap_x64\nssidswap.sys", Resource.AsStream(@"drivers\nssidswap_x64\nssidswap.sys") }
    };

    private readonly DeviceNotificationListener _deviceListener = new();
    private readonly UsbDevicesTreeViewModel _viewModel = new();

    private readonly object _refreshLock = new();

    public MainWindow()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

        // Speed up appearance of tool tips
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(100));
        ToolTipService.ShowDurationProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(int.MaxValue));
        ToolTipService.IsEnabledProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(false));

        InitializeComponent();
    }

    private void DeviceListenerOnDeviceChanged(DeviceEventArgs obj)
    {
        Dispatcher.Invoke(EnumerateAllDevices);
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionButton.Content = $"UI Version: {Assembly.GetEntryAssembly().GetName().Version}";

        // Windows 10 check
        if (Environment.OSVersion.Version.Major < 10)
        {
            await this.ShowMessageAsync("Unsupported operating system",
                @"This product is only supported on Windows 10 (build 1809) or newer. " +
                Environment.NewLine + Environment.NewLine +
                @"The tool will close now."
            );
            e.Handled = true;
            Application.Current.Shutdown(0);
            return;
        }

        // Architecture check
        if (!Environment.Is64BitProcess)
        {
            await this.ShowMessageAsync("Unsupported architecture",
                @"This product is only supported on Intel/AMD x64 (64-Bit) CPU architectures. " +
                Environment.NewLine + Environment.NewLine +
                @"The tool will close now."
            );
            e.Handled = true;
            Application.Current.Shutdown(0);
            return;
        }

        // Display Server warning to user
        if (OS.IsWindowsServer())
            await this.ShowMessageAsync("⚠️ Server OS detected ⚠️",
                @"It looks like your system is running a Windows Server Edition. " +
                @"Running this solution on Windows Server is untested and out of scope. " +
                @"You can still continue using this tool, but do so at your own risk. " +
                @"No support or warranty provided whatsoever. "
            );

        // First run dialog
        if (Settings.Default.IsFirstRun)
        {
            var firstRunWindow = new FirstRunChildWindow { IsModal = true };
            await this.ShowChildWindowAsync(firstRunWindow);
            // Could do something with firstRunWindow.ChildWindowResult
            Settings.Default.IsFirstRun = false;
        }

#if !DEBUG
        if (new UpdateChecker("nefarius", "Identinator").IsUpdateAvailable)
        {
            await this.ShowMessageAsync("Update available",
                "A newer version of the Identinator is available, I'll now take you to the download site!");
            Process.Start(new ProcessStartInfo("https://github.com/nefarius/Identinator/releases/latest")
                { UseShellExecute = true });
        }
#endif

        // Driver installer
        if (!_viewModel.FilterDriver.IsDriverInstalled)
        {
            var installer = new DriverInstallerChildWindow(this) { IsModal = true };
            await this.ShowChildWindowAsync(installer);
        }

        _deviceListener.DeviceArrived += DeviceListenerOnDeviceChanged;
        _deviceListener.DeviceRemoved += DeviceListenerOnDeviceChanged;

        _deviceListener.StartListen(FilterDriver.FilteredDeviceInterfaceId);
        _deviceListener.StartListen(DeviceInterfaceIds.HidDevice);

        EnumerateAllDevices();
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _deviceListener.StopListen();
        _deviceListener.Dispose();

        Settings.Default.Save();
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is UsbDevice { IsHub: false } child)
        {
            _viewModel.SelectedDevice = child;
            SelectedDeviceDetailsGrid.Visibility = Visibility.Visible;
            PlaceholderGrid.Visibility = Visibility.Collapsed;
            return;
        }

        SelectedDeviceDetailsGrid.Visibility = Visibility.Collapsed;
        PlaceholderGrid.Visibility = Visibility.Visible;
    }

    private void ApplyChanges_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDevice is null)
            return;

        var usbDevice = _viewModel.SelectedDevice.Device.ToUsbPnPDevice();

        usbDevice.CyclePort();

        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            Dispatcher.Invoke(EnumerateAllDevices);
        });
    }

    private async void InstallDriver_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;

        _deviceListener.StopListen(FilterDriver.FilteredDeviceInterfaceId);

        await InstallDriver();

        button.IsEnabled = true;

        _viewModel.FilterDriver.Refresh();

        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            Dispatcher.Invoke(EnumerateAllDevices);
        });

        _deviceListener.StartListen(FilterDriver.FilteredDeviceInterfaceId);
    }

    private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MiniDumper.Write("Identinator.dmp");
    }

    private async void UninstallDriver_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;

        var controller = await this.ShowProgressAsync("Removal in progress", "Removing driver packages");

        //
        // Clean out driver store
        // 
        await Task.Run(() =>
        {
            foreach (var package in DriverStore.ExistingDrivers
                         .Where(s => s.Contains("nssidswap", StringComparison.OrdinalIgnoreCase)))
                DriverStore.RemoveDriver(package);
        });

        await controller.CloseAsync();

        await this.ShowMessageAsync("Reboot required",
            @"The removal finished successfully, " +
            @"but a reboot is required for completion. Don't forget to do that ❤️");

        button.IsEnabled = true;

        _viewModel.FilterDriver.Refresh();
    }

    private void VersionButton_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/nefarius/Identinator/releases/latest")
            { UseShellExecute = true });
    }

    private void ApplyAll_OnClick(object sender, RoutedEventArgs e)
    {
        var devices = new List<UsbDevice>();

        foreach (var hostController in _viewModel.UsbHostControllers)
        foreach (var hub in hostController.UsbHubs)
            devices.AddRange(hub.RewriteEnabledChildDevices);

        foreach (var usbDevice in devices) usbDevice.Device.ToUsbPnPDevice().CyclePort();
    }
}