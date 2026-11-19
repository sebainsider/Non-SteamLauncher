using System.Diagnostics;

namespace NonSteamLauncher.Sync.Steam;

public class SteamSyncResult
{
    public int DetectedGamesCount { get; set; }
    public int AddedShortcutsCount { get; set; }
    public int ExistingShortcutsCount { get; set; }
    public int RemovedShortcutsCount { get; set; }
    public int UpdatedProfilesCount { get; set; }
    public int ArtworkImagesDownloadedCount { get; set; }
    public int ArtworkGamesUpdatedCount { get; set; }
    public List<InstalledGame> AddedGames { get; set; } = new();
    public List<InstalledGame> ExistingGames { get; set; } = new();
    public List<InstalledGame> PendingGames { get; set; } = new();
    public List<InstalledGame> RemovedGames { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SteamSyncService
{
    private readonly GameDetector _gameDetector;
    private readonly SteamLocator _steamLocator;
    private readonly SteamArtworkService _artworkService;
    private readonly Logger _logger;

    public SteamSyncService(
        GameDetector? gameDetector = null,
        SteamLocator? steamLocator = null,
        Logger? logger = null,
        SteamArtworkService? artworkService = null)
    {
        _logger = logger ?? new Logger(verbose: true);
        _gameDetector = gameDetector ?? new GameDetector(logger: _logger);
        _steamLocator = steamLocator ?? new SteamLocator(logger: _logger);
        _artworkService = artworkService ?? new SteamArtworkService(logger: _logger);
    }

    private AppSettings ResolveSettings(Options options)
    {
        var settings = AppSettings.Load(options.SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(options.SteamGridDbApiKey))
        {
            settings.SteamGridDbApiKey = options.SteamGridDbApiKey.Trim();
            if (!options.DryRun)
            {
                settings.Save(options.SettingsFilePath);
            }
        }
        return settings;
    }

    /// <summary>
    /// Scans installed games and inspects Steam's shortcuts.vdf WITHOUT modifying any files.
    /// Populates ExistingGames (already synced with NonSteamLauncher) and PendingGames (available to add).
    /// </summary>
    public SteamSyncResult Inspect(Options options)
    {
        ResolveSettings(options);
        var result = new SteamSyncResult();

        _logger.LogInfo("=== Inspecting Steam Library & Installed Games ===");
        var games = _gameDetector.DetectAllGames().ToList();
        result.DetectedGamesCount = games.Count;

        string? steamDir = _steamLocator.FindSteamInstallDirectory(options.SteamPath);
        if (string.IsNullOrEmpty(steamDir))
        {
            string err = "Could not locate Steam installation directory. Please specify it using --steam-path <path>.";
            _logger.LogError(err);
            result.ErrorMessage = err;
            result.Success = false;
            return result;
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

        var allShortcuts = new List<SteamShortcut>();
        foreach (var profileDir in profiles)
        {
            string shortcutsPath = Path.Combine(profileDir, "config", "shortcuts.vdf");
            if (File.Exists(shortcutsPath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(shortcutsPath);
                    allShortcuts.AddRange(VdfBinaryReader.ReadShortcuts(data));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not read 'shortcuts.vdf' in '{profileDir}': {ex.Message}");
                }
            }
        }

        // Also discover any direct-exe shortcuts in shortcuts.vdf belonging to known launchers
        foreach (var s in allShortcuts)
        {
            if (s.Exe.Contains("NonSteamLauncher", StringComparison.OrdinalIgnoreCase))
                continue;

            string cleanExe = s.Exe.Trim('"', '\'');
            string? detectedLauncher = DetectLauncherFromPath(cleanExe, s.StartDir);
            if (detectedLauncher != null)
            {
                string normShortcutName = GameDetector.NormalizeTitle(s.AppName);
                bool alreadyInList = games.Any(g =>
                    g.Title.Equals(s.AppName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(normShortcutName) && GameDetector.NormalizeTitle(g.Title).Equals(normShortcutName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(g.ExecutablePath) && g.ExecutablePath.Equals(cleanExe, StringComparison.OrdinalIgnoreCase)));

                if (!alreadyInList)
                {
                    games.Add(new InstalledGame
                    {
                        Title = s.AppName,
                        Launcher = detectedLauncher,
                        LaunchUri = cleanExe,
                        ExecutablePath = cleanExe,
                        IconPath = !string.IsNullOrEmpty(s.Icon) ? s.Icon : (File.Exists(cleanExe) ? cleanExe : null)
                    });
                }
            }
        }

        result.DetectedGamesCount = games.Count;

        var matchedShortcuts = new HashSet<SteamShortcut>();
        foreach (var game in games)
        {
            var matchingShortcut = FindMatchingShortcut(allShortcuts, game);
            if (matchingShortcut != null)
            {
                matchedShortcuts.Add(matchingShortcut);
                if (matchingShortcut.AppId != 0 && (game.AppId == null || game.AppId == 0))
                {
                    game.AppId = matchingShortcut.AppId;
                }
            }

            if (matchingShortcut != null && matchingShortcut.Exe.Contains("NonSteamLauncher", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(matchingShortcut.Icon) && File.Exists(matchingShortcut.Icon))
                {
                    game.IconPath = matchingShortcut.Icon;
                }
                result.ExistingGames.Add(game);
                result.ExistingShortcutsCount++;
            }
            else
            {
                result.PendingGames.Add(game);
            }
        }

        // Also discover any manual / external shortcuts or games already in shortcuts.vdf
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in result.ExistingGames)
        {
            if (!string.IsNullOrWhiteSpace(g.Title))
            {
                seenTitles.Add(g.Title);
                string norm = GameDetector.NormalizeTitle(g.Title);
                if (!string.IsNullOrEmpty(norm)) seenTitles.Add(norm);
            }
        }
        foreach (var g in result.PendingGames)
        {
            if (!string.IsNullOrWhiteSpace(g.Title))
            {
                seenTitles.Add(g.Title);
                string norm = GameDetector.NormalizeTitle(g.Title);
                if (!string.IsNullOrEmpty(norm)) seenTitles.Add(norm);
            }
        }

        foreach (var s in allShortcuts)
        {
            if (matchedShortcuts.Contains(s))
                continue;

            string cleanTitle = s.AppName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(cleanTitle))
                continue;

            string normTitle = GameDetector.NormalizeTitle(cleanTitle);
            if (seenTitles.Contains(cleanTitle) || (!string.IsNullOrEmpty(normTitle) && seenTitles.Contains(normTitle)))
                continue;

            seenTitles.Add(cleanTitle);
            if (!string.IsNullOrEmpty(normTitle))
                seenTitles.Add(normTitle);

            string cleanExe = s.Exe.Trim('"', '\'');
            string launcherTag = (s.Tags != null && s.Tags.Count > 0 && !string.IsNullOrWhiteSpace(s.Tags[0]))
                ? s.Tags[0]
                : "Manual";

            uint appId = s.AppId;
            if (appId == 0 && !string.IsNullOrEmpty(s.Exe) && !string.IsNullOrEmpty(s.AppName))
            {
                appId = SteamCrc32.GenerateAppId(s.Exe, s.AppName);
            }

            var manualGame = new InstalledGame
            {
                Title = cleanTitle,
                Launcher = launcherTag,
                LaunchUri = cleanExe,
                ExecutablePath = cleanExe,
                IconPath = !string.IsNullOrEmpty(s.Icon) ? s.Icon : (File.Exists(cleanExe) ? cleanExe : null),
                AppId = appId != 0 ? appId : null,
                IsManualShortcut = true
            };

            result.ExistingGames.Add(manualGame);
            result.ExistingShortcutsCount++;
        }

        result.Success = true;
        _logger.LogInfo($"Inspection complete: {result.ExistingGames.Count} synced, {result.PendingGames.Count} available to add.");
        return result;
    }

    /// <summary>
    /// Downloads missing SteamGridDB artwork and icons for all already-synced NonSteamLauncher shortcuts.
    /// </summary>
    public SteamSyncResult SyncArtworkForExistingGames(Options options)
    {
        var settings = AppSettings.Load(options.SettingsFilePath);
        if (!settings.DownloadArtwork || options.SkipArtwork)
        {
            return Inspect(options);
        }

        string? steamDir = _steamLocator.FindSteamInstallDirectory(options.SteamPath);
        if (string.IsNullOrEmpty(steamDir))
            return Inspect(options);

        var profiles = _steamLocator.FindUserProfiles(steamDir);
        var games = _gameDetector.DetectAllGames().ToList();

        foreach (var profileDir in profiles)
        {
            string configDir = Path.Combine(profileDir, "config");
            string gridDir = Path.Combine(configDir, "grid");
            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            if (!File.Exists(shortcutsPath))
                continue;

            List<SteamShortcut> shortcuts;
            try
            {
                shortcuts = VdfBinaryReader.ReadShortcuts(File.ReadAllBytes(shortcutsPath));
            }
            catch
            {
                continue;
            }

            bool iconsModified = false;
            foreach (var s in shortcuts)
            {
                if (!s.Exe.Contains("NonSteamLauncher", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (s.AppId == 0)
                {
                    s.AppId = SteamCrc32.GenerateAppId(s.Exe, s.AppName);
                }

                var matchedGame = games.FirstOrDefault(g =>
                    g.Title.Equals(s.AppName, StringComparison.OrdinalIgnoreCase) ||
                    GameDetector.NormalizeTitle(g.Title).Equals(GameDetector.NormalizeTitle(s.AppName), StringComparison.OrdinalIgnoreCase));

                var artRes = _artworkService.EnsureArtworkForShortcut(
                    gridDir,
                    s,
                    matchedGame,
                    settings.SteamGridDbApiKey,
                    options.DryRun);

                if (artRes.IconsModifiedInShortcuts)
                {
                    iconsModified = true;
                }
            }

            if (iconsModified && !options.DryRun)
            {
                try
                {
                    File.WriteAllBytes(shortcutsPath, VdfBinaryWriter.WriteShortcuts(shortcuts));
                }
                catch { }
            }
        }

        return Inspect(options);
    }

    /// <summary>
    /// Fetches up to <paramref name="maxPerPiece"/> top-ranked artwork options for each piece of each specified game.
    /// </summary>
    public List<GameArtworkOptions> FetchArtworkOptionsForGames(
        Options options,
        IReadOnlyList<InstalledGame> games,
        int maxPerPiece = 4,
        bool onlyMissing = false)
    {
        var results = new List<GameArtworkOptions>();
        var settings = ResolveSettings(options);
        if (!settings.DownloadArtwork || options.SkipArtwork || games.Count == 0)
            return results;

        string? steamDir = _steamLocator.FindSteamInstallDirectory(options.SteamPath);
        string? firstGridDir = null;
        var shortcuts = new List<SteamShortcut>();

        if (!string.IsNullOrEmpty(steamDir))
        {
            var profiles = _steamLocator.FindUserProfiles(steamDir);
            if (profiles.Count > 0)
            {
                string configDir = Path.Combine(profiles[0], "config");
                firstGridDir = Path.Combine(configDir, "grid");
                string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
                if (File.Exists(shortcutsPath))
                {
                    try
                    {
                        shortcuts = VdfBinaryReader.ReadShortcuts(File.ReadAllBytes(shortcutsPath));
                    }
                    catch { }
                }
            }
        }

        foreach (var game in games)
        {
            uint appId = game.AppId ?? 0;
            if (appId == 0 && shortcuts.Count > 0)
            {
                var match = FindMatchingShortcut(shortcuts, game);
                if (match != null)
                {
                    appId = match.AppId != 0
                        ? match.AppId
                        : SteamCrc32.GenerateAppId(match.Exe, match.AppName);
                    if (appId != 0)
                        game.AppId = appId;
                }
            }

            var gameOptions = _artworkService.FetchArtworkOptionsForGame(
                game,
                appId,
                settings.SteamGridDbApiKey,
                firstGridDir,
                maxPerPiece,
                onlyMissing);

            results.Add(gameOptions);
        }

        return results;
    }

    /// <summary>
    /// Downloads and applies user-selected artwork pieces across all Steam user profiles.
    /// </summary>
    public SteamSyncResult ApplyArtworkSelections(
        Options options,
        IReadOnlyList<GameArtworkSelection> selections)
    {
        if (selections.Count == 0)
            return Inspect(options);

        string? steamDir = _steamLocator.FindSteamInstallDirectory(options.SteamPath);
        if (string.IsNullOrEmpty(steamDir))
            return Inspect(options);

        var profiles = _steamLocator.FindUserProfiles(steamDir);
        int totalImages = 0;
        int totalGames = 0;

        foreach (var profileDir in profiles)
        {
            string configDir = Path.Combine(profileDir, "config");
            string gridDir = Path.Combine(configDir, "grid");
            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            if (!File.Exists(shortcutsPath))
                continue;

            List<SteamShortcut> shortcuts;
            try
            {
                shortcuts = VdfBinaryReader.ReadShortcuts(File.ReadAllBytes(shortcutsPath));
            }
            catch
            {
                continue;
            }

            bool iconsModified = false;

            foreach (var selection in selections)
            {
                var matchingShortcut = FindMatchingShortcut(shortcuts, selection.Game);
                if (matchingShortcut == null && selection.AppId != 0)
                {
                    matchingShortcut = shortcuts.FirstOrDefault(s => s.AppId == selection.AppId);
                }
                if (matchingShortcut == null)
                    continue;

                if (matchingShortcut.AppId == 0)
                {
                    matchingShortcut.AppId = SteamCrc32.GenerateAppId(matchingShortcut.Exe, matchingShortcut.AppName);
                }

                var artRes = _artworkService.ApplySelectedArtworkForShortcut(
                    gridDir,
                    matchingShortcut,
                    selection.Game,
                    selection,
                    options.DryRun);

                totalImages += artRes.ImagesDownloadedCount;
                totalGames += artRes.GamesUpdatedCount;
                if (artRes.IconsModifiedInShortcuts)
                {
                    iconsModified = true;
                }
            }

            if (iconsModified && !options.DryRun)
            {
                try
                {
                    File.WriteAllBytes(shortcutsPath, VdfBinaryWriter.WriteShortcuts(shortcuts));
                }
                catch { }
            }
        }

        var updated = Inspect(options);
        updated.ArtworkImagesDownloadedCount = totalImages;
        updated.ArtworkGamesUpdatedCount = totalGames;
        return updated;
    }

    /// <summary>
    /// Synchronizes only the user-selected games into Steam's shortcuts.vdf.
    /// </summary>
    public SteamSyncResult SyncSelectedGames(Options options, IReadOnlyList<InstalledGame> selectedGames, bool deferArtwork = false)
    {
        var settings = ResolveSettings(options);
        bool downloadArtwork = settings.DownloadArtwork && !options.SkipArtwork && !deferArtwork;

        var fullInspection = Inspect(options);
        if (!fullInspection.Success)
            return fullInspection;

        if (selectedGames.Count == 0)
            return fullInspection;

        string? steamDir = _steamLocator.FindSteamInstallDirectory(options.SteamPath);
        if (string.IsNullOrEmpty(steamDir))
            return fullInspection;

        var profiles = _steamLocator.FindUserProfiles(steamDir);
        string rawExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "NonSteamLauncher.exe";
        string exePath = $"\"{rawExe}\"";
        string startDir = $"\"{Path.GetDirectoryName(rawExe) ?? ""}\"";

        var addedGames = new List<InstalledGame>();
        int totalAddedShortcuts = 0;
        int updatedProfiles = 0;
        int artworkImagesDownloaded = 0;
        int artworkGamesUpdated = 0;

        foreach (var profileDir in profiles)
        {
            string profileId = Path.GetFileName(profileDir);
            string configDir = Path.Combine(profileDir, "config");
            string gridDir = Path.Combine(configDir, "grid");
            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            string backupPath = Path.Combine(configDir, "shortcuts.vdf.bak");

            List<SteamShortcut> shortcuts;
            if (File.Exists(shortcutsPath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(shortcutsPath);
                    shortcuts = VdfBinaryReader.ReadShortcuts(data);
                }
                catch
                {
                    shortcuts = new List<SteamShortcut>();
                }
            }
            else
            {
                shortcuts = new List<SteamShortcut>();
            }

            int profileAdded = 0;
            bool iconsModified = false;

            foreach (var game in selectedGames)
            {
                var existingShortcut = FindMatchingShortcut(shortcuts, game);
                SteamShortcut targetShortcut;

                if (existingShortcut != null)
                {
                    if (existingShortcut.Exe.Contains("NonSteamLauncher", StringComparison.OrdinalIgnoreCase))
                    {
                        targetShortcut = existingShortcut;
                        string expectedOptions = BuildLaunchOptions(game.LaunchUri);
                        if (!string.Equals(existingShortcut.LaunchOptions, expectedOptions, StringComparison.OrdinalIgnoreCase))
                        {
                            existingShortcut.LaunchOptions = expectedOptions;
                            profileAdded++;
                            _logger.LogInfo($"  ⚡ Updated LaunchOptions for [{game.Launcher}] {game.Title} -> {game.LaunchUri}");
                        }
                        if ((string.IsNullOrEmpty(existingShortcut.Icon) || existingShortcut.Icon.Contains("prelauncher", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(game.IconPath))
                        {
                            existingShortcut.Icon = game.IconPath;
                            iconsModified = true;
                        }
                        if (existingShortcut.AppId == 0)
                        {
                            existingShortcut.AppId = SteamCrc32.GenerateAppId(exePath, existingShortcut.AppName);
                        }
                    }
                    else
                    {
                        existingShortcut.Exe = exePath;
                        existingShortcut.StartDir = startDir;
                        existingShortcut.LaunchOptions = BuildLaunchOptions(game.LaunchUri);
                        if (string.IsNullOrEmpty(existingShortcut.Icon) && !string.IsNullOrEmpty(game.IconPath))
                        {
                            existingShortcut.Icon = game.IconPath;
                        }
                        if (!existingShortcut.Tags.Contains(game.Launcher))
                        {
                            existingShortcut.Tags.Add(game.Launcher);
                        }
                        if (existingShortcut.AppId == 0)
                        {
                            existingShortcut.AppId = SteamCrc32.GenerateAppId(exePath, existingShortcut.AppName);
                        }

                        targetShortcut = existingShortcut;
                        profileAdded++;
                        if (!addedGames.Any(g => g.Title.Equals(game.Title, StringComparison.OrdinalIgnoreCase)))
                        {
                            addedGames.Add(game);
                        }
                        _logger.LogInfo($"  ✔ Upgraded [{game.Launcher}] {game.Title} to NonSteamLauncher wrapper!");
                    }
                }
                else
                {
                    var newShortcut = new SteamShortcut
                    {
                        AppName = game.Title,
                        Exe = exePath,
                        StartDir = startDir,
                        LaunchOptions = BuildLaunchOptions(game.LaunchUri),
                        Icon = game.IconPath ?? "",
                        Tags = new List<string> { game.Launcher },
                        AllowDesktopConfig = 1,
                        AllowOverlay = 1,
                        AppId = SteamCrc32.GenerateAppId(exePath, game.Title)
                    };

                    shortcuts.Add(newShortcut);
                    targetShortcut = newShortcut;
                    profileAdded++;
                    if (!addedGames.Any(g => g.Title.Equals(game.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        addedGames.Add(game);
                    }
                    _logger.LogInfo($"  + Added [{game.Launcher}] {game.Title} (AppID: {newShortcut.AppId})");
                }

                if (downloadArtwork)
                {
                    var artRes = _artworkService.EnsureArtworkForShortcut(
                        gridDir,
                        targetShortcut,
                        game,
                        settings.SteamGridDbApiKey,
                        options.DryRun);
                    artworkImagesDownloaded += artRes.ImagesDownloadedCount;
                    artworkGamesUpdated += artRes.GamesUpdatedCount;
                    if (artRes.IconsModifiedInShortcuts)
                    {
                        iconsModified = true;
                    }
                }
            }

            totalAddedShortcuts += profileAdded;

            if ((profileAdded > 0 || iconsModified) && !options.DryRun)
            {
                try
                {
                    Directory.CreateDirectory(configDir);
                    if (File.Exists(shortcutsPath))
                    {
                        File.Copy(shortcutsPath, backupPath, overwrite: true);
                    }

                    byte[] outputData = VdfBinaryWriter.WriteShortcuts(shortcuts);
                    File.WriteAllBytes(shortcutsPath, outputData);
                    _logger.LogInfo($"  Saved {shortcuts.Count} shortcut(s) to '{shortcutsPath}'.");
                    updatedProfiles++;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"  Error writing 'shortcuts.vdf' for profile {profileId}: {ex.Message}");
                }
            }
        }

        // Rebuild updated status
        var updatedResult = Inspect(options);
        updatedResult.AddedGames = addedGames;
        updatedResult.AddedShortcutsCount = totalAddedShortcuts;
        updatedResult.UpdatedProfilesCount = updatedProfiles;
        updatedResult.ArtworkImagesDownloadedCount = artworkImagesDownloaded;
        updatedResult.ArtworkGamesUpdatedCount = artworkGamesUpdated;
        return updatedResult;
    }

    public SteamSyncResult Sync(Options options)
    {
        var result = new SteamSyncResult();
        var settings = ResolveSettings(options);
        bool downloadArtwork = settings.DownloadArtwork && !options.SkipArtwork;

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
            string gridDir = Path.Combine(configDir, "grid");
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
            bool iconsModified = false;

            foreach (var game in games)
            {
                var existingShortcut = FindMatchingShortcut(shortcuts, game);

                if (existingShortcut != null)
                {
                    // Check if existing shortcut in Steam is already using NonSteamLauncher
                    if (existingShortcut.Exe.Contains("NonSteamLauncher", StringComparison.OrdinalIgnoreCase))
                    {
                        if (existingShortcut.AppId == 0)
                        {
                            existingShortcut.AppId = SteamCrc32.GenerateAppId(existingShortcut.Exe, existingShortcut.AppName);
                        }

                        string expectedOptions = BuildLaunchOptions(game.LaunchUri);
                        if (!string.Equals(existingShortcut.LaunchOptions, expectedOptions, StringComparison.OrdinalIgnoreCase))
                        {
                            existingShortcut.LaunchOptions = expectedOptions;
                            iconsModified = true;
                            _logger.LogInfo($"  ⚡ Updated LaunchOptions for [{game.Launcher}] {game.Title} -> {game.LaunchUri}");
                        }

                        if ((string.IsNullOrEmpty(existingShortcut.Icon) || existingShortcut.Icon.Contains("prelauncher", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(game.IconPath))
                        {
                            existingShortcut.Icon = game.IconPath;
                            iconsModified = true;
                        }

                        if (downloadArtwork)
                        {
                            var artRes = _artworkService.EnsureArtworkForShortcut(
                                gridDir,
                                existingShortcut,
                                game,
                                settings.SteamGridDbApiKey,
                                options.DryRun);
                            result.ArtworkImagesDownloadedCount += artRes.ImagesDownloadedCount;
                            result.ArtworkGamesUpdatedCount += artRes.GamesUpdatedCount;
                            if (artRes.IconsModifiedInShortcuts)
                            {
                                iconsModified = true;
                            }
                        }

                        if (!string.IsNullOrEmpty(existingShortcut.Icon) && File.Exists(existingShortcut.Icon))
                        {
                            game.IconPath = existingShortcut.Icon;
                        }

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
                    existingShortcut.LaunchOptions = BuildLaunchOptions(game.LaunchUri);
                    if (string.IsNullOrEmpty(existingShortcut.Icon) && !string.IsNullOrEmpty(game.IconPath))
                    {
                        existingShortcut.Icon = game.IconPath;
                    }
                    if (!existingShortcut.Tags.Contains(game.Launcher))
                    {
                        existingShortcut.Tags.Add(game.Launcher);
                    }
                    if (existingShortcut.AppId == 0)
                    {
                        existingShortcut.AppId = SteamCrc32.GenerateAppId(exePath, existingShortcut.AppName);
                    }

                    if (downloadArtwork)
                    {
                        var artRes = _artworkService.EnsureArtworkForShortcut(
                            gridDir,
                            existingShortcut,
                            game,
                            settings.SteamGridDbApiKey,
                            options.DryRun);
                        result.ArtworkImagesDownloadedCount += artRes.ImagesDownloadedCount;
                        result.ArtworkGamesUpdatedCount += artRes.GamesUpdatedCount;
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
                string launchOptions = BuildLaunchOptions(game.LaunchUri);

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

                if (downloadArtwork)
                {
                    var artRes = _artworkService.EnsureArtworkForShortcut(
                        gridDir,
                        newShortcut,
                        game,
                        settings.SteamGridDbApiKey,
                        options.DryRun);
                    result.ArtworkImagesDownloadedCount += artRes.ImagesDownloadedCount;
                    result.ArtworkGamesUpdatedCount += artRes.GamesUpdatedCount;
                }

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
                    s.LaunchOptions = BuildLaunchOptions(cleanExe);
                    if (string.IsNullOrEmpty(s.Icon) && File.Exists(cleanExe))
                    {
                        s.Icon = cleanExe;
                    }
                    if (!s.Tags.Contains(detectedLauncher))
                    {
                        s.Tags.Add(detectedLauncher);
                    }
                    if (s.AppId == 0)
                    {
                        s.AppId = SteamCrc32.GenerateAppId(exePath, s.AppName);
                    }

                    var adoptedGame = new InstalledGame
                    {
                        Title = s.AppName,
                        Launcher = detectedLauncher,
                        LaunchUri = cleanExe,
                        ExecutablePath = cleanExe,
                        IconPath = s.Icon
                    };

                    if (downloadArtwork)
                    {
                        var artRes = _artworkService.EnsureArtworkForShortcut(
                            gridDir,
                            s,
                            adoptedGame,
                            settings.SteamGridDbApiKey,
                            options.DryRun);
                        result.ArtworkImagesDownloadedCount += artRes.ImagesDownloadedCount;
                        result.ArtworkGamesUpdatedCount += artRes.GamesUpdatedCount;
                    }

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

            if (profileAdded > 0 || iconsModified)
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

    /// <summary>
    /// Removes a specific Non-Steam game shortcut from Steam's shortcuts.vdf and cleans up its grid artwork.
    /// </summary>
    public SteamSyncResult RemoveShortcut(Options options, InstalledGame gameToRemove)
    {
        return RemoveShortcuts(options, new[] { gameToRemove });
    }

    /// <summary>
    /// Removes a list of Non-Steam game shortcuts from Steam's shortcuts.vdf and cleans up their grid artwork.
    /// </summary>
    public SteamSyncResult RemoveShortcuts(Options options, IReadOnlyList<InstalledGame> gamesToRemove)
    {
        ResolveSettings(options);
        var result = new SteamSyncResult();

        if (gamesToRemove == null || gamesToRemove.Count == 0)
        {
            return Inspect(options);
        }

        _logger.LogInfo($"=== Removing {gamesToRemove.Count} Non-Steam Shortcut(s) from Steam ===");
        foreach (var g in gamesToRemove)
        {
            _logger.LogInfo($"  - [{g.Launcher}] {g.Title} (AppID: {g.AppId})");
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

        if (_steamLocator.IsSteamRunning())
        {
            _logger.LogWarning("----------------------------------------------------------------------");
            _logger.LogWarning("⚠️  WARNING: Steam is currently running!");
            _logger.LogWarning("   Please exit Steam so your removed shortcuts are not overwritten by Steam upon exit.");
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

        int totalRemoved = 0;
        int updatedProfiles = 0;
        var removedGames = new List<InstalledGame>();

        foreach (var profileDir in profiles)
        {
            string profileId = Path.GetFileName(profileDir);
            string configDir = Path.Combine(profileDir, "config");
            string gridDir = Path.Combine(configDir, "grid");
            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            string backupPath = Path.Combine(configDir, "shortcuts.vdf.bak");

            if (!File.Exists(shortcutsPath))
                continue;

            List<SteamShortcut> shortcuts;
            try
            {
                byte[] data = File.ReadAllBytes(shortcutsPath);
                shortcuts = VdfBinaryReader.ReadShortcuts(data);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not read 'shortcuts.vdf' in profile {profileId}: {ex.Message}");
                continue;
            }

            int profileRemoved = 0;
            var appIdsToRemoveArtwork = new List<uint>();

            for (int i = shortcuts.Count - 1; i >= 0; i--)
            {
                var s = shortcuts[i];
                var match = gamesToRemove.FirstOrDefault(g => IsShortcutMatch(s, g));
                if (match != null)
                {
                    uint id = s.AppId;
                    if (id == 0 && !string.IsNullOrEmpty(s.Exe) && !string.IsNullOrEmpty(s.AppName))
                    {
                        id = SteamCrc32.GenerateAppId(s.Exe, s.AppName);
                    }
                    if (id != 0)
                    {
                        appIdsToRemoveArtwork.Add(id);
                    }

                    shortcuts.RemoveAt(i);
                    profileRemoved++;
                    if (!removedGames.Any(rg => rg.Title.Equals(match.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        removedGames.Add(match);
                    }
                    _logger.LogInfo($"  ✔ Removed shortcut '{s.AppName}' from profile {profileId}");
                }
            }

            if (profileRemoved > 0)
            {
                totalRemoved += profileRemoved;
                if (!options.DryRun)
                {
                    try
                    {
                        Directory.CreateDirectory(configDir);
                        if (File.Exists(shortcutsPath))
                        {
                            File.Copy(shortcutsPath, backupPath, overwrite: true);
                        }

                        byte[] outputData = VdfBinaryWriter.WriteShortcuts(shortcuts);
                        File.WriteAllBytes(shortcutsPath, outputData);
                        _logger.LogInfo($"  Saved {shortcuts.Count} remaining shortcut(s) to '{shortcutsPath}'.");
                        updatedProfiles++;

                        // Clean up grid artwork
                        if (Directory.Exists(gridDir))
                        {
                            foreach (var appId in appIdsToRemoveArtwork)
                            {
                                RemoveArtworkForAppId(gridDir, appId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"  Error writing 'shortcuts.vdf' for profile {profileId}: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogInfo($"  [DRY RUN] Would remove {profileRemoved} shortcut(s) from '{shortcutsPath}'.");
                }
            }
        }

        var inspectResult = Inspect(options);
        inspectResult.RemovedShortcutsCount = totalRemoved;
        inspectResult.RemovedGames = removedGames;
        inspectResult.UpdatedProfilesCount = updatedProfiles;
        return inspectResult;
    }

    private static bool IsShortcutMatch(SteamShortcut s, InstalledGame g)
    {
        if (g.AppId.HasValue && g.AppId.Value != 0 && s.AppId != 0 && s.AppId == g.AppId.Value)
            return true;

        if (s.AppName.Equals(g.Title, StringComparison.OrdinalIgnoreCase))
            return true;

        string normS = GameDetector.NormalizeTitle(s.AppName);
        string normG = GameDetector.NormalizeTitle(g.Title);
        if (!string.IsNullOrEmpty(normS) && normS.Equals(normG, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(g.LaunchUri) && s.LaunchOptions.Contains(g.LaunchUri, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(g.ExecutablePath) && s.Exe.Trim('"', '\'').Equals(g.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void RemoveArtworkForAppId(string gridDir, uint appId)
    {
        if (appId == 0) return;
        try
        {
            ulong id64 = ((ulong)appId << 32) | 0x02000000;
            string[] prefixes = { appId.ToString(), id64.ToString() };

            foreach (var prefix in prefixes)
            {
                var files = Directory.GetFiles(gridDir, $"{prefix}*");
                foreach (var f in files)
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (name.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                        name.Equals($"{prefix}p", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals($"{prefix}_hero", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals($"{prefix}_logo", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals($"{prefix}_icon", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(f);
                            _logger.LogDebug($"  Deleted artwork file: {Path.GetFileName(f)}");
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    private static SteamShortcut? FindMatchingShortcut(IEnumerable<SteamShortcut> shortcuts, InstalledGame game)
    {
        string normGameTitle = GameDetector.NormalizeTitle(game.Title);
        return shortcuts.FirstOrDefault(s =>
            s.AppName.Equals(game.Title, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(normGameTitle) && GameDetector.NormalizeTitle(s.AppName).Equals(normGameTitle, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(game.LaunchUri) && s.LaunchOptions.Contains(game.LaunchUri, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(game.ExecutablePath) && s.Exe.Trim('"', '\'').Equals(game.ExecutablePath, StringComparison.OrdinalIgnoreCase))
        );
    }

    public static string? DetectLauncherFromPath(string exePath, string? startDir)
    {
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            string fileName = Path.GetFileName(exePath.Trim('"', '\''));
            if (IsLauncherClientBinary(fileName))
                return null;
        }

        string combined = $"{exePath} {startDir}";

        if (combined.Contains("EA Games", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Origin Games", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase))
        {
            return "EA App";
        }

        // Check if directory or parent contains __Installer\installerdata.xml (EA App game installed in custom folder)
        if (HasEAInstallerManifest(exePath) || HasEAInstallerManifest(startDir))
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

    private static bool IsLauncherClientBinary(string fileName)
    {
        return fileName.Equals("EADesktop.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("EALauncher.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("EAUninstall.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Link2EA.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("ErrorReporter.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Origin.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("OriginLegacyCLI.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("EpicGamesLauncher.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("UbisoftConnect.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Uplay.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("GalaxyClient.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Battle.net.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasEAInstallerManifest(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            string clean = path.Trim('"', '\'');
            string? dir = File.Exists(clean) ? Path.GetDirectoryName(clean) : clean;
            for (int depth = 0; depth < 3 && !string.IsNullOrEmpty(dir); depth++)
            {
                if (File.Exists(Path.Combine(dir, "__Installer", "installerdata.xml")))
                    return true;
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch { }

        return false;
    }

    public static string BuildLaunchOptions(string launchUri)
    {
        bool usesStoreLauncher = DefaultProcessProvider.GetLauncherProcessNames(launchUri).Length > 0;
        if (usesStoreLauncher)
        {
            return $"--launch \"{launchUri}\" --close-launcher --disable-overlay";
        }

        return $"--launch \"{launchUri}\"";
    }
}
