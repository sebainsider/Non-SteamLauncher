using System.Diagnostics;
using System.Runtime.InteropServices;

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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Array.Empty<string>();

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

            var list = new List<string>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(keyPath, StringComparison.OrdinalIgnoreCase) && trimmed.Length > keyPath.Length)
                {
                    string sub = trimmed[keyPath.Length..].TrimStart('\\', '/');
                    if (!string.IsNullOrEmpty(sub) && !sub.Contains('\\'))
                    {
                        list.Add(sub);
                    }
                }
            }

            return list.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string? GetValue(string keyPath, string valueName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

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
                    // Format: ValueName    REG_SZ    ValueData
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
