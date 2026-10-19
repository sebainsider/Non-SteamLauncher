using System.Text.Json;

namespace NonSteamLauncher.Sync;

public class EpicGamesScanner : IScanner
{
    private readonly string? _customManifestsPath;
    private readonly Logger? _logger;

    public string ScannerName => "Epic Games Store";

    public EpicGamesScanner(string? customManifestsPath = null, Logger? logger = null)
    {
        _customManifestsPath = customManifestsPath;
        _logger = logger;
    }

    public IReadOnlyList<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();

        string manifestsDir = _customManifestsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests"
        );

        if (!Directory.Exists(manifestsDir))
        {
            _logger?.LogDebug($"Epic Games manifests directory not found: '{manifestsDir}'");
            return games;
        }

        try
        {
            var files = Directory.GetFiles(manifestsDir, "*.item", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var game = ParseManifestFile(file);
                    if (game != null)
                    {
                        games.Add(game);
                        _logger?.LogDebug($"Found Epic game: {game.Title} ({game.LaunchUri})");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Error parsing Epic manifest '{file}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error reading Epic Games manifests from '{manifestsDir}': {ex.Message}");
        }

        return games;
    }

    public static InstalledGame? ParseManifestFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return ParseManifestJson(json);
    }

    public static InstalledGame? ParseManifestJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Skip Unreal Engine editors or prerequisites
        if (root.TryGetProperty("bIsEngineInstalled", out var isEngine) && isEngine.GetBoolean())
        {
            return null;
        }

        string displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() ?? "" : "";
        string appName = root.TryGetProperty("AppName", out var an) ? an.GetString() ?? "" : "";
        string catalogNamespace = root.TryGetProperty("CatalogNamespace", out var cn) ? cn.GetString() ?? "" : "";
        string catalogItemId = root.TryGetProperty("CatalogItemId", out var ci) ? ci.GetString() ?? "" : "";
        string installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";
        string launchExecutable = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        if (appName.StartsWith("UE_", StringComparison.OrdinalIgnoreCase) ||
            appName.StartsWith("UnrealEngine", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string uri;
        if (!string.IsNullOrEmpty(catalogNamespace) && !string.IsNullOrEmpty(catalogItemId))
        {
            uri = $"com.epicgames.launcher://apps/{catalogNamespace}%3A{catalogItemId}%3A{appName}?action=launch&silent=true";
        }
        else
        {
            uri = $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true";
        }

        string? exePath = null;
        if (!string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(launchExecutable))
        {
            exePath = Path.Combine(installLocation, launchExecutable);
        }

        return new InstalledGame
        {
            Title = displayName,
            Launcher = "Epic Games",
            LaunchUri = uri,
            InstallDirectory = string.IsNullOrEmpty(installLocation) ? null : installLocation,
            ExecutablePath = exePath,
            IconPath = (exePath != null && File.Exists(exePath)) ? exePath : null
        };
    }
}
