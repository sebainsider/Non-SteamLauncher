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
                string? gameName = _registry.GetValue(fullKey, "gameName");
                string? exe = _registry.GetValue(fullKey, "exe");
                string? path = _registry.GetValue(fullKey, "path");
                string? icon = _registry.GetValue(fullKey, "icon");

                if (string.IsNullOrWhiteSpace(gameName))
                    continue;

                string? fullExe = null;
                if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(exe))
                {
                    fullExe = Path.Combine(path.Trim('"', '\''), exe.Trim('"', '\''));
                }

                // GOG games can launch directly via executable (DRM-Free) or via goggalaxy:// URI
                string launchUri = fullExe ?? $"goggalaxy://openGameView/{gameId}";

                games.Add(new InstalledGame
                {
                    Title = gameName,
                    Launcher = "GOG Galaxy",
                    LaunchUri = launchUri,
                    InstallDirectory = path,
                    ExecutablePath = fullExe,
                    IconPath = (icon != null && File.Exists(icon)) ? icon : fullExe
                });

                _logger?.LogDebug($"Found GOG game: {gameName} (ID: {gameId})");
            }
        }

        return games;
    }
}
