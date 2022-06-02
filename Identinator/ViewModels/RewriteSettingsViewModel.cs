using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nefarius.Drivers.Identinator;
using PropertyChanged;

namespace Identinator.ViewModels;

[AddINotifyPropertyChangedInterface]
internal class RewriteSettingsViewModel
{
    private readonly UsbDevice _device;

    private readonly FilterDriver _driver = new();

    public RewriteSettingsViewModel(UsbDevice device)
    {
        _device = device;

        if (!Equals(_device.Enumerator, "USB"))
            return;

        var entry = _driver.GetRewriteEntryFor(_device.PrimaryHardwareId,
            (int)_device.PortNumber);

        if (entry is null)
            return;

        var hwIds = entry.HardwareIds.ToList();

        if (!hwIds.Any())
            return;

        var regex = FilterDriver.UsbHardwareIdRegex;

        var match = regex.Match(hwIds.First());

        if (!match.Success)
            return;

        var vid = match.Groups[3].Value;

        if (ushort.TryParse(vid, NumberStyles.AllowHexSpecifier, null, out var vidValue))
            VendorId = vidValue.ToString("X4");

        var pid = match.Groups[4].Value;

        if (ushort.TryParse(pid, NumberStyles.AllowHexSpecifier, null, out var pidValue))
            ProductId = pidValue.ToString("X4");

        OverrideCompatibleIds = entry.CompatibleIds.Any();
    }

    public string? DeviceId
    {
        get => _driver.GetRewriteEntryFor(_device.PrimaryHardwareId, PortNumber)?.DeviceId;
        set => _driver.AddOrUpdateRewriteEntry(_device.PrimaryHardwareId, PortNumber).DeviceId = value;
    }

    public IEnumerable<string>? HardwareIds
    {
        get => _driver.GetRewriteEntryFor(_device.PrimaryHardwareId, PortNumber)?.HardwareIds;
        set => _driver.AddOrUpdateRewriteEntry(_device.PrimaryHardwareId, PortNumber).HardwareIds =
            value;
    }

    public IEnumerable<string>? CompatibleIds
    {
        get => _driver.GetRewriteEntryFor(_device.PrimaryHardwareId, PortNumber)?.CompatibleIds;
        set => _driver.AddOrUpdateRewriteEntry(_device.PrimaryHardwareId, PortNumber).CompatibleIds =
            value;
    }

    public bool Replace
    {
        get => _driver.GetRewriteEntryFor(_device.PrimaryHardwareId, PortNumber)?.IsReplacingEnabled ??
               false;
        set => _driver.AddOrUpdateRewriteEntry(_device.PrimaryHardwareId, PortNumber)
            .IsReplacingEnabled = value;
    }

    public string VendorId { get; set; } = new Random().Next(0x1337, ushort.MaxValue).ToString("X4");

    public string ProductId { get; set; } = new Random().Next(0xC001, ushort.MaxValue).ToString("X4");

    public bool OverrideCompatibleIds { get; set; } = true;

    public string? NewHardwareId { get; private set; }

    private int PortNumber => _device.HasCompositeParent
        ? 0
        : (int)_device.PortNumber;

    private void OnVendorIdChanged()
    {
        NewHardwareId = $@"USB\VID_{VendorId}&PID_{ProductId}";

        var entry = _driver.AddOrUpdateRewriteEntry(_device.PrimaryHardwareId, PortNumber);

        entry.HardwareIds = new[] { NewHardwareId };
        entry.DeviceId = NewHardwareId;
    }

    private void OnProductIdChanged()
    {
        OnVendorIdChanged();
    }

    private void OnOverrideCompatibleIdsChanged()
    {
        var entry = _driver.AddOrUpdateRewriteEntry(_device.PrimaryHardwareId, PortNumber);

        if (OverrideCompatibleIds)
            entry.CompatibleIds = new[]
            {
                @"USB\MS_COMP_WINUSB",
                @"USB\Class_FF&SubClass_5D&Prot_01",
                @"USB\Class_FF&SubClass_5D",
                @"USB\Class_FF"
            };
        else
            entry.CompatibleIds = null;
    }

    private void OnReplaceChanged()
    {
        if (!Replace) return;

        OnProductIdChanged();
        OnOverrideCompatibleIdsChanged();
    }
}