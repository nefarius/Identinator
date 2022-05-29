﻿using System;
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
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
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

    private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MiniDumper.Write("Identinator.dmp");
    }

    private void EnumerateAllDevices()
    {
        var instance = 0;

        _viewModel.UsbHostControllers.Clear();

        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.UsbHostController, out var device, instance++))
        {
            var hostController = new UsbHostController(device);

            /* TODO: UDE devices seem to get enumerated twice for some reason?! So skip one... */
            if (_viewModel.UsbHostControllers.Contains(hostController))
                continue;

            var hostControllerChildren = device.GetProperty<string[]>(DevicePropertyDevice.Children);

            if (hostControllerChildren is not null)
                foreach (var hubInstanceId in hostControllerChildren)
                {
                    var hubDevice = PnPDevice.GetDeviceByInstanceId(hubInstanceId);
                    var hub = new UsbHub(hubDevice);

                    EnumerateUsbHub(hub);

                    hostController.UsbHubs.Add(hub);
                }

            _viewModel.UsbHostControllers.Add(hostController);
        }

        MainGrid.DataContext = _viewModel;
        FilterDriverGrid.DataContext = _viewModel.FilterDriver;
    }

    private void DeviceListenerOnDeviceArrived(DeviceEventArgs obj)
    {
        Dispatcher.Invoke(EnumerateAllDevices);
    }

    private static void EnumerateUsbHub(UsbHub hub)
    {
        var hubChildren = hub.Device.GetProperty<string[]>(DevicePropertyDevice.Children);

        if (hubChildren is null)
            return;

        foreach (var hubChildInstanceId in hubChildren)
        {
            var hubChildDevice = PnPDevice.GetDeviceByInstanceId(hubChildInstanceId);

            var service = hubChildDevice.GetProperty<string>(DevicePropertyDevice.Service);

            if (service is not null && service.StartsWith("USBHUB", StringComparison.OrdinalIgnoreCase))
            {
                var hubDevice = new UsbHub(hubChildDevice);
                EnumerateUsbHub(hubDevice);
                hub.ChildNodes.Add(hubDevice);
                continue;
            }

            var usbDevice = new UsbDevice(hubChildDevice);

            hub.ChildNodes.Add(usbDevice);
        }
    }

    /// <summary>
    ///     Creates and return a new temporary directory path.
    /// </summary>
    /// <returns>The newly created unique temporary path.</returns>
    private static string GetTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    /// <summary>
    ///     Creates a new temporary directory and extracts the driver files to it.
    /// </summary>
    private static Task<string> ExtractDriverFilesAsync()
    {
        return Task.Run(async () =>
        {
            var tempDir = GetTemporaryDirectory();

            foreach (var driverFile in EmbeddedFiles)
            {
                var targetPath = Path.Combine(tempDir, driverFile.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                await using var outStream = File.OpenWrite(targetPath);
                await driverFile.Value.CopyToAsync(outStream);
            }

            return tempDir;
        });
    }

    #region Event handlers

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

        if (!_viewModel.FilterDriver.IsDriverInstalled)
        {
            MainTabControl.SelectedIndex = 1;
            await this.ShowMessageAsync("Filter driver not found",
                @"It looks like the mandatory filter driver isn't installed. " +
                @"Please install it using the upcoming dialog. " +
                @"You may need to reboot your machine afterwards. "
            );
        }

        _deviceListener.DeviceArrived += DeviceListenerOnDeviceArrived;

        _deviceListener.StartListen(FilterDriver.FilteredDeviceInterfaceId);

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
        if (e.NewValue is not UsbDevice child)
        {
            SelectedDeviceDetailsGrid.Visibility = Visibility.Collapsed;
            PlaceholderGrid.Visibility = Visibility.Visible;
            return;
        }

        _viewModel.SelectedDevice = child;
        SelectedDeviceDetailsGrid.Visibility = Visibility.Visible;
        PlaceholderGrid.Visibility = Visibility.Collapsed;
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
        var button = sender as Button;
        button.IsEnabled = false;

        var controller = await this.ShowProgressAsync("Installation in progress", "Removing old driver packages");

        var infFile = EmbeddedFiles.First(pair => pair.Key.Contains(".inf", StringComparison.OrdinalIgnoreCase)).Key;

        //
        // Clean out driver store
        // 
        await Task.Run(() =>
        {
            foreach (var package in DriverStore.ExistingDrivers
                         .Where(s => s.Contains("nssidswap", StringComparison.OrdinalIgnoreCase)))
                DriverStore.RemoveDriver(package);
        });

        await Task.Delay(250);

        controller.SetMessage("Extracting driver files");

        var tempDirectory = await ExtractDriverFilesAsync();

        var infPath = Path.Combine(tempDirectory, infFile);

        controller.SetMessage(
            "Installing filter driver. \r\n\r\n" +
            "This can take up to a minute, please be patient. \r\n\r\n" +
            "Some of your USB devices may power-cycle during this process, this is expected. ");

        var installationResult = await Task.Run(() =>
        {
            var ret = Devcon.Install(infPath, out var rebootRequired);

            return new { WasSuccessful = ret, RebootRequired = rebootRequired };
        });

        controller.SetMessage("Cleaning up temporary directory");

        await Task.Delay(250);

        Directory.Delete(tempDirectory, true);

        await controller.CloseAsync();

        if (installationResult.WasSuccessful)
        {
            if (installationResult.RebootRequired)
                await this.ShowMessageAsync("Reboot required",
                    @"The installation finished successfully, " +
                    @"but a reboot is required for completion. Don't forget to do that ❤️");
            else
                await this.ShowMessageAsync("All done", "The installation finished successfully. Enjoy ❤️");
        }
        else
        {
            await this.ShowMessageAsync(
                "Unexpected error occurred",
                "Driver installation failed, please reboot and retry."
            );
        }

        button.IsEnabled = true;

        _viewModel.FilterDriver.Refresh();
    }

    private async void UninstallDriver_OnClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
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

    #endregion
}