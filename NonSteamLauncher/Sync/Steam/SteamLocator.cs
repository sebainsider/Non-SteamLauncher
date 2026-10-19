using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NonSteamLauncher.Sync.Steam;

public class SteamLocator
{
    private readonly IRegistryReader _registry;
    private readonly Logger? _logger;

    public SteamLocator(IRegistryReader? registry = null, Logger? logger = null)
    {
        _registry = registry ?? new WindowsRegistryReader();
        _logger = logger;
    }

    public string? FindSteamInstallDirectory(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && Directory.Exists(explicitPath))
        {
            return explicitPath;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // 1. Try HKCU registry
            string? hkcuPath = _registry.GetValue(@"HKCU\Software\Valve\Steam", "SteamPath");
            if (!string.IsNullOrWhiteSpace(hkcuPath))
            {
                hkcuPath = hkcuPath.Replace('/', '\\');
                if (Directory.Exists(hkcuPath)) return hkcuPath;
            }

            // 2. Try HKLM registry
            string? hklmPath = _registry.GetValue(@"HKLM\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            if (!string.IsNullOrWhiteSpace(hklmPath))
            {
                hklmPath = hklmPath.Replace('/', '\\');
                if (Directory.Exists(hklmPath)) return hklmPath;
            }

            // 3. Try standard Windows paths
            var standardPaths = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                @"D:\Steam",
                @"E:\Steam"
            };

            foreach (var path in standardPaths)
            {
                if (Directory.Exists(path)) return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var linuxPaths = new[]
            {
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".steam", "root")
            };

            foreach (var path in linuxPaths)
            {
                if (Directory.Exists(path)) return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string macPath = Path.Combine(home, "Library", "Application Support", "Steam");
            if (Directory.Exists(macPath)) return macPath;
        }

        return null;
    }

    public List<string> FindUserProfiles(string steamDirectory)
    {
        var profiles = new List<string>();
        string userdataDir = Path.Combine(steamDirectory, "userdata");

        if (!Directory.Exists(userdataDir))
        {
            _logger?.LogWarning($"Steam 'userdata' directory not found at '{userdataDir}'");
            return profiles;
        }

        try
        {
            var dirs = Directory.GetDirectories(userdataDir);
            foreach (var dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                // Profile folders are Steam3 IDs (numeric string, not "0" or "anonymous")
                if (uint.TryParse(dirName, out var id) && id > 0)
                {
                    profiles.Add(dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error reading Steam user profiles: {ex.Message}");
        }

        return profiles;
    }

    public bool IsSteamRunning()
    {
        try
        {
            return Process.GetProcessesByName("steam").Length > 0 ||
                   Process.GetProcessesByName("Steam").Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
