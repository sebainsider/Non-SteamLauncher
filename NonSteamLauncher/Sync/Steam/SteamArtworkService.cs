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

    public GameArtworkOptions FetchArtworkOptionsForGame(
        InstalledGame game,
        uint appId,
        string? sgdbApiKey,
        string? gridDir = null,
        int maxPerPiece = 4,
        bool onlyMissing = false)
    {
        maxPerPiece = Math.Clamp(maxPerPiece, 1, 12);
        var options = new GameArtworkOptions
        {
            Game = game,
            AppId = appId
        };

        string title = game.Title;
        if (string.IsNullOrWhiteSpace(title))
            return options;

        bool hasPoster = !string.IsNullOrEmpty(gridDir) && appId != 0 && HasExistingFile(gridDir, $"{appId}p", ".png", ".jpg", ".jpeg");
        bool hasWide = !string.IsNullOrEmpty(gridDir) && appId != 0 && HasExistingFile(gridDir, $"{appId}", ".png", ".jpg", ".jpeg");
        bool hasHero = !string.IsNullOrEmpty(gridDir) && appId != 0 && HasExistingFile(gridDir, $"{appId}_hero", ".png", ".jpg", ".jpeg");
        bool hasLogo = !string.IsNullOrEmpty(gridDir) && appId != 0 && HasExistingFile(gridDir, $"{appId}_logo", ".png");
        bool hasIcon = !string.IsNullOrEmpty(gridDir) && appId != 0 && HasExistingFile(gridDir, $"{appId}_icon", ".ico", ".png");

        options.ExistingPieces[ArtworkPieceType.Poster] = hasPoster;
        options.ExistingPieces[ArtworkPieceType.Wide] = hasWide;
        options.ExistingPieces[ArtworkPieceType.Hero] = hasHero;
        options.ExistingPieces[ArtworkPieceType.Logo] = hasLogo;
        options.ExistingPieces[ArtworkPieceType.Icon] = hasIcon;

        bool wantPoster = !onlyMissing || !hasPoster;
        bool wantWide = !onlyMissing || !hasWide;
        bool wantHero = !onlyMissing || !hasHero;
        bool wantLogo = !onlyMissing || !hasLogo;
        bool wantIcon = !onlyMissing || !hasIcon;

        // 1. Primary: SteamGridDB API v2 — fetch top-ranked candidates per piece in parallel
        if (!string.IsNullOrWhiteSpace(sgdbApiKey))
        {
            int? sgdbGameId = SearchSteamGridDbGameId(title, sgdbApiKey.Trim());
            if (sgdbGameId.HasValue)
            {
                int id = sgdbGameId.Value;
                _logger.LogDebug($"  Found '{title}' on SteamGridDB (Game ID: {id}). Fetching top {maxPerPiece} options per piece...");

                var posterTask = wantPoster
                    ? Task.Run(() =>
                    {
                        var list = GetTopSgdbCandidates(
                            $"{SgdbBaseUrl}/grids/game/{id}?dimensions=600x900&mimes=image/png,image/jpeg&nsfw=false&humor=false",
                            ArtworkPieceType.Poster, sgdbApiKey, ".png", maxPerPiece);
                        if (list.Count < maxPerPiece)
                        {
                            var extra = GetTopSgdbCandidates(
                                $"{SgdbBaseUrl}/grids/game/{id}?nsfw=false&humor=false",
                                ArtworkPieceType.Poster, sgdbApiKey, ".png", maxPerPiece);
                            MergeUniqueCandidates(list, extra, maxPerPiece);
                        }
                        return list;
                    })
                    : Task.FromResult(new List<ArtworkCandidate>());

                var wideTask = wantWide
                    ? Task.Run(() => GetTopSgdbCandidates(
                        $"{SgdbBaseUrl}/grids/game/{id}?dimensions=920x430,460x215&mimes=image/png,image/jpeg&nsfw=false&humor=false",
                        ArtworkPieceType.Wide, sgdbApiKey, ".png", maxPerPiece))
                    : Task.FromResult(new List<ArtworkCandidate>());

                var heroTask = wantHero
                    ? Task.Run(() => GetTopSgdbCandidates(
                        $"{SgdbBaseUrl}/heroes/game/{id}?mimes=image/png,image/jpeg&nsfw=false&humor=false",
                        ArtworkPieceType.Hero, sgdbApiKey, ".png", maxPerPiece))
                    : Task.FromResult(new List<ArtworkCandidate>());

                var logoTask = wantLogo
                    ? Task.Run(() => GetTopSgdbCandidates(
                        $"{SgdbBaseUrl}/logos/game/{id}?mimes=image/png&nsfw=false&humor=false",
                        ArtworkPieceType.Logo, sgdbApiKey, ".png", maxPerPiece))
                    : Task.FromResult(new List<ArtworkCandidate>());

                var iconTask = wantIcon
                    ? Task.Run(() => GetTopSgdbCandidates(
                        $"{SgdbBaseUrl}/icons/game/{id}?mimes=image/vnd.microsoft.icon,image/png&nsfw=false&humor=false",
                        ArtworkPieceType.Icon, sgdbApiKey, ".ico", maxPerPiece))
                    : Task.FromResult(new List<ArtworkCandidate>());

                Task.WaitAll(posterTask, wideTask, heroTask, logoTask, iconTask);

                options.Pieces[ArtworkPieceType.Poster].AddRange(posterTask.Result);
                options.Pieces[ArtworkPieceType.Wide].AddRange(wideTask.Result);
                options.Pieces[ArtworkPieceType.Hero].AddRange(heroTask.Result);
                options.Pieces[ArtworkPieceType.Logo].AddRange(logoTask.Result);
                options.Pieces[ArtworkPieceType.Icon].AddRange(iconTask.Result);
            }
        }

        // 2. Fallback / Supplement: Official Steam Store CDN if any piece has fewer than maxPerPiece options
        bool needsSteamFallback =
            (wantPoster && options.Pieces[ArtworkPieceType.Poster].Count < maxPerPiece) ||
            (wantWide && options.Pieces[ArtworkPieceType.Wide].Count < maxPerPiece) ||
            (wantHero && options.Pieces[ArtworkPieceType.Hero].Count < maxPerPiece) ||
            (wantLogo && options.Pieces[ArtworkPieceType.Logo].Count < maxPerPiece);

        if (needsSteamFallback)
        {
            int? steamStoreAppId = SearchSteamStoreAppId(title);
            if (steamStoreAppId.HasValue)
            {
                int sAppId = steamStoreAppId.Value;
                _logger.LogDebug($"  Checking Steam Store CDN candidates for '{title}' (Steam AppID: {sAppId}).");

                if (wantPoster && options.Pieces[ArtworkPieceType.Poster].Count < maxPerPiece)
                {
                    string poster2x = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/library_600x900_2x.jpg";
                    string poster1x = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/library_600x900.jpg";
                    if (IsUrlReachable(poster2x))
                    {
                        AddCandidateIfUnique(options.Pieces[ArtworkPieceType.Poster],
                            new ArtworkCandidate(ArtworkPieceType.Poster, poster2x, poster2x, ".jpg", 0, 600, 900, "Official", "Steam", "Steam Store"),
                            maxPerPiece);
                    }
                    else if (IsUrlReachable(poster1x))
                    {
                        AddCandidateIfUnique(options.Pieces[ArtworkPieceType.Poster],
                            new ArtworkCandidate(ArtworkPieceType.Poster, poster1x, poster1x, ".jpg", 0, 600, 900, "Official", "Steam", "Steam Store"),
                            maxPerPiece);
                    }
                }

                if (wantWide && options.Pieces[ArtworkPieceType.Wide].Count < maxPerPiece)
                {
                    string headerUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/header.jpg";
                    if (IsUrlReachable(headerUrl))
                    {
                        AddCandidateIfUnique(options.Pieces[ArtworkPieceType.Wide],
                            new ArtworkCandidate(ArtworkPieceType.Wide, headerUrl, headerUrl, ".jpg", 0, 460, 215, "Official", "Steam", "Steam Store"),
                            maxPerPiece);
                    }
                }

                if (wantHero && options.Pieces[ArtworkPieceType.Hero].Count < maxPerPiece)
                {
                    string heroUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/library_hero.jpg";
                    if (IsUrlReachable(heroUrl))
                    {
                        AddCandidateIfUnique(options.Pieces[ArtworkPieceType.Hero],
                            new ArtworkCandidate(ArtworkPieceType.Hero, heroUrl, heroUrl, ".jpg", 0, 1920, 620, "Official", "Steam", "Steam Store"),
                            maxPerPiece);
                    }
                }

                if (wantLogo && options.Pieces[ArtworkPieceType.Logo].Count < maxPerPiece)
                {
                    string logoUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{sAppId}/logo.png";
                    if (IsUrlReachable(logoUrl))
                    {
                        AddCandidateIfUnique(options.Pieces[ArtworkPieceType.Logo],
                            new ArtworkCandidate(ArtworkPieceType.Logo, logoUrl, logoUrl, ".png", 0, 0, 0, "Official", "Steam", "Steam Store"),
                            maxPerPiece);
                    }
                }
            }
        }

        return options;
    }

    public ArtworkDownloadResult ApplySelectedArtworkForShortcut(
        string gridDir,
        SteamShortcut shortcut,
        InstalledGame? game,
        GameArtworkSelection selection,
        bool dryRun = false)
    {
        var result = new ArtworkDownloadResult();
        uint appId = shortcut.AppId != 0 ? shortcut.AppId : selection.AppId;
        string title = !string.IsNullOrWhiteSpace(game?.Title) ? game.Title : shortcut.AppName;
        if (string.IsNullOrWhiteSpace(title) || appId == 0)
            return result;

        if (dryRun)
        {
            _logger.LogInfo($"  [DRY RUN] Would apply selected artwork for '{title}' (AppID: {appId}).");
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

        int downloaded = 0;

        foreach (var (pieceType, candidate) in selection.SelectedPieces)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Url))
                continue;

            byte[]? bytes = DownloadBytes(candidate.Url);
            if (bytes is not { Length: > 0 })
                continue;

            string ext = !string.IsNullOrWhiteSpace(candidate.Extension)
                ? candidate.Extension
                : GetExtensionFromUrl(candidate.Url, pieceType == ArtworkPieceType.Icon ? ".ico" : ".png");

            string baseName = pieceType switch
            {
                ArtworkPieceType.Poster => $"{appId}p",
                ArtworkPieceType.Wide => $"{appId}",
                ArtworkPieceType.Hero => $"{appId}_hero",
                ArtworkPieceType.Logo => $"{appId}_logo",
                ArtworkPieceType.Icon => $"{appId}_icon",
                _ => $"{appId}"
            };

            // Remove conflicting extensions for this piece so Steam loads the newly selected artwork
            DeleteExistingVariants(gridDir, baseName, ext, pieceType);

            string fileName = $"{baseName}{ext}";
            if (TryWriteAsset(gridDir, fileName, bytes))
            {
                downloaded++;
                if (pieceType == ArtworkPieceType.Icon)
                {
                    string iconFullPath = Path.Combine(gridDir, fileName);
                    shortcut.Icon = iconFullPath;
                    if (game != null)
                    {
                        game.IconPath = iconFullPath;
                    }
                    result.IconsModifiedInShortcuts = true;
                }
            }
        }

        if (downloaded > 0)
        {
            result.GamesUpdatedCount = 1;
            result.ImagesDownloadedCount = downloaded;
            _logger.LogInfo($"  🎨 Applied {downloaded} selected artwork image(s) for '{title}' (AppID: {appId}).");
        }

        return result;
    }

    public byte[]? DownloadImageBytes(string url) => DownloadBytes(url);

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

    public static string CleanTitleForSearch(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        string cleaned = title.Replace("™", "").Replace("®", "").Replace("©", "");

        string[] editionPatterns = new[]
        {
            " - Game of the Year Edition",
            " Game of the Year Edition",
            " (Game of the Year Edition)",
            " [Game of the Year Edition]",
            " - GOTY Edition",
            " GOTY Edition",
            " - GOTY",
            " GOTY",
            " - Complete Edition",
            " Complete Edition",
            " - Definitive Edition",
            " Definitive Edition",
            " - Enhanced Edition",
            " Enhanced Edition",
            " - Director's Cut",
            " Director's Cut",
            " - Digital Deluxe Edition",
            " Digital Deluxe Edition",
            " - Deluxe Edition",
            " Deluxe Edition",
            " - Special Edition",
            " Special Edition",
            " - Remastered Edition",
            " Remastered Edition",
            " - Remastered",
            " Remastered",
            " - Standard Edition",
            " Standard Edition"
        };

        foreach (var pattern in editionPatterns)
        {
            int idx = cleaned.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                cleaned = cleaned.Remove(idx, pattern.Length).Trim();
            }
        }

        return cleaned.Trim();
    }

    private int? SearchSteamGridDbGameId(string title, string apiKey)
    {
        var id = SearchSteamGridDbGameIdSingle(title, apiKey);
        if (id.HasValue)
            return id;

        string cleaned = CleanTitleForSearch(title);
        if (!string.IsNullOrEmpty(cleaned) && !string.Equals(cleaned, title, StringComparison.OrdinalIgnoreCase))
        {
            id = SearchSteamGridDbGameIdSingle(cleaned, apiKey);
            if (id.HasValue)
                return id;
        }

        int dashIdx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0)
        {
            id = SearchSteamGridDbGameIdSingle(title[..dashIdx].Trim(), apiKey);
            if (id.HasValue)
                return id;
        }

        int colonIdx = title.IndexOf(':');
        if (colonIdx > 0)
        {
            id = SearchSteamGridDbGameIdSingle(title[..colonIdx].Trim(), apiKey);
            if (id.HasValue)
                return id;
        }

        return null;
    }

    private int? SearchSteamGridDbGameIdSingle(string title, string apiKey)
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
        var top = GetTopSgdbCandidates(endpointUrl, ArtworkPieceType.Poster, apiKey, ".png", maxCount: 1);
        return top.Count > 0 ? top[0].Url : null;
    }

    internal List<ArtworkCandidate> GetTopSgdbCandidates(
        string endpointUrl,
        ArtworkPieceType pieceType,
        string apiKey,
        string fallbackExt,
        int maxCount = 4)
    {
        var results = new List<(ArtworkCandidate Candidate, int OriginalIndex)>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, endpointUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var resp = _httpClient.Send(req);
            if (!resp.IsSuccessStatusCode)
                return new List<ArtworkCandidate>();

            string json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                return new List<ArtworkCandidate>();

            if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array || dataProp.GetArrayLength() == 0)
                return new List<ArtworkCandidate>();

            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;

            foreach (var item in dataProp.EnumerateArray())
            {
                index++;

                if (item.TryGetProperty("is_deleted", out var deletedProp) &&
                    deletedProp.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                if (!item.TryGetProperty("url", out var urlProp))
                    continue;

                string? url = urlProp.GetString();
                if (string.IsNullOrWhiteSpace(url) || !seenUrls.Add(url))
                    continue;

                string thumbUrl = url;
                if (item.TryGetProperty("thumb", out var thumbProp))
                {
                    string? thumb = thumbProp.GetString();
                    if (!string.IsNullOrWhiteSpace(thumb))
                        thumbUrl = thumb;
                }

                int score = 0;
                if (item.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetInt32(out int parsedScore))
                {
                    score = parsedScore;
                }
                else
                {
                    int upvotes = item.TryGetProperty("upvotes", out var upProp) && upProp.TryGetInt32(out int u) ? u : 0;
                    int downvotes = item.TryGetProperty("downvotes", out var downProp) && downProp.TryGetInt32(out int d) ? d : 0;
                    score = upvotes - downvotes;
                }

                int width = item.TryGetProperty("width", out var wProp) && wProp.TryGetInt32(out int w) ? w : 0;
                int height = item.TryGetProperty("height", out var hProp) && hProp.TryGetInt32(out int h) ? h : 0;

                string? style = item.TryGetProperty("style", out var styleProp) ? styleProp.GetString() : null;
                string? author = null;
                if (item.TryGetProperty("author", out var authorProp) &&
                    authorProp.ValueKind == JsonValueKind.Object &&
                    authorProp.TryGetProperty("name", out var authorNameProp))
                {
                    author = authorNameProp.GetString();
                }

                string ext = GetExtensionFromUrl(url, fallbackExt);
                if (pieceType == ArtworkPieceType.Logo)
                    ext = ".png";

                var candidate = new ArtworkCandidate(
                    PieceType: pieceType,
                    Url: url,
                    ThumbUrl: thumbUrl,
                    Extension: ext,
                    Score: score,
                    Width: width,
                    Height: height,
                    Style: FormatStyleLabel(style),
                    Author: author,
                    Source: "SteamGridDB");

                results.Add((candidate, index));
            }
        }
        catch { }

        return results
            .OrderByDescending(x => x.Candidate.Score)
            .ThenByDescending(x => (long)x.Candidate.Width * x.Candidate.Height)
            .ThenBy(x => x.OriginalIndex)
            .Take(maxCount)
            .Select(x => x.Candidate)
            .ToList();
    }

    private static string? FormatStyleLabel(string? rawStyle)
    {
        if (string.IsNullOrWhiteSpace(rawStyle))
            return null;

        return rawStyle.Trim().ToLowerInvariant() switch
        {
            "alternate" => "Alternate",
            "blurred" => "Blurred",
            "white_logo" => "White logo",
            "material" => "Material",
            "no_logo" => "No logo",
            "official" => "Official",
            "custom" => "Custom",
            _ => char.ToUpperInvariant(rawStyle[0]) + rawStyle[1..].Replace('_', ' ')
        };
    }

    private static void MergeUniqueCandidates(List<ArtworkCandidate> target, List<ArtworkCandidate> incoming, int maxCount)
    {
        foreach (var candidate in incoming)
        {
            AddCandidateIfUnique(target, candidate, maxCount);
            if (target.Count >= maxCount)
                break;
        }
    }

    private static void AddCandidateIfUnique(List<ArtworkCandidate> target, ArtworkCandidate candidate, int maxCount)
    {
        if (target.Count >= maxCount)
            return;

        if (!target.Any(c => string.Equals(c.Url, candidate.Url, StringComparison.OrdinalIgnoreCase)))
        {
            target.Add(candidate);
        }
    }

    private bool IsUrlReachable(string url)
    {
        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResp = _httpClient.Send(headReq);
            if (headResp.IsSuccessStatusCode)
                return true;

            // Some servers/mocks do not support HEAD; fall back to GET check
            if (headResp.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed ||
                headResp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                using var getReq = new HttpRequestMessage(HttpMethod.Get, url);
                using var getResp = _httpClient.Send(getReq);
                if (!getResp.IsSuccessStatusCode)
                    return false;
                byte[] data = getResp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return data.Length > 64;
            }
        }
        catch { }

        return false;
    }

    private static void DeleteExistingVariants(string gridDir, string baseName, string newExt, ArtworkPieceType pieceType)
    {
        string[] variants = pieceType switch
        {
            ArtworkPieceType.Logo => new[] { ".png" },
            ArtworkPieceType.Icon => new[] { ".ico", ".png" },
            _ => new[] { ".png", ".jpg", ".jpeg" }
        };

        foreach (var ext in variants)
        {
            if (string.Equals(ext, newExt, StringComparison.OrdinalIgnoreCase))
                continue;

            string oldPath = Path.Combine(gridDir, baseName + ext);
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { }
            }
        }
    }

    private int? SearchSteamStoreAppId(string title)
    {
        var id = SearchSteamStoreAppIdSingle(title);
        if (id.HasValue)
            return id;

        string cleaned = CleanTitleForSearch(title);
        if (!string.IsNullOrEmpty(cleaned) && !string.Equals(cleaned, title, StringComparison.OrdinalIgnoreCase))
        {
            id = SearchSteamStoreAppIdSingle(cleaned);
            if (id.HasValue)
                return id;
        }

        int dashIdx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0)
        {
            id = SearchSteamStoreAppIdSingle(title[..dashIdx].Trim());
            if (id.HasValue)
                return id;
        }

        int colonIdx = title.IndexOf(':');
        if (colonIdx > 0)
        {
            id = SearchSteamStoreAppIdSingle(title[..colonIdx].Trim());
            if (id.HasValue)
                return id;
        }

        return null;
    }

    private int? SearchSteamStoreAppIdSingle(string title)
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
