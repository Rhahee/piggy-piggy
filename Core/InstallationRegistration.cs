using Microsoft.Win32;
using System.Runtime.Versioning;

namespace MyPiggyBank.Core;

[SupportedOSPlatform("windows")]
public static class InstallationRegistration
{
    public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{7A751CF3-5E61-4FB3-BBDB-A09F77BF92DA}_is1";

    // Only an existing registration matching this exact EXE may be updated.
    // A supplied root permits isolated registry tests without changing the user install.
    public static bool Synchronize(string executable, Version version, RegistryKey? root = null)
    {
        try
        {
            using var key = (root ?? Registry.CurrentUser).OpenSubKey(KeyPath, writable: true);
            if (key?.GetValue("InstallLocation") is not string location || !Path.IsPathFullyQualified(location) || !Path.IsPathFullyQualified(executable) ||
                !Path.GetFullPath(Path.Combine(location, "Piggy - Piggy.exe")).Equals(Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))
                return false;
            key.SetValue("DisplayVersion", AppVersion.Format(version), RegistryValueKind.String);
            key.SetValue("MajorVersion", version.Major, RegistryValueKind.DWord);
            key.SetValue("MinorVersion", version.Minor, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            return false; // Metadata failure must not prevent app startup.
        }
    }
}
