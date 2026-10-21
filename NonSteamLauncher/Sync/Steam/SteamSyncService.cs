using System.Diagnostics;

namespace NonSteamLauncher.Sync.Steam;

public class SteamSyncResult
{
    public int DetectedGamesCount { get; set; }
    public int AddedShortcutsCount { get; set; }
    public int ExistingShortcutsCount { get; set; }
    public int UpdatedProfilesCount { get; set; }
    public List<InstalledGame> AddedGames { get; set; } = new();
    public List<InstalledGame> ExistingGames { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SteamSyncService
{
    private readonly GameDetector _gameDetector;
    private readonly SteamLocator _steamLocator;
    private readonly Logger _logger;

    public SteamSyncService(GameDetector? gameDetector = null, SteamLocator? steamLocator = null, Logger? logger = null)
    {
        _logger = logger ?? new Logger(verbose: true);
        _gameDetector = gameDetector ?? new GameDetector(logger: _logger);
        _steamLocator = steamLocator ?? new SteamLocator(logger: _logger);
    }

    public SteamSyncResult Sync(Options options)
    {
        var result = new SteamSyncResult();

        _logger.LogInfo("=== Starting Steam Library Sync ===");
        _logger.LogInfo("Scanning for installed games across launchers (Epic Games, EA, Ubisoft, Battle.net, GOG)...");

        var games = _gameDetector.DetectAllGames();
        result.DetectedGamesCount = games.Count;

        if (games.Count == 0)
        {
            _logger.LogWarning("No installed games were detected across launchers.");
            result.Success = true;
            return result;
        }

        _logger.LogInfo($"Found {games.Count} installed game(s):");
        foreach (var g in games)
        {
            _logger.LogInfo($"  - [{g.Launcher}] {g.Title}");
        }

        string? steamDir = _steamLocator.FindSteamInstallDirectory(options.SteamPath);
        if (string.IsNullOrEmpty(steamDir))
        {
            string err = "Could not locate Steam installation directory. Please specify it using --steam-path <path>.";
            _logger.LogError(err);
            result.ErrorMessage = err;
            result.Success = false;
            return result;
        }

        _logger.LogInfo($"Steam installation directory: '{steamDir}'");

        if (_steamLocator.IsSteamRunning())
        {
            _logger.LogWarning("----------------------------------------------------------------------");
            _logger.LogWarning("⚠️  WARNING: Steam is currently running!");
            _logger.LogWarning("   Steam only reloads 'shortcuts.vdf' when starting up.");
            _logger.LogWarning("   Please exit Steam (or restart Steam after sync finishes)");
            _logger.LogWarning("   so your newly added games appear in your library.");
            _logger.LogWarning("----------------------------------------------------------------------");
        }

        var profiles = _steamLocator.FindUserProfiles(steamDir);
        if (profiles.Count == 0)
        {
            string err = $"No Steam user profiles found in '{Path.Combine(steamDir, "userdata")}'.";
            _logger.LogError(err);
            result.ErrorMessage = err;
            result.Success = false;
            return result;
        }

        string rawExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "NonSteamLauncher.exe";
        string exePath = $"\"{rawExe}\"";
        string startDir = $"\"{Path.GetDirectoryName(rawExe) ?? ""}\"";

        foreach (var profileDir in profiles)
        {
            string profileId = Path.GetFileName(profileDir);
            string configDir = Path.Combine(profileDir, "config");
            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            string backupPath = Path.Combine(configDir, "shortcuts.vdf.bak");

            _logger.LogInfo($"Processing Steam user profile: {profileId}");

            List<SteamShortcut> shortcuts;
            if (File.Exists(shortcutsPath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(shortcutsPath);
                    shortcuts = VdfBinaryReader.ReadShortcuts(data);
                    _logger.LogInfo($"  Loaded {shortcuts.Count} existing shortcut(s) from 'shortcuts.vdf'.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"  Error reading existing 'shortcuts.vdf' in profile {profileId}: {ex.Message}. Starting fresh.");
                    shortcuts = new List<SteamShortcut>();
                }
            }
            else
            {
                shortcuts = new List<SteamShortcut>();
                _logger.LogInfo("  No existing 'shortcuts.vdf' found; a new file will be created.");
            }

            int profileAdded = 0;
            int profileExisting = 0;

            foreach (var game in games)
            {
                var existingShortcut = shortcuts.FirstOrDefault(s =>
                    s.AppName.Equals(game.Title, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(game.LaunchUri) && s.LaunchOptions.Contains(game.LaunchUri, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(game.ExecutablePath) && s.Exe.Trim('"', '\'').Equals(game.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                );

                if (existingShortcut != null)
                {
                    // Check if existing shortcut in Steam is already using NonSteamLauncher
                    if (existingShortcut.Exe.Contains("NonSteamLauncher", StringComparison.OrdinalIgnoreCase))
                    {
                        profileExisting++;
                        if (!result.ExistingGames.Any(g => g.Title.Equals(game.Title, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.ExistingGames.Add(game);
                        }
                        _logger.LogDebug($"  Game '{game.Title}' already configured with NonSteamLauncher. Skipping.");
                        continue;
                    }

                    // Existing shortcut calls the .exe directly! Upgrade it to use NonSteamLauncher wrapper
                    _logger.LogInfo($"  ⚡ Upgrading existing Steam shortcut '{existingShortcut.AppName}' from direct exe to NonSteamLauncher wrapper...");
                    existingShortcut.Exe = exePath;
                    existingShortcut.StartDir = startDir;
                    existingShortcut.LaunchOptions = $"--launch \"{game.LaunchUri}\" --close-launcher --disable-overlay";
                    if (string.IsNullOrEmpty(existingShortcut.Icon) && !string.IsNullOrEmpty(game.IconPath))
                    {
                        existingShortcut.Icon = game.IconPath;
                    }
                    if (!existingShortcut.Tags.Contains(game.Launcher))
                    {
                        existingShortcut.Tags.Add(game.Launcher);
                    }

                    profileAdded++;
                    if (!result.AddedGames.Any(g => g.Title.Equals(game.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.AddedGames.Add(game);
                    }
                    _logger.LogInfo($"  ✔ Upgraded [{game.Launcher}] {game.Title} to NonSteamLauncher wrapper!");
                    continue;
                }

                // Construct new shortcut
                string launchOptions = $"--launch \"{game.LaunchUri}\" --close-launcher --disable-overlay";

                var newShortcut = new SteamShortcut
                {
                    AppName = game.Title,
                    Exe = exePath,
                    StartDir = startDir,
                    LaunchOptions = launchOptions,
                    Icon = game.IconPath ?? "",
                    Tags = new List<string> { game.Launcher },
                    AllowDesktopConfig = 1,
                    AllowOverlay = 1,
                    AppId = SteamCrc32.GenerateAppId(exePath, game.Title)
                };

                shortcuts.Add(newShortcut);
                profileAdded++;
                if (!result.AddedGames.Any(g => g.Title.Equals(game.Title, StringComparison.OrdinalIgnoreCase)))
                {
                    result.AddedGames.Add(game);
                }
                _logger.LogInfo($"  + Added [{game.Launcher}] {game.Title} (AppID: {newShortcut.AppId})");
            }

            // Inspect any other existing shortcuts in shortcuts.vdf:
            // If they point directly to an exe belonging to a known launcher, upgrade them!
            foreach (var s in shortcuts)
            {
                if (s.Exe.Contains("NonSteamLauncher", StringComparison.OrdinalIgnoreCase))
                    continue;

                string cleanExe = s.Exe.Trim('"', '\'');
                string? detectedLauncher = DetectLauncherFromPath(cleanExe, s.StartDir);
                if (detectedLauncher != null)
                {
                    _logger.LogInfo($"  ⚡ Detected direct-exe shortcut '{s.AppName}' [{detectedLauncher}]. Upgrading to NonSteamLauncher...");
                    s.Exe = exePath;
                    s.StartDir = startDir;
                    s.LaunchOptions = $"--launch \"{cleanExe}\" --close-launcher --disable-overlay";
                    if (string.IsNullOrEmpty(s.Icon) && File.Exists(cleanExe))
                    {
                        s.Icon = cleanExe;
                    }
                    if (!s.Tags.Contains(detectedLauncher))
                    {
                        s.Tags.Add(detectedLauncher);
                    }

                    var adoptedGame = new InstalledGame
                    {
                        Title = s.AppName,
                        Launcher = detectedLauncher,
                        LaunchUri = cleanExe,
                        ExecutablePath = cleanExe,
                        IconPath = s.Icon
                    };

                    profileAdded++;
                    if (!result.AddedGames.Any(g => g.Title.Equals(adoptedGame.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.AddedGames.Add(adoptedGame);
                    }
                    _logger.LogInfo($"  ✔ Upgraded [{detectedLauncher}] {s.AppName} to NonSteamLauncher wrapper!");
                }
            }

            result.AddedShortcutsCount += profileAdded;
            result.ExistingShortcutsCount += profileExisting;

            if (profileAdded > 0)
            {
                if (options.DryRun)
                {
                    _logger.LogInfo($"  [DRY RUN] Would save {shortcuts.Count} total shortcuts to '{shortcutsPath}'.");
                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(configDir);

                        // Backup existing shortcuts.vdf if it exists
                        if (File.Exists(shortcutsPath))
                        {
                            File.Copy(shortcutsPath, backupPath, overwrite: true);
                            _logger.LogDebug($"  Backup created at '{backupPath}'.");
                        }

                        byte[] outputData = VdfBinaryWriter.WriteShortcuts(shortcuts);
                        File.WriteAllBytes(shortcutsPath, outputData);
                        _logger.LogInfo($"  Saved {shortcuts.Count} shortcut(s) to '{shortcutsPath}'.");
                        result.UpdatedProfilesCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"  Error writing 'shortcuts.vdf' for profile {profileId}: {ex.Message}");
                    }
                }
            }
            else
            {
                _logger.LogInfo($"  All {games.Count} game(s) are already present in this profile.");
            }
        }

        result.Success = true;
        _logger.LogInfo("=== Steam Library Sync Completed Successfully ===");
        return result;
    }

    public static string? DetectLauncherFromPath(string exePath, string? startDir)
    {
        string combined = $"{exePath} {startDir}";

        if (combined.Contains("EA Games", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Origin Games", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase))
        {
            return "EA App";
        }

        if (combined.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Uplay", StringComparison.OrdinalIgnoreCase))
        {
            return "Ubisoft Connect";
        }

        if (combined.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
        {
            return "Epic Games";
        }

        if (combined.Contains("GOG Games", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("GOG Galaxy", StringComparison.OrdinalIgnoreCase))
        {
            return "GOG Galaxy";
        }

        if (combined.Contains("Battle.net", StringComparison.OrdinalIgnoreCase))
        {
            return "Battle.net";
        }

        if (combined.Contains("XboxGames", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("gamelaunchhelper", StringComparison.OrdinalIgnoreCase))
        {
            return "Xbox";
        }

        return null;
    }
}
