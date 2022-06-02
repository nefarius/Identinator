using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Nefarius.Drivers.Identinator.Util;

namespace Nefarius.Drivers.Identinator;

public sealed class RewriteEntry : IDisposable
{
    private readonly RegistryKey _key;

    internal RewriteEntry(RegistryKey key)
    {
        _key = key;
    }

    public bool IsReplacingEnabled
    {
        get => _key.GetBool("Replace", false);
        set => _key.SetBool("Replace", value);
    }

    public IEnumerable<string> HardwareIds
    {
        get => _key.GetStringArray("HardwareIDs") ?? Enumerable.Empty<string>();
        set => _key.SetStringArray("HardwareIDs", value);
    }

    public IEnumerable<string> CompatibleIds
    {
        get => _key.GetStringArray("CompatibleIDs") ?? Enumerable.Empty<string>();
        set => _key.SetStringArray("CompatibleIDs", value);
    }

    public string DeviceId
    {
        get => _key.GetString("DeviceID", string.Empty) ?? string.Empty;
        set => _key.SetString("DeviceID", value);
    }

    public void Dispose()
    {
        _key.Dispose();
    }
}