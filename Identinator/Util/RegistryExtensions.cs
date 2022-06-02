using System.Collections.Generic;
using Microsoft.Win32;

namespace Identinator.Util;

internal static class RegistryExtensions
{
    public static void SetBool(this RegistryKey key, string valueName, bool value)
    {
        key.SetValue(valueName, value ? 1 : 0, RegistryValueKind.DWord);
    }

    public static bool GetBool(this RegistryKey key, string valueName, bool defaultValue)
    {
        return int.Parse(key.GetValue(valueName, defaultValue ? 1 : 0).ToString()) > 0;
    }

    public static void SetString(this RegistryKey key, string valueName, string value)
    {
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public static string? GetString(this RegistryKey key, string valueName, string defaultValue)
    {
        return key.GetValue(valueName, defaultValue) as string;
    }

    public static void SetStringArray(this RegistryKey key, string valueName, IEnumerable<string> value)
    {
        key.SetValue(valueName, value, RegistryValueKind.MultiString);
    }

    public static IEnumerable<string>? GetStringArray(this RegistryKey key, string valueName, IEnumerable<string>? defaultValue = default)
    {
        return key.GetValue(valueName, defaultValue) as IEnumerable<string>;
    }
}