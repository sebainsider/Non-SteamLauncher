using System.Net.Http.Headers;
using System.Text.Json;

namespace NonSteamLauncher.Sync.Steam;

public class ArtworkDownloadResult
{
    public int GamesUpdatedCount { get; set; }
    public int ImagesDownloadedCount { get; set; }
    public bool IconsModifiedInShortcuts { get; set; }
}

public class SteamArtworkService
{
    private const string SgdbBaseUrl = "https://www.steamgriddb.com/api/v2";
    private readonly HttpClient _httpClient;
    private readonly Logger _logger;

    private sealed record CachedGameArtwork(
        byte[]? PosterBytes, string PosterExt,
        byte[]? WideBytes, string WideExt,
        byte[]? HeroBytes, string HeroExt,
        byte[]? LogoBytes, string LogoExt,
        byte[]? IconBytes, string IconExt);

    private readonly Dictionary<string, CachedGameArtwork> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

    public SteamArtworkService(Logger? logger = null, HttpClient? httpClient = null)
    {
        _logger = logger ?? new Logger(verbose: true);
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NonSteamLauncher/1.7.0");
        }
    }

    public ArtworkDownloadResult EnsureArtworkForShortcut(
        string gridDir,
        SteamShortcut shortcut,
        InstalledGame? game,
        string? sgdbApiKey,
        bool dryRun = false)
    {
        var result = new ArtworkDownloadResult();
        uint appId = shortcut.AppId;
        string title = !string.IsNullOrWhiteSpace(game?.Title) ? game.Title : shortcut.AppName;
        if (string.IsNullOrWhiteSpace(title) || appId == 0)
            return result;

        bool hasPoster = HasExistingFile(gridDir, $"{appId}p", ".png", ".jpg", ".jpeg");
        bool hasWide = HasExistingFile(gridDir, $"{appId}", ".png", ".jpg", ".jpeg");
        bool hasHero = HasExistingFile(gridDir, $"{appId}_hero", ".png", ".jpg", ".jpeg");
        bool hasLogo = HasExistingFile(gridDir, $"{appId}_logo", ".png");
        string? existingGridIcon = FindExistingFile(gridDir, $"{appId}_icon", ".ico", ".png");
        bool hasIcon = existingGridIcon != null;

        // If an icon already exists in grid/ and the shortcut does not use it yet (or uses an exe icon when SGDB icon exists), link it
        if (existingGridIcon != null && !string.Equals(shortcut.Icon, existingGridIcon, StringComparison.OrdinalIgnoreCase))
        {
            shortcut.Icon = existingGridIcon;
            if (game != null)
            {
                game.IconPath = existingGridIcon;
            }
            result.IconsModifiedInShortcuts = true;
        }

        // If all 4 grid pieces exist and (either we have an icon or no SGDB API key is set to fetch custom icons), nothing to download
        bool needsIconDownload = !hasIcon && !string.IsNullOrWhiteSpace(sgdbApiKey);
        if (hasPoster && hasWide && hasHero && hasLogo && !needsIconDownload)
        {
            return result;
        }

        if (dryRun)
        {
            _logger.LogInfo($"  [DRY RUN] Would download missing SteamGridDB artwork for '{title}' (AppID: {appId}).");
            return result;
        }

        try
        {
            Directory.CreateDirectory(gridDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"  Could not create Steam grid directory '{gridDir}': {ex.Message}");
            return result;
        }

        var artwork = FetchArtworkForTitle(title, sgdbApiKey);
        if (artwork == null)
            return result;

        int downloaded = 0;

        if (!hasPoster && artwork.PosterBytes is { Length: > 0 })
        {
            if (TryWriteAsset(gridDir, $"{appId}p{artwork.PosterExt}", artwork.PosterBytes))
                downloaded++;
        }

        if (!hasWide && artwork.WideBytes is { Length: > 0 })
        {
            if (TryWriteAsset(gridDir, $"{appId}{artwork.WideExt}", artwork.WideBytes))
                downloaded++;
        }

        if (!hasHero && artwork.HeroBytes is { Length: > 0 })
        {
            if (TryWriteAsset(gridDir, $"{appId}_hero{artwork.HeroExt}", artwork.HeroBytes))
                downloaded++;
        }

        if (!hasLogo && artwork.LogoBytes is { Length: > 0 })
        {
            if (TryWriteAsset(gridDir, $"{appId}_logo{artwork.LogoExt}", artwork.LogoBytes))
                downloaded++;
        }

        if (!hasIcon && artwork.IconBytes is { Length: > 0 })
        {
            string iconFileName = $"{appId}_icon{artwork.IconExt}";
            string iconFullPath = Path.Combine(gridDir, iconFileName);
            if (TryWriteAsset(gridDir, iconFileName, artwork.IconBytes))
            {
                downloaded++;
                shortcut.Icon = iconFullPath;
                if (game != null)
                {
                    game.IconPath = iconFullPath;
                }
                result.IconsModifiedInShortcuts = true;
            }
        }

        if (downloaded > 0)
        {
            result.GamesUpdatedCount = 1;
            result.ImagesDownloadedCount = downloaded;
            _logger.LogInfo($"  🎨 Downloaded {downloaded} artwork image(s) for '{title}' (AppID: {appId}).");
        }

        return result;
    }

    private CachedGameArtwork? FetchArtworkForTitle(string title, string? sgdbApiKey)
    {
        string cacheKey = $"{GameDetector.NormalizeTitle(title)}|{(string.IsNullOrWhiteSpace(sgdbApiKey) ? "nokey" : "sgdb")}";
        if (_memoryCache.TryGetValue(cacheKey, out var cached))
            return cached;

        byte[]? posterBytes = null;
        string posterExt = ".png";
        byte[]? wideBytes = null;
        string wideExt = ".png";
        byte[]? heroBytes = null;
        string heroExt = ".png";
        byte[]? logoBytes = null;
        string logoExt = ".png";
        byte[]? iconBytes = null;
        string iconExt = ".ico";

        // 1. Primary: SteamGridDB API v2 (same database as BoilR & SGDBoop)
        if (!string.IsNullOrWhiteSpace(sgdbApiKey))
        {
            int? sgdbGameId = SearchSteamGridDbGameId(title, sgdbApiKey.Trim());
            if (sgdbGameId.HasValue)
            {
                int id = sgdbGameId.Value;
                _logger.LogDebug($"  Found '{title}' on SteamGridDB (Game ID: {id}). Fetching artwork...");

                var posterUrl = GetFirstSgdbAssetUrl($"{SgdbBaseUrl}/grids/game/{id}?dimensions=600x900&mimes=image/png,image/jpeg&nsfw=false&humor=false", sgdbApiKey)
                             ?? GetFirstSgdbAssetUrl($"{SgdbBaseUrl}/grids/game/{id}?nsfw=false&humor=false", sgdbApiKey);
                if (posterUrl != null)
                {
                    posterBytes = DownloadBytes(posterUrl);
                    posterExt = GetExtensionFromUrl(posterUrl, ".png");
                }

                var wideUrl = GetFirstSgdbAssetUrl($"{SgdbBaseUrl}/grids/game/{id}?dimensions=920x430,460x215&mimes=image/png,image/jpeg&nsfw=false&humor=false", sgdbApiKey);
                if (wideUrl != null)
                {
                    wideBytes = DownloadBytes(wideUrl);
                    wideExt = GetExtensionFromUrl(wideUrl, ".png");
                }

                var heroUrl = GetFirstSgdbAssetUrl($"{SgdbBaseUrl}/heroes/game/{id}?mimes=image/png,image/jpeg&nsfw=false&humor=false", sgdbApiKey);
                if (heroUrl != null)
                {
                    heroBytes = DownloadBytes(heroUrl);
                    heroExt = GetExtensionFromUrl(heroUrl, ".png");
                }

                var logoUrl = GetFirstSgdbAssetUrl($"{SgdbBaseUrl}/logos/game/{id}?mimes=image/png&nsfw=false&humor=false", sgdbApiKey);
                if (logoUrl != null)
                {
                    logoBytes = DownloadBytes(logoUrl);
                    logoExt = ".png";
                }

                var iconUrl = GetFirstSgdbAssetUrl($"{SgdbBaseUrl}/icons/game/{id}?mimes=image/vnd.microsoft.icon,image/png&nsfw=false&humor=false", sgdbApiKey);
                if (iconUrl != null)
                {
                    iconBytes = DownloadBytes(iconUrl);
                    iconExt = GetExtensionFromUrl(iconUrl, ".ico");
                }
            }
        }

        // 2. Fallback: Official Steam Store CDN for any missing grid assets (works even without API key)
        if (posterBytes == null || wideBytes == null || heroBytes == null || logoBytes == null)
        {
            int? steamStoreAppId = SearchSteamStoreAppId(title);
            if (steamStoreAppId.HasValue)
            {
                int sAppId = steamStoreAppId.Value;
                _logger.LogDebug($"  Using Steam Store CDN fallback for '{title}' (Steam AppID: {sAppId}).");

                if (posterBytes == null)
                {
                    posterBytes = DownloadBytes($"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/library_600x900_2x.jpg")
                               ?? DownloadBytes($"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/library_600x900.jpg");
                    if (posterBytes != null) posterExt = ".jpg";
                }

                if (wideBytes == null)
                {
                    wideBytes = DownloadBytes($"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/header.jpg");
                    if (wideBytes != null) wideExt = ".jpg";
                }

                if (heroBytes == null)
                {
                    heroBytes = DownloadBytes($"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/library_hero.jpg");
                    if (heroBytes != null) heroExt = ".jpg";
                }

                if (logoBytes == null)
                {
                    logoBytes = DownloadBytes($"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/logo.png");
                    if (logoBytes != null) logoExt = ".png";
                }
            }
        }

        var built = new CachedGameArtwork(
            posterBytes, posterExt,
            wideBytes, wideExt,
            heroBytes, heroExt,
            logoBytes, logoExt,
            iconBytes, iconExt);

        _memoryCache[cacheKey] = built;
        return built;
    }

    private int? SearchSteamGridDbGameId(string title, string apiKey)
    {
        try
        {
            string url = $"{SgdbBaseUrl}/search/autocomplete/{Uri.EscapeDataString(title)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var resp = _httpClient.Send(req);
            if (!resp.IsSuccessStatusCode)
                return null;

            string json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                return null;

            if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array || dataProp.GetArrayLength() == 0)
                return null;

            string normTarget = GameDetector.NormalizeTitle(title);
            int? firstId = null;

            foreach (var item in dataProp.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idProp) || !idProp.TryGetInt32(out int id))
                    continue;

                firstId ??= id;

                if (item.TryGetProperty("name", out var nameProp))
                {
                    string? name = nameProp.GetString();
                    if (!string.IsNullOrEmpty(name) && GameDetector.NormalizeTitle(name).Equals(normTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        return id;
                    }
                }
            }

            return firstId;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"  SteamGridDB search failed for '{title}': {ex.Message}");
            return null;
        }
    }

    private string? GetFirstSgdbAssetUrl(string endpointUrl, string apiKey)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, endpointUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var resp = _httpClient.Send(req);
            if (!resp.IsSuccessStatusCode)
                return null;

            string json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                return null;

            if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array || dataProp.GetArrayLength() == 0)
                return null;

            foreach (var item in dataProp.EnumerateArray())
            {
                if (item.TryGetProperty("url", out var urlProp))
                {
                    string? url = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                        return url;
                }
            }
        }
        catch { }

        return null;
    }

    private int? SearchSteamStoreAppId(string title)
    {
        try
        {
            string url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(title)}&l=english&cc=US";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = _httpClient.Send(req);
            if (!resp.IsSuccessStatusCode)
                return null;

            string json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("items", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array || itemsProp.GetArrayLength() == 0)
                return null;

            string normTarget = GameDetector.NormalizeTitle(title);
            int? firstId = null;

            foreach (var item in itemsProp.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idProp) || !idProp.TryGetInt32(out int id))
                    continue;

                firstId ??= id;

                if (item.TryGetProperty("name", out var nameProp))
                {
                    string? name = nameProp.GetString();
                    if (!string.IsNullOrEmpty(name) && GameDetector.NormalizeTitle(name).Equals(normTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        return id;
                    }
                }
            }

            return firstId;
        }
        catch
        {
            return null;
        }
    }

    private byte[]? DownloadBytes(string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = _httpClient.Send(req);
            if (!resp.IsSuccessStatusCode)
                return null;

            byte[] data = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return data.Length > 64 ? data : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetExtensionFromUrl(string url, string fallbackExt)
    {
        try
        {
            var uri = new Uri(url);
            string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".ico")
            {
                return ext == ".jpeg" ? ".jpg" : ext;
            }
        }
        catch { }

        return fallbackExt;
    }

    private static bool HasExistingFile(string dir, string baseName, params string[] extensions) =>
        FindExistingFile(dir, baseName, extensions) != null;

    private static string? FindExistingFile(string dir, string baseName, params string[] extensions)
    {
        if (!Directory.Exists(dir))
            return null;

        foreach (string ext in extensions)
        {
            string candidate = Path.Combine(dir, baseName + ext);
            if (File.Exists(candidate))
            {
                try
                {
                    if (new FileInfo(candidate).Length > 0)
                        return candidate;
                }
                catch { }
            }
        }
        return null;
    }

    private static bool TryWriteAsset(string gridDir, string fileName, byte[] bytes)
    {
        try
        {
            string fullPath = Path.Combine(gridDir, fileName);
            File.WriteAllBytes(fullPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
