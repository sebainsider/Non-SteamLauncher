namespace NonSteamLauncher.Sync;

public class EAAppScanner : IScanner
{
    private readonly IRegistryReader _registry;
    private readonly IEnumerable<string>? _customDirectories;
    private readonly Logger? _logger;

    public string ScannerName => "EA App";

    public EAAppScanner(IRegistryReader? registry = null, IEnumerable<string>? customDirectories = null, Logger? logger = null)
    {
        _registry = registry ?? new WindowsRegistryReader();
        _customDirectories = customDirectories;
        _logger = logger;
    }

    public IReadOnlyList<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan Registry Entries
        var registryBases = new[]
        {
            @"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Games",
            @"HKLM\SOFTWARE\Electronic Arts\EA Games",
            @"HKLM\SOFTWARE\WOW6432Node\EA Games",
            @"HKLM\SOFTWARE\EA Games",
            @"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Core\Installed Games"
        };

        foreach (var regBase in registryBases)
        {
            var subKeys = _registry.GetSubKeyNames(regBase);
            foreach (var subKey in subKeys)
            {
                string fullKey = $"{regBase}\\{subKey}";
                string? installDir = _registry.GetValue(fullKey, "Install Dir") ??
                                     _registry.GetValue(fullKey, "InstallDir") ??
                                     _registry.GetValue(fullKey, "Path");
                string? displayName = _registry.GetValue(fullKey, "DisplayName") ?? subKey;

                if (!string.IsNullOrWhiteSpace(installDir))
                {
                    installDir = installDir.Trim('"', '\'').TrimEnd('\\', '/');
                    string? exe = FindMainExecutable(installDir);

                    if (exe != null && seenTitles.Add(displayName))
                    {
                        games.Add(new InstalledGame
                        {
                            Title = displayName,
                            Launcher = "EA App",
                            LaunchUri = exe,
                            InstallDirectory = installDir,
                            ExecutablePath = exe,
                            IconPath = exe
                        });
                        _logger?.LogDebug($"Found EA game in registry: {displayName} ({exe})");
                    }
                }
            }
        }

        // 2. Scan Standard EA Installation Folders
        var searchDirs = _customDirectories?.ToList() ?? GetDefaultEADirectories();

        foreach (var parentDir in searchDirs)
        {
            if (!Directory.Exists(parentDir))
                continue;

            try
            {
                var subDirs = Directory.GetDirectories(parentDir);
                foreach (var gameDir in subDirs)
                {
                    string folderName = Path.GetFileName(gameDir.TrimEnd('\\', '/'));
                    if (string.IsNullOrWhiteSpace(folderName) || seenTitles.Contains(folderName))
                        continue;

                    string? exe = FindMainExecutable(gameDir);
                    if (exe != null && seenTitles.Add(folderName))
                    {
                        games.Add(new InstalledGame
                        {
                            Title = folderName,
                            Launcher = "EA App",
                            LaunchUri = exe,
                            InstallDirectory = gameDir,
                            ExecutablePath = exe,
                            IconPath = exe
                        });
                        _logger?.LogDebug($"Found EA game in directory: {folderName} ({exe})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Error reading EA directory '{parentDir}': {ex.Message}");
            }
        }

        // 3. Scan Origin / EA Desktop LocalContent manifests
        ScanLocalContentManifests(games, seenTitles);

        return games;
    }

    private void ScanLocalContentManifests(List<InstalledGame> games, HashSet<string> seenTitles)
    {
        var localContentPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Origin", "LocalContent"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA Desktop", "LocalContent")
        };

        foreach (var basePath in localContentPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            try
            {
                var manifestFiles = Directory.GetFiles(basePath, "*.mfst", SearchOption.AllDirectories);
                foreach (var mfst in manifestFiles)
                {
                    string title = Path.GetFileNameWithoutExtension(mfst);
                    if (string.IsNullOrWhiteSpace(title) || seenTitles.Contains(title))
                        continue;

                    // Try to see if there's a matching folder in EA Games
                    foreach (var eaParent in GetDefaultEADirectories())
                    {
                        string candidateDir = Path.Combine(eaParent, title);
                        if (Directory.Exists(candidateDir))
                        {
                            string? exe = FindMainExecutable(candidateDir);
                            if (exe != null && seenTitles.Add(title))
                            {
                                games.Add(new InstalledGame
                                {
                                    Title = title,
                                    Launcher = "EA App",
                                    LaunchUri = exe,
                                    InstallDirectory = candidateDir,
                                    ExecutablePath = exe,
                                    IconPath = exe
                                });
                                _logger?.LogDebug($"Found EA game via manifest: {title} ({exe})");
                            }
                            break;
                        }
                    }
                }
            }
            catch { }
        }
    }

    public static string? FindMainExecutable(string dir)
    {
        if (!Directory.Exists(dir))
            return null;

        try
        {
            var exes = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(e => !e.Contains("crash", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("report", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("unity", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("unins", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("setup", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("installer", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("touchup", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("cleanup", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("activation", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("eax", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("eadesktop", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exes.Count == 0)
                return null;

            string folderName = Path.GetFileName(dir.TrimEnd('\\', '/'));
            string normFolder = folderName.Replace(" ", "");

            // 1. Look for an executable matching the folder name (e.g. UnravelTwo.exe for "Unravel Two")
            var match = exes.FirstOrDefault(e =>
                Path.GetFileNameWithoutExtension(e).Replace(" ", "").Equals(normFolder, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;

            // 2. Pick the largest executable (the main game binary)
            return exes.OrderByDescending(e =>
            {
                try { return new FileInfo(e).Length; }
                catch { return 0; }
            }).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetDefaultEADirectories()
    {
        var dirs = new List<string>
        {
            @"C:\Program Files\EA Games",
            @"C:\Program Files (x86)\Origin Games",
            @"C:\Program Files\Electronic Arts",
            @"D:\EA Games",
            @"D:\Origin Games",
            @"E:\EA Games",
            @"E:\Origin Games"
        };

        return dirs;
    }
}
