using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Identinator.Annotations;
using Identinator.Util;
using Microsoft.Win32;
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.PnP;
using PropertyChanged;

namespace Identinator.ViewModels;

[AddINotifyPropertyChangedInterface]
internal static class FilterDriver
{
    private static readonly RegistryKey ServiceParameters;

    public static readonly Regex UsbHardwareIdRegex = new(@"(USB)\\(VID_([a-fA-F0-9]+)&PID_([a-fA-F0-9]+))");

    static FilterDriver()
    {
        ServiceParameters =
            Registry.LocalMachine.OpenSubKey(
                "SYSTEM\\CurrentControlSet\\Services\\nssidswap\\Parameters", true)!;

        if (ServiceParameters is null)
            throw new Exception("nssidswap registry path not found. Is the driver installed?");
    }

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
        typeof(string));

    public static bool IsEnabled
    {
        get => ServiceParameters.GetBool("Enabled", false);
        set => ServiceParameters.SetBool("Enabled", value);
    }

    public static bool IsVerboseOn
    {
        get => ServiceParameters.GetBool("VerboseOn", false);
        set => ServiceParameters.SetBool("VerboseOn", value);
    }

    public static RegistryKey AddOrUpdateRewriteEntry(string hardwareId, int portNumber = 0)
    {
        var match = UsbHardwareIdRegex.Match(hardwareId);

        if (!match.Success)
            throw new InvalidOperationException("Failed to parse hardware ID.");

        var enumerator = match.Groups[1].Value;
        var vidPid = match.Groups[2].Value;

        var enumeratorKey = ServiceParameters.CreateSubKey(enumerator);

        if (enumeratorKey is null)
            throw new InvalidOperationException("Failed to create sub-key for enumerator.");

        var vidPidKey = enumeratorKey.CreateSubKey(vidPid);

        if (vidPidKey is null)
            throw new InvalidOperationException("Failed to create sub-key for Vendor- and Product-IDs.");

        if (portNumber == 0)
            return vidPidKey;

        var portKey = vidPidKey.CreateSubKey(portNumber.ToString());

        if (portKey is null)
            throw new InvalidOperationException("Failed to create sub-key for port number.");

        return portKey;
    }

    public static RegistryKey? GetRewriteEntryFor(string hardwareId, int portNumber = 0)
    {
        var match = UsbHardwareIdRegex.Match(hardwareId);

        if (!match.Success)
            throw new InvalidOperationException("Failed to parse hardware ID.");

        var enumerator = match.Groups[1].Value;
        var vidPid = match.Groups[2].Value;

        var enumeratorKey = ServiceParameters.OpenSubKey(enumerator);

        var vidPidKey = enumeratorKey?.OpenSubKey(vidPid, true);

        if (vidPidKey is null)
            return null;

        var portKey = vidPidKey.OpenSubKey(portNumber.ToString(), true);

        return portKey ?? vidPidKey;
    }

    public static bool GetReplace(RegistryKey? parentKey)
    {
        return parentKey is not null && parentKey.GetBool("Replace", false);
    }

    public static void SetReplace(RegistryKey parentKey, bool value)
    {
        parentKey.SetBool("Replace", value);
    }

    public static IEnumerable<string> GetHardwareIds(RegistryKey? parentKey)
    {
        if (parentKey is null)
            return Enumerable.Empty<string>();

        return parentKey.GetStringArray("HardwareIDs") ?? Enumerable.Empty<string>();
    }

    public static void SetHardwareIds(RegistryKey parentKey, IEnumerable<string> value)
    {
        parentKey.SetStringArray("HardwareIDs", value);
    }

    public static IEnumerable<string> GetCompatibleIds(RegistryKey? parentKey)
    {
        if (parentKey is null)
            return Enumerable.Empty<string>();

        return parentKey.GetStringArray("CompatibleIDs") ?? Enumerable.Empty<string>();
    }

    public static void SetCompatibleIds(RegistryKey parentKey, IEnumerable<string> value)
    {
        parentKey.SetStringArray("CompatibleIDs", value);
    }

    public static string? GetDeviceId(RegistryKey? parentKey)
    {
        return parentKey?.GetString("DeviceID", string.Empty);
    }

    public static void SetDeviceId(RegistryKey parentKey, string value)
    {
        parentKey.SetString("DeviceID", value);
    }
}

internal class FilterDriverViewModel : INotifyPropertyChanged
{
    /// <summary>
    ///     Regex to strip out version value from INF file.
    /// </summary>
    private static readonly Regex DriverVersionRegex =
        new(@"^DriverVer *=.*,(\d*\.\d*\.\d*\.\d*)", RegexOptions.Multiline);

    /// <summary>
    ///     Gets or sets whether the global rewriting feature is enabled or disabled.
    /// </summary>
    public bool IsEnabled
    {
        get => FilterDriver.IsEnabled;
        set => FilterDriver.IsEnabled = value;
    }

    /// <summary>
    ///     Gets or sets whether verbose tracing is enabled or disabled.
    /// </summary>
    public bool IsVerboseOn
    {
        get => FilterDriver.IsVerboseOn;
        set => FilterDriver.IsVerboseOn = value;
    }

    /// <summary>
    ///     Detected version of the latest running filter driver, if any.
    /// </summary>
    public string CurrentDriverVersion
    {
        get
        {
            var value = GetLocalDriverVersion();

            return string.IsNullOrEmpty(value) ? "Not found" : value;
        }
    }

    public bool IsDriverInstalled => !string.IsNullOrEmpty(GetLocalDriverVersion());

    private static string? GetLocalDriverVersion()
    {
        return DriverStore.ExistingDrivers
            .Where(s => s.Contains("nssidswap", StringComparison.OrdinalIgnoreCase))
            .Select(d => GetInfDriverVersion(File.ReadAllText(d)))
            .MaxBy(k => k)?.ToString();
    }

    /// <summary>
    ///     Extracts the driver version from an INF file.
    /// </summary>
    /// <param name="infContent">The string content of the INF file.</param>
    /// <returns>The detected <see cref="Version" />.</returns>
    private static Version GetInfDriverVersion(string infContent)
    {
        var match = DriverVersionRegex.Match(infContent);

        return Version.Parse(match.Groups[1].Value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Refresh()
    {
        OnPropertyChanged(null);
    }
}