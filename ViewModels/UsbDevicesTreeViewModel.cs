using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nefarius.Utilities.DeviceManagement.PnP;
using PropertyChanged;

namespace Identinator.ViewModels;

/// <summary>
///     Represents a USB device.
/// </summary>
[AddINotifyPropertyChangedInterface]
internal class UsbDevice : IEquatable<UsbDevice>
{
    public UsbDevice(PnPDevice device)
    {
        Device = device;

        EnumerateChildren(this);

        RewriteSettings = new RewriteSettingsViewModel(this);
    }

    /// <summary>
    ///     The Hardware IDs of this device.
    /// </summary>
    public List<string>? HardwareIds =>
        Device.GetProperty<string[]>(FilterDriver.OriginalHardwareIdsProperty)?.ToList() ??
        Device.GetProperty<string[]>(DevicePropertyDevice.HardwareIds)?.ToList();

    /// <summary>
    ///     The Compatible IDs of this device.
    /// </summary>
    public List<string>? CompatibleIds =>
        Device.GetProperty<string[]>(FilterDriver.OriginalCompatibleIdsProperty)?.ToList() ??
        Device.GetProperty<string[]>(DevicePropertyDevice.CompatibleIds)?.ToList();

    public string Class => Device.GetProperty<string>(DevicePropertyDevice.Class) ?? "<None>";

    public string Enumerator => Device.GetProperty<string>(DevicePropertyDevice.EnumeratorName);

    public uint PortNumber => Device.GetProperty<uint>(DevicePropertyDevice.Address);

    public bool IsHub { get; protected set; }

    public bool IsCompositeDevice => "usbccgp".Equals(Device.GetProperty<string>(DevicePropertyDevice.EnumeratorName),
        StringComparison.OrdinalIgnoreCase);

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

            return !origIds.SequenceEqual(curIds);
        }
    }

    public UsbDeviceCollection ChildNodes { get; set; } = new();

    /// <summary>
    ///     The friendly name or description of the device.
    /// </summary>
    public string Name
    {
        get
        {
            /* not all devices have this property set, so attempt to read */
            var name = Device.GetProperty<string>(DevicePropertyDevice.FriendlyName);

            return name ?? Device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
            /* and fall back on description (always populated) */
        }
    }

    public RewriteSettingsViewModel RewriteSettings { get; }

    /// <summary>
    ///     The underlying <see cref="PnPDevice" />.
    /// </summary>
    public PnPDevice Device { get; }

    public bool Equals(UsbDevice? other)
    {
        return Equals(other?.Device, Device);
    }

    public override string ToString()
    {
        return Name;
    }

    private void EnumerateChildren(UsbDevice device)
    {
        var childrenInstances = device.Device.GetProperty<string[]>(DevicePropertyDevice.Children);

        if (childrenInstances is null)
            return;

        foreach (var childId in childrenInstances)
        {
            var childDevice = PnPDevice.GetDeviceByInstanceId(childId);

            var usbDevice = new UsbDevice(childDevice);

            EnumerateChildren(usbDevice);

            ChildNodes.Add(usbDevice);
        }
    }
}

[AddINotifyPropertyChangedInterface]
internal class UsbDeviceCollection : ObservableCollection<UsbDevice>
{
}

[AddINotifyPropertyChangedInterface]
internal class UsbHub : UsbDevice, IEquatable<UsbHub>
{
    public UsbHub(PnPDevice device) : base(device)
    {
        IsHub = true;
    }

    public bool Equals(UsbHub? other)
    {
        return Equals(other?.Device, Device);
    }

    public override string ToString()
    {
        return Device.GetProperty<string>(DevicePropertyDevice.FriendlyName) ??
               Device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
    }
}

[AddINotifyPropertyChangedInterface]
internal class UsbHubCollection : ObservableCollection<UsbHub>
{
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

    public bool Equals(UsbHostController? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Device.Equals(other.Device);
    }

    public override string ToString()
    {
        return Device.GetProperty<string>(DevicePropertyDevice.FriendlyName) ??
               Device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((UsbHostController)obj);
    }

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
}

[AddINotifyPropertyChangedInterface]
internal class UsbDevicesTreeViewModel
{
    public UsbHostControllerCollection UsbHostControllers { get; set; } = new();

    public UsbDevice? SelectedDevice { get; set; }

    public FilterDriverViewModel FilterDriver { get; set; } = new();
}