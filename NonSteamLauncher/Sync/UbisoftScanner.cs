namespace NonSteamLauncher.Sync;

public class UbisoftScanner : IScanner
{
    private readonly IRegistryReader _registry;
    private readonly Logger? _logger;

    public string ScannerName => "Ubisoft Connect";

    public UbisoftScanner(IRegistryReader? registry = null, Logger? logger = null)
    {
        _registry = registry ?? new WindowsRegistryReader();
        _logger = logger;
    }

    public IReadOnlyList<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();

        var registryPaths = new[]
        {
            @"HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs",
            @"HKLM\SOFTWARE\Ubisoft\Launcher\Installs"
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
                string? installDir = _registry.GetValue(fullKey, "InstallDir");

                if (string.IsNullOrWhiteSpace(installDir))
                    continue;

                installDir = installDir.Trim('"', '\'').TrimEnd('\\', '/');

                string title = Path.GetFileName(installDir);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Ubisoft Game {gameId}";
                }

                string uri = $"uplay://launch/{gameId}/0";

                games.Add(new InstalledGame
                {
                    Title = title,
                    Launcher = "Ubisoft Connect",
                    LaunchUri = uri,
                    InstallDirectory = installDir,
                    IconPath = Directory.Exists(installDir) ? FindGameExecutableOrIcon(installDir) : null
                });

                _logger?.LogDebug($"Found Ubisoft game: {title} (ID: {gameId})");
            }
        }

        return games;
    }

    private static string? FindGameExecutableOrIcon(string dir)
    {
        try
        {
            var exes = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);
            var best = exes.FirstOrDefault(e => !e.Contains("crash", StringComparison.OrdinalIgnoreCase) &&
                                                !e.Contains("report", StringComparison.OrdinalIgnoreCase) &&
                                                !e.Contains("unity", StringComparison.OrdinalIgnoreCase) &&
                                                !e.Contains("unins", StringComparison.OrdinalIgnoreCase));
            return best;
        }
        catch
        {
            return null;
        }
    }
}
