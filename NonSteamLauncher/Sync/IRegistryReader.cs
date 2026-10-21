using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NonSteamLauncher.Sync;

public interface IRegistryReader
{
    string[] GetSubKeyNames(string keyPath);
    string? GetValue(string keyPath, string valueName);
}

public class WindowsRegistryReader : IRegistryReader
{
    public string[] GetSubKeyNames(string keyPath)
    {
        if (!OperatingSystem.IsWindows())
            return Array.Empty<string>();

        try
        {
            var (hive, subKeyPath) = ParseKeyPath(keyPath);
            if (hive != null && !string.IsNullOrEmpty(subKeyPath))
            {
                using var key = hive.OpenSubKey(subKeyPath, writable: false);
                if (key != null)
                {
                    return key.GetSubKeyNames();
                }
                return Array.Empty<string>();
            }
        }
        catch
        {
            // Fall through to reg.exe fallback if needed
        }

        return GetSubKeyNamesViaRegExe(keyPath);
    }

    public string? GetValue(string keyPath, string valueName)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var (hive, subKeyPath) = ParseKeyPath(keyPath);
            if (hive != null && !string.IsNullOrEmpty(subKeyPath))
            {
                using var key = hive.OpenSubKey(subKeyPath, writable: false);
                if (key != null)
                {
                    object? val = key.GetValue(valueName);
                    return val?.ToString();
                }
                return null;
            }
        }
        catch
        {
            // Fall through to reg.exe fallback if needed
        }

        return GetValueViaRegExe(keyPath, valueName);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static (RegistryKey? Hive, string SubKeyPath) ParseKeyPath(string keyPath)
    {
        string trimmed = keyPath.Trim().TrimStart('[', '\\').TrimEnd(']', '\\');
        int firstSlash = trimmed.IndexOfAny(new[] { '\\', '/' });
        if (firstSlash <= 0)
            return (null, string.Empty);

        string rootName = trimmed[..firstSlash].ToUpperInvariant();
        string subKey = trimmed[(firstSlash + 1)..].Replace('/', '\\');

        RegistryKey? hive = rootName switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKU" or "HKEY_USERS" => Registry.Users,
            _ => null
        };

        return (hive, subKey);
    }

    public static string NormalizeFullKeyPrefix(string keyPath)
    {
        string trimmed = keyPath.Trim().TrimEnd('\\', '/');
        if (trimmed.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase))
            return @"HKEY_LOCAL_MACHINE\" + trimmed[5..];
        if (trimmed.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
            return @"HKEY_CURRENT_USER\" + trimmed[5..];
        if (trimmed.StartsWith(@"HKCR\", StringComparison.OrdinalIgnoreCase))
            return @"HKEY_CLASSES_ROOT\" + trimmed[5..];
        if (trimmed.StartsWith(@"HKU\", StringComparison.OrdinalIgnoreCase))
            return @"HKEY_USERS\" + trimmed[4..];
        return trimmed;
    }

    private static string[] GetSubKeyNamesViaRegExe(string keyPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"query \"{keyPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return Array.Empty<string>();

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            string expandedKeyPath = NormalizeFullKeyPrefix(keyPath);
            var list = new List<string>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                string? sub = null;
                if (trimmed.StartsWith(expandedKeyPath, StringComparison.OrdinalIgnoreCase) && trimmed.Length > expandedKeyPath.Length)
                {
                    sub = trimmed[expandedKeyPath.Length..].TrimStart('\\', '/');
                }
                else if (trimmed.StartsWith(keyPath, StringComparison.OrdinalIgnoreCase) && trimmed.Length > keyPath.Length)
                {
                    sub = trimmed[keyPath.Length..].TrimStart('\\', '/');
                }

                if (!string.IsNullOrEmpty(sub) && !sub.Contains('\\'))
                {
                    list.Add(sub);
                }
            }

            return list.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? GetValueViaRegExe(string keyPath, string valueName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"query \"{keyPath}\" /v \"{valueName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(valueName, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(new[] { "REG_SZ", "REG_EXPAND_SZ", "REG_DWORD" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        return parts[1].Trim();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
