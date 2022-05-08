using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PropertyChanged;

namespace Identinator.ViewModels;

[AddINotifyPropertyChangedInterface]
internal class RewriteSettingsViewModel
{
    private readonly UsbDevice _device;

    public RewriteSettingsViewModel(UsbDevice device)
    {
        _device = device;

        if (!Equals(_device.Enumerator, "USB"))
            return;

        var key = FilterDriver.GetRewriteEntryFor(_device.HardwareIds.Last(),
            (int)_device.PortNumber);

        if (key is null)
            return;

        var hwIds = FilterDriver.GetHardwareIds(key).ToList();

        if (!hwIds.Any())
            return;

        var regex = FilterDriver.UsbHardwareIdRegex;

        var match = regex.Match(hwIds.Last());

        if (!match.Success)
            return;

        var vid = match.Groups[3].Value;

        if (ushort.TryParse(vid, NumberStyles.AllowHexSpecifier, null, out var vidValue))
            VendorId = vidValue.ToString("X4");

        var pid = match.Groups[4].Value;

        if (ushort.TryParse(pid, NumberStyles.AllowHexSpecifier, null, out var pidValue))
            ProductId = pidValue.ToString("X4");

        OverrideCompatibleIds = FilterDriver.GetCompatibleIds(key).Any();
    }

    public string? DeviceId
    {
        get => FilterDriver.GetDeviceId(FilterDriver.GetRewriteEntryFor(_device.HardwareIds.Last(),
            (int)_device.PortNumber));
        set => FilterDriver.SetDeviceId(
            FilterDriver.AddOrUpdateRewriteEntry(_device.HardwareIds.Last(), (int)_device.PortNumber), value);
    }

    public IEnumerable<string>? HardwareIds
    {
        get => FilterDriver.GetHardwareIds(FilterDriver.GetRewriteEntryFor(_device.HardwareIds.Last(),
            (int)_device.PortNumber));
        set => FilterDriver.SetHardwareIds(
            FilterDriver.AddOrUpdateRewriteEntry(_device.HardwareIds.Last(), (int)_device.PortNumber), value);
    }

    public IEnumerable<string>? CompatibleIds
    {
        get => FilterDriver.GetCompatibleIds(FilterDriver.GetRewriteEntryFor(_device.HardwareIds.Last(),
            (int)_device.PortNumber));
        set => FilterDriver.SetCompatibleIds(
            FilterDriver.AddOrUpdateRewriteEntry(_device.HardwareIds.Last(), (int)_device.PortNumber), value);
    }

    public bool Replace
    {
        get => FilterDriver.GetReplace(FilterDriver.GetRewriteEntryFor(_device.HardwareIds.Last(),
            (int)_device.PortNumber));
        set => FilterDriver.SetReplace(
            FilterDriver.AddOrUpdateRewriteEntry(_device.HardwareIds.Last(), (int)_device.PortNumber), value);
    }

    public string VendorId { get; set; } = new Random().Next(0x1337, ushort.MaxValue).ToString("X4");

    public string ProductId { get; set; } = new Random().Next(0xC001, ushort.MaxValue).ToString("X4");

    public bool OverrideCompatibleIds { get; set; } = true;

    public string? NewHardwareId { get; private set; }

    private void OnVendorIdChanged()
    {
        NewHardwareId = $@"USB\VID_{VendorId}&PID_{ProductId}";

        var key = FilterDriver.AddOrUpdateRewriteEntry(_device.HardwareIds.Last(), (int)_device.PortNumber);

        FilterDriver.SetHardwareIds(key, new[] { NewHardwareId });
        FilterDriver.SetDeviceId(key, NewHardwareId);
    }

    private void OnProductIdChanged()
    {
        OnVendorIdChanged();
    }

    private void OnOverrideCompatibleIdsChanged()
    {
        var key = FilterDriver.AddOrUpdateRewriteEntry(_device.HardwareIds.Last(), (int)_device.PortNumber);

        if (OverrideCompatibleIds)
            FilterDriver.SetCompatibleIds(key, new[]
            {
                @"USB\Class_FF&SubClass_5D&Prot_01",
                @"USB\Class_FF&SubClass_5D",
                @"USB\Class_FF"
            });
        else
            key.DeleteValue("CompatibleIDs", false);
    }

    private void OnReplaceChanged()
    {
        if (!Replace) return;

        OnProductIdChanged();
        OnOverrideCompatibleIdsChanged();
    }
}