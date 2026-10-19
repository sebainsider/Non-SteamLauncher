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
                // Check if already in shortcuts by AppName or by LaunchUri inside LaunchOptions
                bool alreadyExists = shortcuts.Any(s =>
                    s.AppName.Equals(game.Title, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(game.LaunchUri) && s.LaunchOptions.Contains(game.LaunchUri, StringComparison.OrdinalIgnoreCase))
                );

                if (alreadyExists)
                {
                    profileExisting++;
                    if (!result.ExistingGames.Any(g => g.Title.Equals(game.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.ExistingGames.Add(game);
                    }
                    _logger.LogDebug($"  Game '{game.Title}' already configured in Steam. Skipping.");
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
}
