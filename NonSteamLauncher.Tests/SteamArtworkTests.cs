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

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
