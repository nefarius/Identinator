﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using Identinator.ViewModels;
using MahApps.Metro.Controls.Dialogs;
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace Identinator;

public partial class MainWindow
{
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

    public async Task InstallDriver()
    {
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
    }

    private void EnumerateAllDevices()
    {
        var instance = 0;

        var hostControllers = new UsbHostControllerCollection();

        while (Devcon.FindByInterfaceGuid(DeviceInterfaceIds.UsbHostController, out var device, instance++))
        {
            var hostController = new UsbHostController(device);

            /* TODO: UDE devices seem to get enumerated twice for some reason?! So skip one... */
            if (hostControllers.Contains(hostController))
                continue;

            var hostControllerChildren = device.GetProperty<string[]>(DevicePropertyDevice.Children);

            if (hostControllerChildren is not null)
                foreach (var hubInstanceId in hostControllerChildren)
                {
                    var hubDevice = PnPDevice.GetDeviceByInstanceId(hubInstanceId);
                    var hub = new UsbHub(null, hubDevice);

                    hostController.UsbHubs.Add(hub);
                }

            hostControllers.Add(hostController);
        }

        if (!_viewModel.UsbHostControllers.Any())
        {
            _viewModel.UsbHostControllers = hostControllers;
            MainGrid.DataContext = _viewModel;
            FilterDriverGrid.DataContext = _viewModel.FilterDriver;
        }
        else
        {
            var lhs = GetAllChildDevicesFor(_viewModel.UsbHostControllers).ToList();
            var rhs = GetAllChildDevicesFor(hostControllers).ToList();

            var added = rhs.Except(lhs).Where(d => !d.HasCompositeParent).ToList();
            var removed = lhs.Except(rhs).ToList();

            var hubs = GetAllHubDevicesFor(_viewModel.UsbHostControllers).ToList();

            foreach (var device in added)
            {
                var nodes = hubs.First(h => Equals(h, device.ParentHub)).ChildNodes;

                nodes.Add(device);   
            }

            CollectionViewSource.GetDefaultView(_viewModel.UsbHostControllers).Refresh();
        }
    }

    private static IEnumerable<UsbDevice> GetAllChildDevicesFor(UsbHostControllerCollection hostControllers)
    {
        var devices = new List<UsbDevice>();

        foreach (var hostController in hostControllers)
            foreach (var hub in hostController.UsbHubs)
                devices.AddRange(GetAllChildDevices(hub));

        return devices;
    }

    private static IEnumerable<UsbHub> GetAllHubDevicesFor(UsbHostControllerCollection hostControllers)
    {
        var devices = new List<UsbHub>();

        foreach (var hostController in hostControllers)
        foreach (var hub in hostController.UsbHubs)
            devices.AddRange(GetAllHubDevices(hub));

        return devices.Distinct();
    }

    private static IEnumerable<UsbHub> GetAllHubDevices(UsbHub hub)
    {
        var devices = new List<UsbHub> { hub };

        if (!hub.ChildNodes.Any()) return devices;

        foreach (var childNode in hub.ChildNodes.OfType<UsbHub>())
        {
            devices.Add(childNode);
            var children = GetAllHubDevices(childNode);
            devices.AddRange(children);
        }

        return devices;
    }

    private static IEnumerable<UsbDevice> GetAllChildDevices(UsbDevice device)
    {
        var devices = new List<UsbDevice>();

        if (!device.ChildNodes.Any()) return Enumerable.Empty<UsbDevice>();

        foreach (var childNode in device.ChildNodes)
        {
            devices.Add(childNode);
            var children = GetAllChildDevices(childNode);
            devices.AddRange(children);
        }

        return devices;
    }

    private static IEnumerable<UsbDevice> GetRewriteEnabledChildDevices(UsbDevice device)
    {
        var devices = new List<UsbDevice>();

        if (!device.ChildNodes.Any()) return Enumerable.Empty<UsbDevice>();

        foreach (var childNode in device.ChildNodes.Where(d => d.RewriteSettings.Replace))
        {
            devices.Add(childNode);
            var children = GetRewriteEnabledChildDevices(childNode);
            devices.AddRange(children);
        }

        return devices;
    }
}