using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Nefarius.Drivers.Identinator.Util;

namespace Nefarius.Drivers.Identinator;

/// <summary>
///     Describes a rewrite entry that can exist for a USB device.
/// </summary>
public sealed class RewriteEntry : IDisposable
{
    private readonly RegistryKey _key;

    internal RewriteEntry(RegistryKey key)
    {
        _key = key;
    }

    /// <summary>
    ///     True if the configuration for this device is active and will be applied on power-up, false when disabled.
    /// </summary>
    public bool IsReplacingEnabled
    {
        get => _key.GetBool("Replace", false);
        set => _key.SetBool("Replace", value);
    }

    /// <summary>
    ///     One or more hardware IDs that will overwrite the existing ones.
    /// </summary>
    public IEnumerable<string> HardwareIds
    {
        get => _key.GetStringArray("HardwareIDs") ?? Enumerable.Empty<string>();
        set => _key.SetStringArray("HardwareIDs", value);
    }

    /// <summary>
    ///     One or more compatible IDs that will overwrite the existing ones.
    /// </summary>
    public IEnumerable<string> CompatibleIds
    {
        get => _key.GetStringArray("CompatibleIDs") ?? Enumerable.Empty<string>();
        set => _key.SetStringArray("CompatibleIDs", value);
    }

    /// <summary>
    ///     The device ID that will overwrite the existing one.
    /// </summary>
    public string DeviceId
    {
        get => _key.GetString("DeviceID", string.Empty) ?? string.Empty;
        set => _key.SetString("DeviceID", value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _key.Dispose();
    }
}