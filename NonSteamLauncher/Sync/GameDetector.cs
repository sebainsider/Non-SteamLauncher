using System.Text.RegularExpressions;

namespace NonSteamLauncher.Sync;

public class GameDetector
{
    private readonly List<IScanner> _scanners;
    private readonly Logger? _logger;

    public GameDetector(IEnumerable<IScanner>? scanners = null, Logger? logger = null)
    {
        _logger = logger;
        if (scanners != null)
        {
            _scanners = scanners.ToList();
        }
        else
        {
            _scanners = new List<IScanner>
            {
                new EpicGamesScanner(logger: _logger),
                new UbisoftScanner(logger: _logger),
                new GogScanner(logger: _logger),
                new DesktopShortcutScanner(logger: _logger)
            };
        }
    }

    public IReadOnlyList<InstalledGame> DetectAllGames()
    {
        var rawGames = new List<InstalledGame>();

        foreach (var scanner in _scanners)
        {
            try
            {
                _logger?.LogDebug($"Running scanner: {scanner.ScannerName}...");
                var games = scanner.Scan();
                _logger?.LogDebug($"Scanner '{scanner.ScannerName}' found {games.Count} game(s).");
                rawGames.AddRange(games);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error running scanner '{scanner.ScannerName}': {ex.Message}");
            }
        }

        return DeduplicateGames(rawGames);
    }

    public static IReadOnlyList<InstalledGame> DeduplicateGames(IEnumerable<InstalledGame> games)
    {
        var map = new Dictionary<string, InstalledGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in games)
        {
            string key = NormalizeTitle(game.Title);
            if (string.IsNullOrEmpty(key))
            {
                key = game.LaunchUri;
            }

            if (map.TryGetValue(key, out var existing))
            {
                // Prefer the entry that has executable or icon path
                if (string.IsNullOrEmpty(existing.IconPath) && !string.IsNullOrEmpty(game.IconPath))
                {
                    existing.IconPath = game.IconPath;
                }
                if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(game.ExecutablePath))
                {
                    existing.ExecutablePath = game.ExecutablePath;
                }
                if (string.IsNullOrEmpty(existing.InstallDirectory) && !string.IsNullOrEmpty(game.InstallDirectory))
                {
                    existing.InstallDirectory = game.InstallDirectory;
                }
            }
            else
            {
                map[key] = game;
            }
        }

        return map.Values
            .OrderBy(g => g.Launcher)
            .ThenBy(g => g.Title)
            .ToList();
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Strip non-alphanumeric characters for title matching
        string clean = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9]", "");
        return clean;
    }
}
