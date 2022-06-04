using System;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Nefarius.Drivers.Identinator.Util;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace Nefarius.Drivers.Identinator;

/// <summary>
///     An instance of settings influencing nssidswap.sys behaviour.
/// </summary>
public class FilterDriver
{
    private readonly RegistryKey? _serviceParameters;

    public FilterDriver()
    {
        _serviceParameters =
            Registry.LocalMachine.OpenSubKey(
                "SYSTEM\\CurrentControlSet\\Services\\nssidswap\\Parameters", true)!;
    }

    /// <summary>
    ///     A regular expression for dissecting enumerator and VID/PID portion from a hardware ID.
    /// </summary>
    public static Regex UsbHardwareIdRegex => new(@"(USB)\\(VID_([a-fA-F0-9]+)&PID_([a-fA-F0-9]+).*)");

    /// <summary>
    ///     A device interface GUID that is exposed additionally on all filtered devices.
    /// </summary>
    public static Guid FilteredDeviceInterfaceId => Guid.Parse("{86431f5b-9f5a-48ce-8fbf-a537413ef358}");

    /// <summary>
    ///     Custom device property to back-up original Hardware IDs in.
    /// </summary>
    public static DevicePropertyKey OriginalHardwareIdsProperty => CustomDeviceProperty.CreateCustomDeviceProperty(
        Guid.Parse("{e98e7b19-6b9b-4d6e-aa39-01d9c270d09b}"), 2,
        typeof(string[]));

    /// <summary>
    ///     Custom device property to back-up original Compatible IDs in.
    /// </summary>
    public static DevicePropertyKey OriginalCompatibleIdsProperty => CustomDeviceProperty.CreateCustomDeviceProperty(
        Guid.Parse("{e98e7b19-6b9b-4d6e-aa39-01d9c270d09b}"), 3,
        typeof(string[]));

    /// <summary>
    ///     Custom device property to back-up original Device ID in.
    /// </summary>
    public static DevicePropertyKey OriginalDeviceIdProperty => CustomDeviceProperty.CreateCustomDeviceProperty(
        Guid.Parse("{e98e7b19-6b9b-4d6e-aa39-01d9c270d09b}"), 4,
        typeof(string[] /* TODO: bug in driver, wrong type, fix with new driver version */));

    public bool IsDriverServiceKeyPresent => _serviceParameters is not null;

    /// <summary>
    ///     True if the global rewrite functionality is enabled, false otherwise.
    /// </summary>
    public bool IsEnabled
    {
        get => _serviceParameters?.GetBool("Enabled", false) ?? false;
        set => _serviceParameters?.SetBool("Enabled", value);
    }

    /// <summary>
    ///     True if verbose WPP tracing messages are enabled, false otherwise.
    /// </summary>
    public bool IsVerboseOn
    {
        get => _serviceParameters?.GetBool("VerboseOn", false) ?? false;
        set => _serviceParameters?.SetBool("VerboseOn", value);
    }

    /// <summary>
    ///     Adds (or overwrites) a rewrite entry based on the supplied hardware ID and optional port number.
    /// </summary>
    /// <param name="hardwareId">The hardware ID.</param>
    /// <param name="portNumber">If provided, non-zero port number on the parent hub.</param>
    /// <returns>A new <see cref="RewriteEntry" /> on success.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public RewriteEntry AddOrUpdateRewriteEntry(string hardwareId, int portNumber = 0)
    {
        var match = UsbHardwareIdRegex.Match(hardwareId);

        if (!match.Success)
            throw new InvalidOperationException("Failed to parse hardware ID.");

        var enumerator = match.Groups[1].Value;
        var vidPid = match.Groups[2].Value;

        var enumeratorKey = _serviceParameters?.CreateSubKey(enumerator);

        if (enumeratorKey is null)
            throw new InvalidOperationException("Failed to create sub-key for enumerator.");

        var vidPidKey = enumeratorKey.CreateSubKey(vidPid);

        if (vidPidKey is null)
            throw new InvalidOperationException("Failed to create sub-key for Vendor- and Product-IDs.");

        if (portNumber == 0)
            return new RewriteEntry(vidPidKey);

        var portKey = vidPidKey.CreateSubKey(portNumber.ToString());

        if (portKey is null)
            throw new InvalidOperationException("Failed to create sub-key for port number.");

        return new RewriteEntry(portKey);
    }

    /// <summary>
    ///     Gets a rewrite entry - if it exists -  based on the supplied hardware ID and optional port number.
    /// </summary>
    /// <param name="hardwareId">The hardware ID.</param>
    /// <param name="portNumber">If provided, non-zero port number on the parent hub.</param>
    /// <returns>A new <see cref="RewriteEntry"/> on success, null otherwise.</returns>
    public RewriteEntry? GetRewriteEntryFor(string hardwareId, int portNumber = 0)
    {
        var match = UsbHardwareIdRegex.Match(hardwareId);

        if (!match.Success)
            return null;

        var enumerator = match.Groups[1].Value;
        var vidPid = match.Groups[2].Value;

        var enumeratorKey = _serviceParameters?.OpenSubKey(enumerator);

        var vidPidKey = enumeratorKey?.OpenSubKey(vidPid, true);

        if (vidPidKey is null)
            return null;

        var portKey = vidPidKey.OpenSubKey(portNumber.ToString(), true);

        return new RewriteEntry(portKey ?? vidPidKey);
    }
}