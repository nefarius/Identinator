using System.Runtime.InteropServices;

namespace Identinator.Util;

/// <summary>
///     https://stackoverflow.com/a/3138781/490629
/// </summary>
internal static class OS
{
    private const int OS_ANYSERVER = 29;

    public static bool IsWindowsServer()
    {
        return IsOS(OS_ANYSERVER);
    }

    [DllImport("shlwapi.dll", SetLastError = true, EntryPoint = "#437")]
    private static extern bool IsOS(int os);
}