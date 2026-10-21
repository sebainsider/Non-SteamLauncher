using System.Xml.Linq;

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
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAddGame(InstalledGame? game, string source)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.Title) || string.IsNullOrWhiteSpace(game.LaunchUri))
                return;

            string normTitle = GameDetector.NormalizeTitle(game.Title);
            if (string.IsNullOrEmpty(normTitle))
                normTitle = game.Title;

            string normDir = string.IsNullOrWhiteSpace(game.InstallDirectory)
                ? ""
                : game.InstallDirectory.TrimEnd('\\', '/');

            if (!string.IsNullOrEmpty(normDir) && seenDirs.Contains(normDir))
                return;

            if (!seenTitles.Add(normTitle))
                return;

            if (!string.IsNullOrEmpty(normDir))
                seenDirs.Add(normDir);

            games.Add(game);
            _logger?.LogDebug($"Found EA game ({source}): {game.Title} ({game.LaunchUri})");
        }

        // 1. Scan Classic EA Registry Entries
        var registryBases = new[]
        {
            @"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Games",
            @"HKLM\SOFTWARE\Electronic Arts\EA Games",
            @"HKLM\SOFTWARE\WOW6432Node\EA Games",
            @"HKLM\SOFTWARE\EA Games",
            @"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Core\Installed Games",
            @"HKLM\SOFTWARE\WOW6432Node\Origin Games",
            @"HKLM\SOFTWARE\Origin Games"
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
                    var game = CreateGameFromDirectory(installDir, displayName);
                    TryAddGame(game, "registry");
                }
            }
        }

        // 2. Scan Windows Uninstall Registry for EA Installer / Electronic Arts games
        ScanUninstallRegistry(TryAddGame);

        // 3. Scan Dedicated EA Installation Folders (or custom test directories)
        var dedicatedDirs = _customDirectories?.ToList() ?? GetDefaultEADirectories();
        foreach (var parentDir in dedicatedDirs)
        {
            ScanDirectoryForGames(parentDir, requireInstallerManifest: false, TryAddGame);
        }

        // 4. Scan EA Desktop configured download directories (user_*.ini / machine.ini) & shared game folders
        if (_customDirectories == null)
        {
            var sharedDirs = GetConfiguredAndSharedEADirectories();
            foreach (var parentDir in sharedDirs)
            {
                ScanDirectoryForGames(parentDir, requireInstallerManifest: true, TryAddGame);
            }
        }

        // 5. Scan Origin / EA Desktop LocalContent manifests & InstallData
        ScanLocalContentManifests(TryAddGame);

        return games;
    }

    private void ScanUninstallRegistry(Action<InstalledGame?, string> tryAddGame)
    {
        var uninstallBases = new[]
        {
            @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var regBase in uninstallBases)
        {
            var subKeys = _registry.GetSubKeyNames(regBase);
            foreach (var subKey in subKeys)
            {
                if (subKey.StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fullKey = $"{regBase}\\{subKey}";
                string? installLocation = _registry.GetValue(fullKey, "InstallLocation");
                if (string.IsNullOrWhiteSpace(installLocation))
                    continue;

                installLocation = installLocation.Trim('"', '\'').TrimEnd('\\', '/');
                if (IsLauncherClientDirectory(installLocation))
                    continue;

                string? displayName = _registry.GetValue(fullKey, "DisplayName");
                if (string.IsNullOrWhiteSpace(displayName) || IsLauncherClientTitle(displayName))
                    continue;

                string? uninstallString = _registry.GetValue(fullKey, "UninstallString") ?? "";
                if (uninstallString.Contains("steam://", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? publisher = _registry.GetValue(fullKey, "Publisher") ?? "";
                string manifestPath = Path.Combine(installLocation, "__Installer", "installerdata.xml");

                bool isEaInstaller = uninstallString.Contains("EAInstaller", StringComparison.OrdinalIgnoreCase) ||
                                     File.Exists(manifestPath) ||
                                     publisher.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase);

                if (!isEaInstaller)
                    continue;

                string? displayIcon = _registry.GetValue(fullKey, "DisplayIcon");
                var game = CreateGameFromDirectory(installLocation, displayName, displayIcon);
                tryAddGame(game, "uninstall-registry");
            }
        }
    }

    private void ScanDirectoryForGames(string parentDir, bool requireInstallerManifest, Action<InstalledGame?, string> tryAddGame)
    {
        if (string.IsNullOrWhiteSpace(parentDir) || !Directory.Exists(parentDir))
            return;

        try
        {
            var subDirs = Directory.GetDirectories(parentDir);
            foreach (var gameDir in subDirs)
            {
                if (IsLauncherClientDirectory(gameDir))
                    continue;

                string folderName = Path.GetFileName(gameDir.TrimEnd('\\', '/'));
                if (string.IsNullOrWhiteSpace(folderName))
                    continue;

                string manifestPath = Path.Combine(gameDir, "__Installer", "installerdata.xml");
                if (requireInstallerManifest && !File.Exists(manifestPath))
                    continue;

                var game = CreateGameFromDirectory(gameDir, fallbackTitle: folderName);
                tryAddGame(game, requireInstallerManifest ? "installerdata-manifest" : "directory");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug($"Error reading EA directory '{parentDir}': {ex.Message}");
        }
    }

    private InstalledGame? CreateGameFromDirectory(string installDir, string? fallbackTitle = null, string? fallbackIconOrExe = null)
    {
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            return null;

        string manifestPath = Path.Combine(installDir, "__Installer", "installerdata.xml");
        string? title = null;
        string? exe = null;

        if (File.Exists(manifestPath))
        {
            try
            {
                string xml = File.ReadAllText(manifestPath);
                var parsed = ParseInstallerDataXml(xml, installDir, _registry);
                title = parsed.Title;
                exe = parsed.ExecutablePath;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Error parsing '{manifestPath}': {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            if (!string.IsNullOrWhiteSpace(fallbackIconOrExe))
            {
                string cleanIcon = fallbackIconOrExe.Split(',')[0].Trim('"', '\'');
                if (cleanIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(cleanIcon) && !IsExcludedExecutable(cleanIcon))
                {
                    exe = cleanIcon;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            exe = FindMainExecutable(installDir);
        }

        if (string.IsNullOrWhiteSpace(exe))
            return null;

        string finalTitle = !string.IsNullOrWhiteSpace(title)
            ? title
            : (!string.IsNullOrWhiteSpace(fallbackTitle) ? CleanGameTitle(fallbackTitle) : Path.GetFileName(installDir.TrimEnd('\\', '/')));

        if (string.IsNullOrWhiteSpace(finalTitle))
            return null;

        return new InstalledGame
        {
            Title = finalTitle,
            Launcher = "EA App",
            LaunchUri = exe,
            InstallDirectory = installDir,
            ExecutablePath = exe,
            IconPath = exe
        };
    }

    public static (string? Title, string? ExecutablePath, string? ContentId) ParseInstallerDataXml(
        string xmlContent,
        string? installDir = null,
        IRegistryReader? registry = null)
    {
        var doc = XDocument.Parse(xmlContent);
        var root = doc.Root;
        if (root == null)
            return (null, null, null);

        string? contentId = root.Element("contentIDs")?
            .Elements("contentID")
            .Select(e => e.Value.Trim())
            .FirstOrDefault(v => !string.IsNullOrEmpty(v));

        // 1. Check if Origin Games registry has a clean DisplayName for this contentId
        string? title = null;
        if (!string.IsNullOrEmpty(contentId) && registry != null)
        {
            title = registry.GetValue($@"HKLM\SOFTWARE\WOW6432Node\Origin Games\{contentId}", "DisplayName") ??
                    registry.GetValue($@"HKLM\SOFTWARE\Origin Games\{contentId}", "DisplayName");
        }

        // 2. Otherwise read from <gameTitles>
        if (string.IsNullOrWhiteSpace(title))
        {
            var gameTitles = root.Element("gameTitles")?.Elements("gameTitle").ToList();
            if (gameTitles != null && gameTitles.Count > 0)
            {
                var enTitle = gameTitles.FirstOrDefault(t =>
                    string.Equals(t.Attribute("locale")?.Value, "en_US", StringComparison.OrdinalIgnoreCase));
                title = (enTitle ?? gameTitles[0]).Value.Trim();
            }
        }

        // 3. Resolve non-trial launcher executable from <runtime><launcher>
        string? exePath = null;
        var launchers = root.Element("runtime")?.Elements("launcher").ToList();
        if (launchers != null && launchers.Count > 0)
        {
            var nonTrialLauncher = launchers.FirstOrDefault(l =>
            {
                string? trialVal = l.Element("trial")?.Value.Trim();
                return !string.Equals(trialVal, "1", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(trialVal, "true", StringComparison.OrdinalIgnoreCase);
            }) ?? launchers[0];

            if (string.IsNullOrWhiteSpace(title))
            {
                var names = nonTrialLauncher.Elements("name").ToList();
                var enName = names.FirstOrDefault(n =>
                    string.Equals(n.Attribute("locale")?.Value, "en_US", StringComparison.OrdinalIgnoreCase));
                title = (enName ?? names.FirstOrDefault())?.Value.Trim();
            }

            string? rawFilePath = nonTrialLauncher.Element("filePath")?.Value.Trim();
            if (!string.IsNullOrWhiteSpace(rawFilePath))
            {
                string relativeExe = rawFilePath;
                int bracketEnd = rawFilePath.IndexOf(']');
                if (bracketEnd >= 0 && bracketEnd + 1 < rawFilePath.Length)
                {
                    relativeExe = rawFilePath[(bracketEnd + 1)..].TrimStart('\\', '/');
                }

                if (!string.IsNullOrWhiteSpace(installDir) && !string.IsNullOrWhiteSpace(relativeExe))
                {
                    exePath = Path.Combine(installDir, relativeExe.Replace('/', '\\'));
                }
                else
                {
                    exePath = relativeExe;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            title = CleanGameTitle(title);
        }

        return (title, exePath, contentId);
    }

    public static string CleanGameTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return string.Empty;

        string cleaned = rawTitle
            .Replace("™", "")
            .Replace("®", "")
            .Trim();

        // Collapse multiple spaces
        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " ");
        }

        return cleaned;
    }

    private void ScanLocalContentManifests(Action<InstalledGame?, string> tryAddGame)
    {
        var localContentPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Origin", "LocalContent"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA Desktop", "LocalContent"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA Desktop", "InstallData")
        };

        var candidateParents = GetDefaultEADirectories()
            .Concat(GetConfiguredAndSharedEADirectories())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var basePath in localContentPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            try
            {
                var subDirs = Directory.GetDirectories(basePath);
                foreach (var subDir in subDirs)
                {
                    string folderName = Path.GetFileName(subDir.TrimEnd('\\', '/'));
                    if (string.IsNullOrWhiteSpace(folderName))
                        continue;

                    foreach (var eaParent in candidateParents)
                    {
                        string candidateDir = Path.Combine(eaParent, folderName);
                        if (Directory.Exists(candidateDir))
                        {
                            var game = CreateGameFromDirectory(candidateDir, folderName);
                            tryAddGame(game, "localcontent-dir");
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
            // 1. Check __Installer\installerdata.xml first if available
            string manifestPath = Path.Combine(dir, "__Installer", "installerdata.xml");
            if (File.Exists(manifestPath))
            {
                try
                {
                    string xml = File.ReadAllText(manifestPath);
                    var (_, parsedExe, _) = ParseInstallerDataXml(xml, dir);
                    if (!string.IsNullOrWhiteSpace(parsedExe) && File.Exists(parsedExe))
                    {
                        return parsedExe;
                    }
                }
                catch { }
            }

            // 2. Search top directory executables
            var exes = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(e => !IsExcludedExecutable(e))
                .ToList();

            // 3. If no valid exe in top directory, search subdirectories (e.g., Binaries\Win64)
            if (exes.Count == 0)
            {
                exes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    .Where(e => !IsExcludedExecutable(e) && !IsExcludedSubdirectory(dir, e))
                    .ToList();
            }

            if (exes.Count == 0)
                return null;

            string folderName = Path.GetFileName(dir.TrimEnd('\\', '/'));
            string normFolder = folderName.Replace(" ", "");

            // Prefer executable matching the folder name (e.g. UnravelTwo.exe for "Unravel Two")
            var match = exes.FirstOrDefault(e =>
                Path.GetFileNameWithoutExtension(e).Replace(" ", "").Equals(normFolder, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;

            // Otherwise pick the largest executable (the main game binary)
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

    private static bool IsExcludedExecutable(string exePath)
    {
        string fileName = Path.GetFileName(exePath);
        return fileName.Contains("crash", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("report", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("unity", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("unins", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("installer", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("touchup", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("cleanup", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("activation", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("eax", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("eadesktop", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("ealauncher", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("eauninstall", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("link2ea", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("origin", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("vc_redist", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("dxsetup", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("_trial", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("trial.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedSubdirectory(string rootDir, string filePath)
    {
        string relative = Path.GetRelativePath(rootDir, filePath);
        return relative.StartsWith("__Installer", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("Support", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("Core", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("Redist", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("DirectX", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLauncherClientDirectory(string dir)
    {
        string trimmed = dir.TrimEnd('\\', '/');
        string folderName = Path.GetFileName(trimmed);
        return folderName.Equals("EA Desktop", StringComparison.OrdinalIgnoreCase) ||
               folderName.Equals("EA Core", StringComparison.OrdinalIgnoreCase) ||
               folderName.Equals("EADM", StringComparison.OrdinalIgnoreCase) ||
               folderName.Equals("Origin", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(@"\Electronic Arts\EA Desktop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLauncherClientTitle(string title)
    {
        string trimmed = title.Trim();
        return trimmed.Equals("EA app", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("EA Desktop", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Origin", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("EA Background Service", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> GetDefaultEADirectories()
    {
        return new List<string>
        {
            @"C:\Program Files\EA Games",
            @"C:\Program Files (x86)\Origin Games",
            @"D:\EA Games",
            @"D:\Origin Games",
            @"E:\EA Games",
            @"E:\Origin Games"
        };
    }

    public static List<string> GetConfiguredAndSharedEADirectories()
    {
        var dirs = new List<string>();

        void AddIfValid(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string clean = path.Trim().Trim('"', '\'').TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(clean) && !dirs.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                dirs.Add(clean);
            }
        }

        // 1. Read %LOCALAPPDATA%\Electronic Arts\EA Desktop\user_*.ini -> user.downloadinplacedir
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string eaLocalDir = Path.Combine(localAppData, "Electronic Arts", "EA Desktop");
            if (Directory.Exists(eaLocalDir))
            {
                foreach (var iniFile in Directory.GetFiles(eaLocalDir, "user_*.ini", SearchOption.TopDirectoryOnly))
                {
                    foreach (var line in File.ReadAllLines(iniFile))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("user.downloadinplacedir=", StringComparison.OrdinalIgnoreCase))
                        {
                            AddIfValid(trimmed["user.downloadinplacedir=".Length..]);
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Read %ProgramData%\EA Desktop\machine.ini -> machine.downloadinplacedir
        try
        {
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string machineIni = Path.Combine(progData, "EA Desktop", "machine.ini");
            if (File.Exists(machineIni))
            {
                foreach (var line in File.ReadAllLines(machineIni))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("machine.downloadinplacedir=", StringComparison.OrdinalIgnoreCase))
                    {
                        AddIfValid(trimmed["machine.downloadinplacedir=".Length..]);
                    }
                }
            }
        }
        catch { }

        // 3. Common shared game root folders where users install EA games
        AddIfValid(@"C:\Games");
        AddIfValid(@"D:\Games");
        AddIfValid(@"E:\Games");

        return dirs;
    }
}
