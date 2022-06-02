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

    private readonly FilterDriver _driver = new();

    public RewriteSettingsViewModel(UsbDevice device)
    {
        _device = device;

        if (!Equals(_device.Enumerator, "USB"))
            return;

        var key = _driver.GetRewriteEntryFor(_device.HardwareIds.First(),
            (int)_device.PortNumber);

        if (key is null)
            return;

        var hwIds = _driver.GetHardwareIds(key).ToList();

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

        OverrideCompatibleIds = _driver.GetCompatibleIds(key).Any();
    }

    public string? DeviceId
    {
        get => _driver.GetDeviceId(_driver.GetRewriteEntryFor(_device.HardwareIds.First(),
            (int)_device.PortNumber));
        set => _driver.SetDeviceId(
            _driver.AddOrUpdateRewriteEntry(_device.HardwareIds.First(), (int)_device.PortNumber), value);
    }

    public IEnumerable<string>? HardwareIds
    {
        get => _driver.GetHardwareIds(_driver.GetRewriteEntryFor(_device.HardwareIds.First(),
            (int)_device.PortNumber));
        set => _driver.SetHardwareIds(
            _driver.AddOrUpdateRewriteEntry(_device.HardwareIds.First(), (int)_device.PortNumber), value);
    }

    public IEnumerable<string>? CompatibleIds
    {
        get => _driver.GetCompatibleIds(_driver.GetRewriteEntryFor(_device.HardwareIds.First(),
            (int)_device.PortNumber));
        set => _driver.SetCompatibleIds(
            _driver.AddOrUpdateRewriteEntry(_device.HardwareIds.First(), (int)_device.PortNumber), value);
    }

    public bool Replace
    {
        get => _driver.GetReplace(_driver.GetRewriteEntryFor(_device.HardwareIds.First(),
            (int)_device.PortNumber));
        set => _driver.SetReplace(
            _driver.AddOrUpdateRewriteEntry(_device.HardwareIds.First(), (int)_device.PortNumber), value);
    }

    public string VendorId { get; set; } = new Random().Next(0x1337, ushort.MaxValue).ToString("X4");

    public string ProductId { get; set; } = new Random().Next(0xC001, ushort.MaxValue).ToString("X4");

    public bool OverrideCompatibleIds { get; set; } = true;

    public string? NewHardwareId { get; private set; }

    private void OnVendorIdChanged()
    {
        NewHardwareId = $@"USB\VID_{VendorId}&PID_{ProductId}";

        var key = _driver.AddOrUpdateRewriteEntry(_device.HardwareIds.First(), (int)_device.PortNumber);

        _driver.SetHardwareIds(key, new[] { NewHardwareId });
        _driver.SetDeviceId(key, NewHardwareId);
    }

    private void OnProductIdChanged()
    {
        OnVendorIdChanged();
    }

    private void OnOverrideCompatibleIdsChanged()
    {
        var key = _driver.AddOrUpdateRewriteEntry(_device.HardwareIds.First(), (int)_device.PortNumber);

        if (OverrideCompatibleIds)
            _driver.SetCompatibleIds(key, new[]
            {
                @"USB\MS_COMP_WINUSB",
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