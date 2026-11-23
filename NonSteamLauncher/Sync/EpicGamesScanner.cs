using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NonSteamLauncher.Sync;

public class EpicGamesScanner : IScanner
{
    private readonly string? _customManifestsPath;
    private readonly string? _customPreviewInstallationsPath;
    private readonly Logger? _logger;

    public string ScannerName => "Epic Games Store";

    public EpicGamesScanner(
        string? customManifestsPath = null,
        Logger? logger = null,
        string? customPreviewInstallationsPath = null)
    {
        _customManifestsPath = customManifestsPath;
        _customPreviewInstallationsPath = customPreviewInstallationsPath;
        _logger = logger;
    }

    public static string DefaultManifestsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests"
    );

    public static string DefaultPreviewInstallationsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "com.epicgames.eglv2", "installations.jsonl"
    );

    public IReadOnlyList<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();
        var seenUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string manifestsDir = _customManifestsPath ?? DefaultManifestsDirectory;

        if (Directory.Exists(manifestsDir))
        {
            try
            {
                var files = Directory.GetFiles(manifestsDir, "*.item", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var game = ParseManifestFile(file);
                        if (game != null && seenUris.Add(game.LaunchUri))
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
        }
        else
        {
            _logger?.LogDebug($"Epic Games legacy manifests directory not found: '{manifestsDir}'");
        }

        // Also scan the new Epic Games Launcher Preview (com.epicgames.eglv2) installations.jsonl
        string previewPath = _customPreviewInstallationsPath ?? DefaultPreviewInstallationsPath;
        if (File.Exists(previewPath))
        {
            try
            {
                foreach (var game in ParsePreviewInstallationsFile(previewPath))
                {
                    if (seenUris.Add(game.LaunchUri))
                    {
                        games.Add(game);
                        _logger?.LogDebug($"Found Epic Preview game: {game.Title} ({game.LaunchUri})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error reading Epic Games Preview installations from '{previewPath}': {ex.Message}");
            }
        }

        return games;
    }

    public static IReadOnlyList<InstalledGame> ParsePreviewInstallationsFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        return ParsePreviewInstallationsLines(lines);
    }

    public static IReadOnlyList<InstalledGame> ParsePreviewInstallationsLines(IEnumerable<string> lines)
    {
        var games = new List<InstalledGame>();

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(rawLine);
                var root = doc.RootElement;

                // Header line has "entries" instead of "artifactId"
                if (!root.TryGetProperty("artifactId", out var artifactProp))
                    continue;

                if (root.TryGetProperty("complete", out var completeProp) &&
                    completeProp.ValueKind == JsonValueKind.False)
                {
                    continue;
                }

                string artifactId = artifactProp.GetString() ?? "";
                string catalogNamespace = root.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() ?? "" : "";
                string catalogItemId = root.TryGetProperty("itemId", out var itemProp) ? itemProp.GetString() ?? "" : "";
                string installLocation = root.TryGetProperty("dir", out var dirProp) ? dirProp.GetString() ?? "" : "";
                string launchExecutable = root.TryGetProperty("launchExecutable", out var exeProp) ? exeProp.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(artifactId))
                    continue;

                if (artifactId.StartsWith("UE_", StringComparison.OrdinalIgnoreCase) ||
                    artifactId.StartsWith("UnrealEngine", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string uri;
                if (!string.IsNullOrEmpty(catalogNamespace) && !string.IsNullOrEmpty(catalogItemId))
                {
                    uri = $"com.epicgames.launcher://apps/{catalogNamespace}%3A{catalogItemId}%3A{artifactId}?action=launch&silent=true";
                }
                else
                {
                    uri = $"com.epicgames.launcher://apps/{artifactId}?action=launch&silent=true";
                }

                string? exePath = null;
                if (!string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(launchExecutable))
                {
                    exePath = Path.Combine(installLocation, launchExecutable);
                }

                string title = ResolvePreviewDisplayTitle(installLocation, exePath, artifactId);

                games.Add(new InstalledGame
                {
                    Title = title,
                    Launcher = "Epic Games",
                    LaunchUri = uri,
                    InstallDirectory = string.IsNullOrEmpty(installLocation) ? null : installLocation,
                    ExecutablePath = exePath,
                    IconPath = (exePath != null && File.Exists(exePath)) ? exePath : null
                });
            }
            catch
            {
                // Skip malformed JSONL lines
            }
        }

        return games;
    }

    private static string ResolvePreviewDisplayTitle(string installLocation, string? exePath, string fallback)
    {
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.ProductName) &&
                    !info.ProductName.Contains("UnrealEngine", StringComparison.OrdinalIgnoreCase))
                {
                    return info.ProductName.Trim();
                }
            }
            catch
            {
                // Ignore version info errors
            }
        }

        string candidate = !string.IsNullOrEmpty(installLocation)
            ? Path.GetFileName(installLocation.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : fallback;

        if (string.IsNullOrWhiteSpace(candidate))
            return fallback;

        // Turn CamelCase / PascalCase folder names like "AlanWake2" into "Alan Wake 2"
        if (!candidate.Contains(' ') && !candidate.Contains('_') && !candidate.Contains('-'))
        {
            candidate = Regex.Replace(candidate, @"(?<=[a-z])(?=[A-Z0-9])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
        }

        return candidate.Trim();
    }

    /// <summary>
    /// Resolves the LaunchExecutable path for an Epic Games URI (com.epicgames.launcher://apps/...)
    /// by checking both the new Epic Games Launcher Preview (installations.jsonl) and legacy manifests.
    /// </summary>
    public static string? ResolveLaunchExecutableForUri(
        string? launchUri,
        string? customManifestsPath = null,
        string? customPreviewInstallationsPath = null)
    {
        if (string.IsNullOrWhiteSpace(launchUri))
            return null;

        string clean = launchUri.Trim('"', '\'');
        const string prefix = "com.epicgames.launcher://apps/";
        if (!clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string remainder = clean[prefix.Length..];
        int queryIdx = remainder.IndexOf('?');
        if (queryIdx >= 0)
            remainder = remainder[..queryIdx];

        string decodedId = Uri.UnescapeDataString(remainder).Trim('/');
        if (string.IsNullOrWhiteSpace(decodedId))
            return null;

        // Extract artifact/app name (last segment if namespace:itemId:artifactId)
        string[] parts = decodedId.Split(':');
        string targetArtifact = parts[^1];

        string previewPath = customPreviewInstallationsPath ?? DefaultPreviewInstallationsPath;
        if (File.Exists(previewPath))
        {
            try
            {
                foreach (var game in ParsePreviewInstallationsFile(previewPath))
                {
                    if (MatchesEpicUri(game.LaunchUri, decodedId, targetArtifact) &&
                        !string.IsNullOrWhiteSpace(game.ExecutablePath))
                    {
                        return game.ExecutablePath;
                    }
                }
            }
            catch
            {
                // Ignore preview lookup errors
            }
        }

        string manifestsDir = customManifestsPath ?? DefaultManifestsDirectory;
        if (Directory.Exists(manifestsDir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(manifestsDir, "*.item", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var game = ParseManifestFile(file);
                        if (game != null &&
                            MatchesEpicUri(game.LaunchUri, decodedId, targetArtifact) &&
                            !string.IsNullOrWhiteSpace(game.ExecutablePath))
                        {
                            return game.ExecutablePath;
                        }
                    }
                    catch
                    {
                        // Skip malformed manifest
                    }
                }
            }
            catch
            {
                // Ignore manifest lookup errors
            }
        }

        return null;
    }

    private static bool MatchesEpicUri(string gameLaunchUri, string decodedId, string targetArtifact)
    {
        const string prefix = "com.epicgames.launcher://apps/";
        if (!gameLaunchUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string remainder = gameLaunchUri[prefix.Length..];
        int queryIdx = remainder.IndexOf('?');
        if (queryIdx >= 0)
            remainder = remainder[..queryIdx];

        string gameDecodedId = Uri.UnescapeDataString(remainder).Trim('/');
        if (gameDecodedId.Equals(decodedId, StringComparison.OrdinalIgnoreCase))
            return true;

        string[] gameParts = gameDecodedId.Split(':');
        return gameParts[^1].Equals(targetArtifact, StringComparison.OrdinalIgnoreCase);
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
