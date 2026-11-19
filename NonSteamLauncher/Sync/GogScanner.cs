using System.Text.Json;

namespace NonSteamLauncher.Sync;

public class GogScanner : IScanner
{
    private readonly IRegistryReader _registry;
    private readonly Logger? _logger;

    public string ScannerName => "GOG Galaxy";

    public GogScanner(IRegistryReader? registry = null, Logger? logger = null)
    {
        _registry = registry ?? new WindowsRegistryReader();
        _logger = logger;
    }

    public IReadOnlyList<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();

        var registryPaths = new[]
        {
            @"HKLM\SOFTWARE\WOW6432Node\GOG.com\Games",
            @"HKLM\SOFTWARE\GOG.com\Games"
        };

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var basePath in registryPaths)
        {
            var subKeys = _registry.GetSubKeyNames(basePath);
            foreach (var gameId in subKeys)
            {
                if (!seenIds.Add(gameId))
                    continue;

                string fullKey = $"{basePath}\\{gameId}";

                // Skip DLC entries that depend on a base game
                string? dependsOn = _registry.GetValue(fullKey, "dependsOn");
                if (!string.IsNullOrWhiteSpace(dependsOn))
                    continue;

                string? gameName = _registry.GetValue(fullKey, "gameName");
                string? exe = _registry.GetValue(fullKey, "exe");
                string? path = _registry.GetValue(fullKey, "path");
                string? icon = _registry.GetValue(fullKey, "icon");

                if (string.IsNullOrWhiteSpace(gameName))
                    continue;

                var (resolvedExe, workingDir) = ResolveGameExecutable(path, gameId, exe, _logger);
                string? resolvedIcon = ResolveIcon(path, gameId, icon, resolvedExe);

                // GOG games can launch directly via executable (DRM-Free) or via goggalaxy:// URI
                string launchUri = resolvedExe ?? $"goggalaxy://openGameView/{gameId}";

                games.Add(new InstalledGame
                {
                    Title = gameName,
                    Launcher = "GOG Galaxy",
                    LaunchUri = launchUri,
                    InstallDirectory = workingDir ?? path,
                    ExecutablePath = resolvedExe,
                    IconPath = resolvedIcon
                });

                _logger?.LogDebug($"Found GOG game: {gameName} (ID: {gameId}, Exe: {resolvedExe ?? "none"})");
            }
        }

        return games;
    }

    public static (string? realExe, string? workingDir) ResolveGameExecutable(
        string? installPath,
        string gameId,
        string? registryExe,
        Logger? logger = null)
    {
        string? cleanInstallPath = installPath?.Trim('"', '\'');
        if (!string.IsNullOrEmpty(cleanInstallPath) && Directory.Exists(cleanInstallPath))
        {
            // 1. Check goggame-<gameId>.info or any goggame-*.info in installation directory
            string infoFile = Path.Combine(cleanInstallPath, $"goggame-{gameId}.info");
            if (!File.Exists(infoFile))
            {
                try
                {
                    var matches = Directory.GetFiles(cleanInstallPath, "goggame-*.info");
                    if (matches.Length > 0)
                    {
                        infoFile = matches[0];
                    }
                }
                catch { }
            }

            if (File.Exists(infoFile))
            {
                try
                {
                    string json = File.ReadAllText(infoFile);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("playTasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
                    {
                        string? candidateGameExe = null;
                        string? candidateWorkingDir = null;

                        foreach (var task in tasks.EnumerateArray())
                        {
                            if (!task.TryGetProperty("type", out var typeProp) ||
                                !string.Equals(typeProp.GetString(), "FileTask", StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (!task.TryGetProperty("path", out var pathProp))
                                continue;

                            string? taskPath = pathProp.GetString();
                            if (string.IsNullOrWhiteSpace(taskPath))
                                continue;

                            string category = task.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "" : "";
                            bool isPrimary = task.TryGetProperty("isPrimary", out var priProp) && priProp.GetBoolean();

                            // Skip launcher/bootstrap or uninstall tasks (e.g. REDprelauncher, GalaxyClient, unins000)
                            if (string.Equals(category, "launcher", StringComparison.OrdinalIgnoreCase) ||
                                taskPath.Contains("prelauncher", StringComparison.OrdinalIgnoreCase) ||
                                taskPath.Contains("REDlauncher", StringComparison.OrdinalIgnoreCase) ||
                                taskPath.Contains("unins", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            string combinedPath = Path.IsPathRooted(taskPath)
                                ? taskPath
                                : Path.Combine(cleanInstallPath, taskPath.Replace('/', '\\'));

                            if (File.Exists(combinedPath))
                            {
                                string? taskWorkDir = task.TryGetProperty("workingDir", out var wdProp) ? wdProp.GetString() : null;
                                string? combinedWorkDir = !string.IsNullOrEmpty(taskWorkDir)
                                    ? (Path.IsPathRooted(taskWorkDir) ? taskWorkDir : Path.Combine(cleanInstallPath, taskWorkDir.Replace('/', '\\')))
                                    : Path.GetDirectoryName(combinedPath);

                                if (string.Equals(category, "game", StringComparison.OrdinalIgnoreCase))
                                {
                                    // If modern DirectX 12 or 64-bit variant, return immediately; otherwise save as strong candidate
                                    if (combinedPath.Contains("dx12", StringComparison.OrdinalIgnoreCase) ||
                                        combinedPath.Contains("x64", StringComparison.OrdinalIgnoreCase))
                                    {
                                        logger?.LogDebug($"GogScanner: Resolved game executable via goggame info [category=game]: {combinedPath}");
                                        return (combinedPath, combinedWorkDir);
                                    }

                                    candidateGameExe ??= combinedPath;
                                    candidateWorkingDir ??= combinedWorkDir;
                                }
                                else if (isPrimary && candidateGameExe == null)
                                {
                                    candidateGameExe = combinedPath;
                                    candidateWorkingDir = combinedWorkDir;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(candidateGameExe))
                        {
                            logger?.LogDebug($"GogScanner: Resolved game executable via goggame info: {candidateGameExe}");
                            return (candidateGameExe, candidateWorkingDir);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogDebug($"GogScanner: Could not parse '{infoFile}': {ex.Message}");
                }
            }

            // 2. Check launcher-configuration.json (present in CDPR games like Witcher 3 Next-Gen)
            string launcherCfg = Path.Combine(cleanInstallPath, "launcher-configuration.json");
            if (File.Exists(launcherCfg))
            {
                try
                {
                    string json = File.ReadAllText(launcherCfg);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("executables", out var exes) && exes.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in exes.EnumerateArray())
                        {
                            if (item.TryGetProperty("executable", out var execObj))
                            {
                                string? dir = execObj.TryGetProperty("directoryPath", out var dp) ? dp.GetString() : null;
                                string? file = execObj.TryGetProperty("fileName", out var fn) ? fn.GetString() : null;

                                if (!string.IsNullOrEmpty(file))
                                {
                                    string full = !string.IsNullOrEmpty(dir)
                                        ? Path.Combine(cleanInstallPath, dir.Replace('/', '\\'), file)
                                        : Path.Combine(cleanInstallPath, file);

                                    if (File.Exists(full))
                                    {
                                        logger?.LogDebug($"GogScanner: Resolved game executable via launcher-configuration.json: {full}");
                                        return (full, Path.GetDirectoryName(full));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogDebug($"GogScanner: Could not parse '{launcherCfg}': {ex.Message}");
                }
            }

            // 3. Known CDPR fallbacks when registry points to intermediate REDprelauncher
            string? cleanRegistryExe = registryExe?.Trim('"', '\'');
            if (cleanRegistryExe != null &&
                (cleanRegistryExe.Contains("REDprelauncher", StringComparison.OrdinalIgnoreCase) ||
                 cleanRegistryExe.Contains("REDlauncher", StringComparison.OrdinalIgnoreCase)))
            {
                string[] knownCdprPaths = new[]
                {
                    Path.Combine(cleanInstallPath, "bin", "x64_dx12", "witcher3.exe"),
                    Path.Combine(cleanInstallPath, "bin", "x64", "witcher3.exe"),
                    Path.Combine(cleanInstallPath, "bin", "x64", "Cyberpunk2077.exe")
                };

                foreach (var candidate in knownCdprPaths)
                {
                    if (File.Exists(candidate))
                    {
                        logger?.LogDebug($"GogScanner: Resolved CDPR game executable via known path fallback: {candidate}");
                        return (candidate, Path.GetDirectoryName(candidate));
                    }
                }
            }
        }

        // 4. Default fallback: registry exe combined with install path
        string? defaultExe = null;
        if (!string.IsNullOrWhiteSpace(cleanInstallPath) && !string.IsNullOrWhiteSpace(registryExe))
        {
            string cleanExe = registryExe.Trim('"', '\'');
            defaultExe = Path.IsPathRooted(cleanExe) ? cleanExe : Path.Combine(cleanInstallPath, cleanExe);
        }
        else if (!string.IsNullOrWhiteSpace(registryExe))
        {
            defaultExe = registryExe.Trim('"', '\'');
        }

        return (defaultExe, !string.IsNullOrEmpty(defaultExe) && File.Exists(defaultExe) ? Path.GetDirectoryName(defaultExe) : cleanInstallPath);
    }

    public static string? ResolveIcon(string? installPath, string gameId, string? registryIcon, string? executablePath)
    {
        if (!string.IsNullOrWhiteSpace(registryIcon) && File.Exists(registryIcon))
            return registryIcon;

        string? cleanInstallPath = installPath?.Trim('"', '\'');
        if (!string.IsNullOrEmpty(cleanInstallPath) && Directory.Exists(cleanInstallPath))
        {
            string idIcon = Path.Combine(cleanInstallPath, $"goggame-{gameId}.ico");
            if (File.Exists(idIcon))
                return idIcon;

            try
            {
                var icoFiles = Directory.GetFiles(cleanInstallPath, "goggame-*.ico");
                if (icoFiles.Length > 0 && File.Exists(icoFiles[0]))
                    return icoFiles[0];

                string gogIco = Path.Combine(cleanInstallPath, "gog.ico");
                if (File.Exists(gogIco))
                    return gogIco;

                string supportIco = Path.Combine(cleanInstallPath, "support.ico");
                if (File.Exists(supportIco))
                    return supportIco;
            }
            catch { }
        }

        return executablePath;
    }
}
