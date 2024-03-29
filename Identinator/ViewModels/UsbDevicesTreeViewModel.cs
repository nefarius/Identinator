﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Identinator.Annotations;
using Nefarius.Drivers.Identinator;
using Nefarius.Utilities.DeviceManagement.PnP;
using PropertyChanged;

namespace Identinator.ViewModels;

/// <summary>
///     Represents a USB device.
/// </summary>
internal class UsbDevice : IEquatable<UsbDevice>, INotifyPropertyChanged
{
    private readonly ObservableCollection<UsbDevice> _childNodes;

    public UsbDevice(UsbHub? parentHub, PnPDevice device)
    {
        Device = device;
        ParentHub = parentHub;

        _childNodes = new ObservableCollection<UsbDevice>();
        ChildNodes = new ReadOnlyCollection<UsbDevice>(_childNodes);

        EnumerateChildren(parentHub, this);

        RewriteSettings = new RewriteSettingsViewModel(this);
    }

    /// <summary>
    ///     The direct parent <see cref="UsbHub" /> (if any) of this device.
    /// </summary>
    public UsbHub? ParentHub { get; }

    /// <summary>
    ///     The device unique ID.
    /// </summary>
    public string DeviceId =>
        Device.GetProperty<string>(FilterDriver.OriginalDeviceIdProperty) ??
        Device.DeviceId;

    /// <summary>
    ///     The Hardware IDs of this device.
    /// </summary>
    public List<string>? HardwareIds =>
        Device.GetProperty<string[]>(FilterDriver.OriginalHardwareIdsProperty)?.ToList() ??
        Device.GetProperty<string[]>(DevicePropertyDevice.HardwareIds)?.ToList();

    /// <summary>
    ///     The primary hardware ID.
    /// </summary>
    public string PrimaryHardwareId => HardwareIds?.First() ?? string.Empty;

    /// <summary>
    ///     The Compatible IDs of this device.
    /// </summary>
    public List<string>? CompatibleIds =>
        Device.GetProperty<string[]>(FilterDriver.OriginalCompatibleIdsProperty)?.ToList() ??
        Device.GetProperty<string[]>(DevicePropertyDevice.CompatibleIds)?.ToList();

    /// <summary>
    ///     The device class name.
    /// </summary>
    public string Class => Device.GetProperty<string>(DevicePropertyDevice.Class) ?? "<None>";

    /// <summary>
    ///     The enumerator name.
    /// </summary>
    public string Enumerator => Device.GetProperty<string>(DevicePropertyDevice.EnumeratorName);

    /// <summary>
    ///     The port number on the hub this device is connected to.
    /// </summary>
    public uint PortNumber => Device.GetProperty<uint>(DevicePropertyDevice.Address);

    /// <summary>
    ///     True if this device is a hub.
    /// </summary>
    public bool IsHub { get; protected set; }

    /// <summary>
    ///     Indicates whether this device is online with rewritten IDs.
    /// </summary>
    public bool IsRewritten
    {
        get
        {
            if (!RewriteSettings.Replace)
                return false;

            var origIds = Device.GetProperty<string[]>(FilterDriver.OriginalHardwareIdsProperty);
            var curIds = Device.GetProperty<string[]>(DevicePropertyDevice.HardwareIds);

            return !origIds?.SequenceEqual(curIds) ?? false;
        }
    }

    /// <summary>
    ///     True if this device is the last USB device in the chain of siblings.
    /// </summary>
    public bool IsLowestUsbChild => !ChildNodes.Any(usb => usb.Enumerator.Equals("USB"));

    /// <summary>
    ///     True if this device is a child of a composite device, false otherwise.
    /// </summary>
    public bool HasCompositeParent
    {
        get
        {
            var compositeDevice = PnPDevice
                .GetDeviceByInstanceId(Device.GetProperty<string>(DevicePropertyDevice.Parent),
                    DeviceLocationFlags.Phantom);

            // find root hub
            while (compositeDevice is not null)
            {
                var parentId = compositeDevice.GetProperty<string>(DevicePropertyDevice.Parent);

                if (parentId is null)
                    break;

                var service = compositeDevice.GetProperty<string>(DevicePropertyDevice.Service);

                // TODO: improve this method of detection
                if (
                    service is not null &&
                    (
                        service.Equals("usbccgp", StringComparison.OrdinalIgnoreCase) ||
                        service.StartsWith("xusb", StringComparison.OrdinalIgnoreCase) ||
                        service.StartsWith("dc1-controller", StringComparison.OrdinalIgnoreCase)
                    )
                )
                    return true;

                compositeDevice = PnPDevice.GetDeviceByInstanceId(parentId);
            }

            return false;
        }
    }

    /// <summary>
    ///     True if this device has arrived during runtime, false otherwise.
    /// </summary>
    public bool IsNewlyAttached { get; set; } = false;

    /// <summary>
    ///     True if device is online, false if phantom device.
    /// </summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    ///     List of children to this device, if any.
    /// </summary>
    public IReadOnlyCollection<UsbDevice> ChildNodes { get; }

    /// <summary>
    ///     The friendly name or description of the device.
    /// </summary>
    public string Name
    {
        get
        {
            /* not all devices have this property set, so attempt to read... */
            var name = Device.GetProperty<string>(DevicePropertyDevice.FriendlyName);

            /* ...and fall back on description (always populated) */
            return name ?? Device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
        }
    }

    /// <summary>
    ///     Rewrite settings of this device.
    /// </summary>
    public RewriteSettingsViewModel RewriteSettings { get; }

    /// <summary>
    ///     The underlying <see cref="PnPDevice" />.
    /// </summary>
    public PnPDevice Device { get; }

    /// <summary>
    ///     Gets all descending <see cref="UsbDevice" />s.
    /// </summary>
    public IEnumerable<UsbDevice> AllChildDevices
    {
        get
        {
            var devices = new List<UsbDevice>();

            if (!ChildNodes.Any()) return Enumerable.Empty<UsbDevice>();

            foreach (var childNode in ChildNodes)
            {
                devices.Add(childNode);
                var children = childNode.AllChildDevices;
                devices.AddRange(children);
            }

            return devices;
        }
    }

    /// <summary>
    ///     Gets all descending rewrite-enabled <see cref="UsbDevice" />s.
    /// </summary>
    public IEnumerable<UsbDevice> RewriteEnabledChildDevices
    {
        get
        {
            var devices = new List<UsbDevice>();

            if (!ChildNodes.Any()) return Enumerable.Empty<UsbDevice>();

            foreach (var childNode in ChildNodes.Where(d => d.RewriteSettings.Replace))
            {
                devices.Add(childNode);
                var children = childNode.RewriteEnabledChildDevices;
                devices.AddRange(children);
            }

            return devices;
        }
    }

    /// <inheritdoc />
    public bool Equals(UsbDevice? other)
    {
        return Equals(other?.DeviceId, DeviceId) && Equals(other?.PortNumber, PortNumber);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddChildDevice(UsbDevice device)
    {
        _childNodes.Add(device);
        OnPropertyChanged(nameof(ChildNodes));
    }

    public void RemoveChildDevice(UsbDevice device)
    {
        if (!_childNodes.Contains(device)) return;

        _childNodes.Remove(device);
        OnPropertyChanged(nameof(ChildNodes));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as UsbDevice);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Device.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    /// <summary>
    ///     Recursively enumerates the entire tree of child devices.
    /// </summary>
    /// <param name="parentHub">If non-null, the parent <see cref="UsbHub" />.</param>
    /// <param name="device">The <see cref="UsbDevice" /> to enumerate.</param>
    private void EnumerateChildren(UsbHub? parentHub, UsbDevice device)
    {
        var childrenInstances = device.Device.GetProperty<string[]>(DevicePropertyDevice.Children);

        if (childrenInstances is null)
            return;

        foreach (var childId in childrenInstances)
        {
            var childDevice = PnPDevice.GetDeviceByInstanceId(childId);

            var service = childDevice.GetProperty<string>(DevicePropertyDevice.Service);

            /* recursively enumerate a hub */
            if (service is not null && service.StartsWith("USBHUB", StringComparison.OrdinalIgnoreCase))
            {
                var usbHub = new UsbHub(parentHub, childDevice);
                EnumerateChildren(usbHub, usbHub);
                device._childNodes.Add(usbHub);
                OnPropertyChanged(nameof(ChildNodes));
            }
            else
            {
                var usbDevice = new UsbDevice(
                    /* a device without a parent hub is a hub itself */
                    parentHub ?? (UsbHub)device,
                    childDevice
                );

                /* avoid duplicates */
                if (!device.ChildNodes.Contains(usbDevice))
                {
                    device._childNodes.Add(usbDevice);
                    OnPropertyChanged(nameof(ChildNodes));
                }
            }
        }
    }

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal class UsbHub : UsbDevice, IEquatable<UsbHub>
{
    public UsbHub(UsbHub? parentHub, PnPDevice device) : base(parentHub, device)
    {
        IsHub = true;
    }

    /// <summary>
    ///     Gets all descending <see cref="UsbHub" />s.
    /// </summary>
    public IEnumerable<UsbHub> AllHubDevices
    {
        get
        {
            var devices = new List<UsbHub> { this };

            if (!ChildNodes.Any()) return devices;

            foreach (var childNode in ChildNodes.OfType<UsbHub>())
            {
                devices.Add(childNode);
                var children = childNode.AllHubDevices;
                devices.AddRange(children);
            }

            return devices;
        }
    }

    /// <inheritdoc />
    public bool Equals(UsbHub? other)
    {
        return Equals(other?.Device, Device);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as UsbHub);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Device.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Device.GetProperty<string>(DevicePropertyDevice.FriendlyName) ??
               Device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
    }
}

[AddINotifyPropertyChangedInterface]
internal class UsbHubCollection : ObservableCollection<UsbHub>
{
    protected override void InsertItem(int index, UsbHub item)
    {
        base.InsertItem(index, item);
        OnPropertyChanged(new PropertyChangedEventArgs(null));
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        OnPropertyChanged(new PropertyChangedEventArgs(null));
    }
}

[AddINotifyPropertyChangedInterface]
internal class UsbHostController : IEquatable<UsbHostController>
{
    public UsbHostController(PnPDevice device)
    {
        Device = device;
    }

    public UsbHubCollection UsbHubs { get; set; } = new();

    public PnPDevice Device { get; }

    /// <inheritdoc />
    public bool Equals(UsbHostController? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Device.Equals(other.Device);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Device.GetProperty<string>(DevicePropertyDevice.FriendlyName) ??
               Device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((UsbHostController)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Device.GetHashCode();
    }

    public static bool operator ==(UsbHostController? left, UsbHostController? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(UsbHostController? left, UsbHostController? right)
    {
        return !Equals(left, right);
    }
}

[AddINotifyPropertyChangedInterface]
internal class UsbHostControllerCollection : ObservableCollection<UsbHostController>
{
    /// <summary>
    ///     Gets all descending <see cref="UsbHub" />s.
    /// </summary>
    public IEnumerable<UsbHub> AllHubDevices
    {
        get
        {
            var devices = new List<UsbHub>();

            foreach (var hostController in this)
            foreach (var hub in hostController.UsbHubs)
                devices.AddRange(hub.AllHubDevices);

            return devices.Distinct();
        }
    }

    /// <summary>
    ///     Gets all descending <see cref="UsbDevice" />s.
    /// </summary>
    public IEnumerable<UsbDevice> AllChildDevices
    {
        get
        {
            var devices = new List<UsbDevice>();

            foreach (var hostController in this)
            foreach (var hub in hostController.UsbHubs)
                devices.AddRange(hub.AllChildDevices);

            return devices;
        }
    }

    protected override void InsertItem(int index, UsbHostController item)
    {
        base.InsertItem(index, item);
        OnPropertyChanged(new PropertyChangedEventArgs(null));
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        OnPropertyChanged(new PropertyChangedEventArgs(null));
    }
}

internal enum RefreshMechanism
{
    Full,
    Smart
}

internal class UsbDevicesTreeViewModel : INotifyPropertyChanged
{
    public UsbHostControllerCollection UsbHostControllers { get; set; } = new();

    public UsbDevice? SelectedDevice { get; set; }

    public FilterDriverViewModel FilterDriver { get; set; } = new();

    public RefreshMechanism Refresh { get; set; } = RefreshMechanism.Full;

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}