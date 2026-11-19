using System.Net;
using System.Text;
using NonSteamLauncher.Sync;
using NonSteamLauncher.Sync.Steam;
using Xunit;

namespace NonSteamLauncher.Tests;

public class SteamArtworkTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responder(request);
    }

    [Fact]
    public void AppSettings_SaveAndLoad_RoundTripsCorrectly()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"nsl_settings_{Guid.NewGuid():N}.json");
        try
        {
            var settings = new AppSettings
            {
                SteamGridDbApiKey = "test_sgdb_api_key_123456",
                DownloadArtwork = false
            };
            settings.Save(tempFile);

            var loaded = AppSettings.Load(tempFile);
            Assert.Equal("test_sgdb_api_key_123456", loaded.SteamGridDbApiKey);
            Assert.False(loaded.DownloadArtwork);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void EnsureArtworkForShortcut_DownloadsAllFiveAssetsAndSetsShortcutIcon()
    {
        string tempGridDir = Path.Combine(Path.GetTempPath(), $"nsl_grid_{Guid.NewGuid():N}");
        byte[] fakeImagePayload = new byte[128];
        Array.Fill(fakeImagePayload, (byte)0x89);

        var handler = new FakeHttpMessageHandler(req =>
        {
            string url = req.RequestUri!.ToString();

            if (url.Contains("/search/autocomplete/"))
            {
                return JsonResponse("""{"success":true,"data":[{"id":999,"name":"Alan Wake 2"}]}""");
            }
            if (url.Contains("/grids/game/999?dimensions=600x900"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/grid/poster.png"}]}""");
            }
            if (url.Contains("/grids/game/999?dimensions=920x430"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/grid/wide.png"}]}""");
            }
            if (url.Contains("/heroes/game/999"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/hero/hero.png"}]}""");
            }
            if (url.Contains("/logos/game/999"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/logo/logo.png"}]}""");
            }
            if (url.Contains("/icons/game/999"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/icon/game.ico"}]}""");
            }
            if (url.StartsWith("https://cdn.steamgriddb.com/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fakeImagePayload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        try
        {
            using var client = new HttpClient(handler);
            var service = new SteamArtworkService(new Logger(verbose: false), client);

            var shortcut = new SteamShortcut
            {
                AppId = 2812345678,
                AppName = "Alan Wake 2",
                Exe = "\"C:\\NonSteamLauncher.exe\""
            };
            var game = new InstalledGame
            {
                Title = "Alan Wake 2",
                Launcher = "Epic Games"
            };

            var res = service.EnsureArtworkForShortcut(tempGridDir, shortcut, game, "fake_sgdb_key", dryRun: false);

            Assert.Equal(1, res.GamesUpdatedCount);
            Assert.Equal(5, res.ImagesDownloadedCount);
            Assert.True(res.IconsModifiedInShortcuts);

            Assert.True(File.Exists(Path.Combine(tempGridDir, "2812345678p.png")));
            Assert.True(File.Exists(Path.Combine(tempGridDir, "2812345678.png")));
            Assert.True(File.Exists(Path.Combine(tempGridDir, "2812345678_hero.png")));
            Assert.True(File.Exists(Path.Combine(tempGridDir, "2812345678_logo.png")));
            string expectedIconPath = Path.Combine(tempGridDir, "2812345678_icon.ico");
            Assert.True(File.Exists(expectedIconPath));
            Assert.Equal(expectedIconPath, shortcut.Icon);
            Assert.Equal(expectedIconPath, game.IconPath);
        }
        finally
        {
            if (Directory.Exists(tempGridDir))
                Directory.Delete(tempGridDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("The Witcher 3: Wild Hunt - Game of the Year Edition", "The Witcher 3: Wild Hunt")]
    [InlineData("Control - Ultimate Edition", "Control - Ultimate Edition")]
    [InlineData("Cyberpunk 2077: Phantom Liberty", "Cyberpunk 2077: Phantom Liberty")]
    [InlineData("Dark Souls III - Deluxe Edition", "Dark Souls III")]
    [InlineData("DOOM Eternal (Game of the Year Edition)", "DOOM Eternal")]
    [InlineData("Dishonored: Definitive Edition", "Dishonored:")]
    [InlineData("Batman: Arkham City - GOTY", "Batman: Arkham City")]
    public void CleanTitleForSearch_RemovesEditionSuffixes(string input, string expected)
    {
        string result = SteamArtworkService.CleanTitleForSearch(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EnsureArtworkForShortcut_FallsBackToCleanTitle_WhenSteamStoreRawFails()
    {
        string tempGridDir = Path.Combine(Path.GetTempPath(), $"nsl_grid_witcher_{Guid.NewGuid():N}");
        byte[] fakeImagePayload = new byte[128];
        Array.Fill(fakeImagePayload, (byte)0x77);

        var handler = new FakeHttpMessageHandler(req =>
        {
            string url = req.RequestUri!.ToString();

            // Store search with raw "The Witcher 3: Wild Hunt - Game of the Year Edition" returns 0 items
            if (url.Contains("storesearch") && url.Contains("Game%20of%20the%20Year"))
            {
                return JsonResponse("""{"total":0,"items":[]}""");
            }
            // Store search with clean "The Witcher 3" or "The Witcher 3: Wild Hunt" returns Witcher 3 AppID 292030
            if (url.Contains("storesearch") && url.Contains("Witcher"))
            {
                return JsonResponse("""{"total":1,"items":[{"id":292030,"name":"The Witcher 3: Wild Hunt"}]}""");
            }
            if (url.Contains("cdn.cloudflare.steamstatic.com/steam/apps/292030/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fakeImagePayload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        try
        {
            using var client = new HttpClient(handler);
            var service = new SteamArtworkService(new Logger(verbose: false), client);

            var shortcut = new SteamShortcut
            {
                AppId = 3859965427,
                AppName = "The Witcher 3: Wild Hunt - Game of the Year Edition",
                Exe = "\"C:\\NonSteamLauncher.exe\""
            };
            var game = new InstalledGame
            {
                Title = "The Witcher 3: Wild Hunt - Game of the Year Edition",
                Launcher = "GOG Galaxy"
            };

            // No SGDB key -> uses Steam Store CDN fallback with cleaned title
            var res = service.EnsureArtworkForShortcut(tempGridDir, shortcut, game, sgdbApiKey: null, dryRun: false);

            Assert.Equal(1, res.GamesUpdatedCount);
            Assert.True(res.ImagesDownloadedCount >= 4);

            Assert.True(File.Exists(Path.Combine(tempGridDir, "3859965427p.jpg")));
            Assert.True(File.Exists(Path.Combine(tempGridDir, "3859965427.jpg")));
            Assert.True(File.Exists(Path.Combine(tempGridDir, "3859965427_hero.jpg")));
            Assert.True(File.Exists(Path.Combine(tempGridDir, "3859965427_logo.png")));
        }
        finally
        {
            if (Directory.Exists(tempGridDir))
                Directory.Delete(tempGridDir, recursive: true);
        }
    }

    [Fact]
    public void FetchArtworkOptionsForGame_ReturnsTop4BestRankedOptionsPerPiece_NotJustFirst()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            string url = req.RequestUri!.ToString();

            if (url.Contains("/search/autocomplete/"))
            {
                return JsonResponse("""{"success":true,"data":[{"id":777,"name":"Hades"}]}""");
            }
            if (url.Contains("/grids/game/777?dimensions=600x900"))
            {
                // Intentionally unsorted scores + 1 deleted + 6 total items -> should pick top 4 by score descending
                return JsonResponse("""
                {
                  "success": true,
                  "data": [
                    {"url":"https://cdn.steamgriddb.com/grid/p_low.png","thumb":"https://cdn.steamgriddb.com/thumb/p_low.png","score":2,"width":600,"height":900,"style":"alternate"},
                    {"url":"https://cdn.steamgriddb.com/grid/p_deleted.png","thumb":"https://cdn.steamgriddb.com/thumb/p_deleted.png","score":999,"width":600,"height":900,"is_deleted":true},
                    {"url":"https://cdn.steamgriddb.com/grid/p_best.png","thumb":"https://cdn.steamgriddb.com/thumb/p_best.png","score":150,"width":600,"height":900,"style":"official"},
                    {"url":"https://cdn.steamgriddb.com/grid/p_mid.png","thumb":"https://cdn.steamgriddb.com/thumb/p_mid.png","score":45,"width":600,"height":900,"style":"material"},
                    {"url":"https://cdn.steamgriddb.com/grid/p_second.png","thumb":"https://cdn.steamgriddb.com/thumb/p_second.png","score":95,"width":600,"height":900,"style":"blurred"},
                    {"url":"https://cdn.steamgriddb.com/grid/p_third.png","thumb":"https://cdn.steamgriddb.com/thumb/p_third.png","score":70,"width":600,"height":900,"style":"no_logo"}
                  ]
                }
                """);
            }
            if (url.Contains("/grids/game/777?dimensions=920x430"))
            {
                return JsonResponse("""
                {
                  "success": true,
                  "data": [
                    {"url":"https://cdn.steamgriddb.com/grid/w1.png","score":10,"width":920,"height":430},
                    {"url":"https://cdn.steamgriddb.com/grid/w2.png","score":80,"width":920,"height":430},
                    {"url":"https://cdn.steamgriddb.com/grid/w3.png","score":50,"width":920,"height":430},
                    {"url":"https://cdn.steamgriddb.com/grid/w4.png","score":30,"width":920,"height":430},
                    {"url":"https://cdn.steamgriddb.com/grid/w5.png","score":5,"width":920,"height":430}
                  ]
                }
                """);
            }
            if (url.Contains("/heroes/game/777"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/hero/h1.png","score":12,"width":1920,"height":620}]}""");
            }
            if (url.Contains("/logos/game/777"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/logo/l1.png","score":33,"width":800,"height":300}]}""");
            }
            if (url.Contains("/icons/game/777"))
            {
                return JsonResponse("""{"success":true,"data":[{"url":"https://cdn.steamgriddb.com/icon/i1.ico","score":21,"width":256,"height":256}]}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var service = new SteamArtworkService(new Logger(verbose: false), client);
        var game = new InstalledGame { Title = "Hades", Launcher = "Epic Games" };

        var options = service.FetchArtworkOptionsForGame(game, appId: 123456, sgdbApiKey: "fake_key", maxPerPiece: 4);

        Assert.True(options.HasAnyOptions);

        // Poster: must have exactly 4, ordered by score descending (150, 95, 70, 45), excluding deleted & low
        var posters = options.Pieces[ArtworkPieceType.Poster];
        Assert.Equal(4, posters.Count);
        Assert.Equal("https://cdn.steamgriddb.com/grid/p_best.png", posters[0].Url);
        Assert.Equal("https://cdn.steamgriddb.com/grid/p_second.png", posters[1].Url);
        Assert.Equal("https://cdn.steamgriddb.com/grid/p_third.png", posters[2].Url);
        Assert.Equal("https://cdn.steamgriddb.com/grid/p_mid.png", posters[3].Url);

        // Wide: top 4 by score (80, 50, 30, 10)
        var wides = options.Pieces[ArtworkPieceType.Wide];
        Assert.Equal(4, wides.Count);
        Assert.Equal("https://cdn.steamgriddb.com/grid/w2.png", wides[0].Url);
        Assert.Equal("https://cdn.steamgriddb.com/grid/w3.png", wides[1].Url);
        Assert.Equal("https://cdn.steamgriddb.com/grid/w4.png", wides[2].Url);
        Assert.Equal("https://cdn.steamgriddb.com/grid/w1.png", wides[3].Url);
    }

    [Fact]
    public void ApplySelectedArtworkForShortcut_DownloadsChosenCandidatesAndReplacesOldExtension()
    {
        string tempGridDir = Path.Combine(Path.GetTempPath(), $"nsl_grid_apply_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempGridDir);

        byte[] posterPayload = Enumerable.Repeat((byte)0x11, 128).ToArray();
        byte[] iconPayload = Enumerable.Repeat((byte)0x22, 128).ToArray();

        var handler = new FakeHttpMessageHandler(req =>
        {
            string url = req.RequestUri!.ToString();
            if (url == "https://cdn.steamgriddb.com/grid/chosen_poster.png")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(posterPayload) };
            if (url == "https://cdn.steamgriddb.com/icon/chosen_icon.ico")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(iconPayload) };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        try
        {
            uint appId = 2999888777;
            // Pre-existing .jpg poster should be removed when user selects a .png poster
            string oldPosterJpg = Path.Combine(tempGridDir, $"{appId}p.jpg");
            File.WriteAllText(oldPosterJpg, "old poster");

            using var client = new HttpClient(handler);
            var service = new SteamArtworkService(new Logger(verbose: false), client);

            var shortcut = new SteamShortcut { AppId = appId, AppName = "Hades", Exe = "\"C:\\NonSteamLauncher.exe\"" };
            var game = new InstalledGame { Title = "Hades", Launcher = "Epic Games", AppId = appId };

            var selection = new GameArtworkSelection { Game = game, AppId = appId };
            selection.SelectedPieces[ArtworkPieceType.Poster] = new ArtworkCandidate(
                ArtworkPieceType.Poster, "https://cdn.steamgriddb.com/grid/chosen_poster.png", "", ".png", 100, 600, 900, "Official", "Author", "SteamGridDB");
            selection.SelectedPieces[ArtworkPieceType.Wide] = null; // Skipped
            selection.SelectedPieces[ArtworkPieceType.Icon] = new ArtworkCandidate(
                ArtworkPieceType.Icon, "https://cdn.steamgriddb.com/icon/chosen_icon.ico", "", ".ico", 50, 256, 256, "Custom", "Author", "SteamGridDB");

            var res = service.ApplySelectedArtworkForShortcut(tempGridDir, shortcut, game, selection);

            Assert.Equal(1, res.GamesUpdatedCount);
            Assert.Equal(2, res.ImagesDownloadedCount);
            Assert.True(res.IconsModifiedInShortcuts);

            Assert.False(File.Exists(oldPosterJpg));
            Assert.Equal(posterPayload, File.ReadAllBytes(Path.Combine(tempGridDir, $"{appId}p.png")));
            string expectedIcon = Path.Combine(tempGridDir, $"{appId}_icon.ico");
            Assert.Equal(iconPayload, File.ReadAllBytes(expectedIcon));
            Assert.Equal(expectedIcon, shortcut.Icon);
            Assert.Equal(expectedIcon, game.IconPath);
        }
        finally
        {
            if (Directory.Exists(tempGridDir))
                Directory.Delete(tempGridDir, recursive: true);
        }
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
