using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Identinator.Annotations;
using Nefarius.Drivers.Identinator;
using Nefarius.Utilities.DeviceManagement.Drivers;

namespace Identinator.ViewModels;

internal class FilterDriverViewModel : INotifyPropertyChanged
{
    /// <summary>
    ///     Regex to strip out version value from INF file.
    /// </summary>
    private static readonly Regex DriverVersionRegex =
        new(@"^DriverVer *=.*,(\d*\.\d*\.\d*\.\d*)", RegexOptions.Multiline);

    private readonly FilterDriver _driver = new();

    /// <summary>
    ///     Gets or sets whether the global rewriting feature is enabled or disabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _driver.IsEnabled;
        set => _driver.IsEnabled = value;
    }

    /// <summary>
    ///     Gets or sets whether verbose tracing is enabled or disabled.
    /// </summary>
    public bool IsVerboseOn
    {
        get => _driver.IsVerboseOn;
        set => _driver.IsVerboseOn = value;
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

    /// <summary>
    ///     True if the driver has been detected, false otherwise.
    /// </summary>
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