using NonSteamLauncher.Sync;
using Xunit;

namespace NonSteamLauncher.Tests;

public class ScannerTests
{
    private class MockRegistryReader : IRegistryReader
    {
        private readonly Dictionary<string, List<string>> _subKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string, string), string> _values = new();

        public void AddSubKey(string parent, string subKey)
        {
            if (!_subKeys.TryGetValue(parent, out var list))
            {
                list = new List<string>();
                _subKeys[parent] = list;
            }
            list.Add(subKey);
        }

        public void AddValue(string key, string valueName, string valueData)
        {
            _values[(key, valueName)] = valueData;
        }

        public string[] GetSubKeyNames(string keyPath)
        {
            return _subKeys.TryGetValue(keyPath, out var list) ? list.ToArray() : Array.Empty<string>();
        }

        public string? GetValue(string keyPath, string valueName)
        {
            return _values.TryGetValue((keyPath, valueName), out var data) ? data : null;
        }
    }

    [Fact]
    public void EpicGamesScanner_ParseManifestJson_ValidGame()
    {
        string json = """
        {
            "FormatVersion": 0,
            "AppVersionString": "1.0.0",
            "AppName": "Frogmores",
            "CatalogItemId": "5f6c8d37a1f54460a5e8f49ef2c4a9a0",
            "CatalogNamespace": "6f438871317448e8a83d42042079148d",
            "DisplayName": "Alan Wake 2",
            "InstallationGuid": "{GUID}",
            "InstallLocation": "C:\\Games\\AlanWake2",
            "LaunchExecutable": "AlanWake2.exe",
            "bIsEngineInstalled": false
        }
        """;

        var game = EpicGamesScanner.ParseManifestJson(json);

        Assert.NotNull(game);
        Assert.Equal("Alan Wake 2", game.Title);
        Assert.Equal("Epic Games", game.Launcher);
        Assert.Equal("com.epicgames.launcher://apps/6f438871317448e8a83d42042079148d%3A5f6c8d37a1f54460a5e8f49ef2c4a9a0%3AFrogmores?action=launch&silent=true", game.LaunchUri);
        Assert.Equal("C:\\Games\\AlanWake2\\AlanWake2.exe", game.ExecutablePath);
    }

    [Fact]
    public void EpicGamesScanner_ParseManifestJson_IgnoresEngineInstallation()
    {
        string json = """
        {
            "AppName": "UE_5.3",
            "DisplayName": "Unreal Engine 5.3",
            "bIsEngineInstalled": true
        }
        """;

        var game = EpicGamesScanner.ParseManifestJson(json);
        Assert.Null(game);
    }

    [Theory]
    [InlineData("com.epicgames.launcher://apps/Item?action=launch", "Epic Games")]
    [InlineData("uplay://launch/5105/0", "Ubisoft Connect")]
    [InlineData("origin2://game/launch?offerIds=1000", "EA App")]
    [InlineData("battlenet://Fen", "Battle.net")]
    [InlineData("goggalaxy://openGameView/123", "GOG Galaxy")]
    [InlineData("https://www.google.com", null)]
    public void DesktopShortcutScanner_DetectLauncherFromUri(string uri, string? expectedLauncher)
    {
        string? launcher = DesktopShortcutScanner.DetectLauncherFromUri(uri);
        Assert.Equal(expectedLauncher, launcher);
    }

    [Fact]
    public void DesktopShortcutScanner_ParseUrlLines_ParsesCorrectly()
    {
        var lines = new[]
        {
            "[{000214A0-0000-0000-C000-000000000046}]",
            "Prop3=19,0",
            "[InternetShortcut]",
            "IDList=",
            "URL=battlenet://Fen",
            "IconIndex=0"
        };

        var game = DesktopShortcutScanner.ParseUrlLines("Diablo IV", lines);

        Assert.NotNull(game);
        Assert.Equal("Diablo IV", game.Title);
        Assert.Equal("Battle.net", game.Launcher);
        Assert.Equal("battlenet://Fen", game.LaunchUri);
    }

    [Fact]
    public void GameDetector_DeduplicateGames_MergesAndKeepsRicherMetadata()
    {
        var games = new List<InstalledGame>
        {
            new()
            {
                Title = "Alan Wake 2",
                Launcher = "Epic Games",
                LaunchUri = "com.epicgames.launcher://apps/Item?action=launch",
                ExecutablePath = "C:\\Games\\AlanWake2\\AlanWake2.exe"
            },
            new()
            {
                Title = "Alan Wake 2", // From desktop shortcut with no exe path
                Launcher = "Epic Games",
                LaunchUri = "com.epicgames.launcher://apps/Item?action=launch",
                ExecutablePath = null
            },
            new()
            {
                Title = "Diablo IV",
                Launcher = "Battle.net",
                LaunchUri = "battlenet://Fen"
            }
        };

        var deduplicated = GameDetector.DeduplicateGames(games);

        Assert.Equal(2, deduplicated.Count);
        var aw2 = deduplicated.First(g => g.Title == "Alan Wake 2");
        Assert.Equal("C:\\Games\\AlanWake2\\AlanWake2.exe", aw2.ExecutablePath);
    }

    [Fact]
    public void UbisoftScanner_DiscoversGameFromRegistry()
    {
        var mockRegistry = new MockRegistryReader();
        mockRegistry.AddSubKey(@"HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", "5105");
        mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\5105", "InstallDir", @"C:\Games\Far Cry 6");

        var scanner = new UbisoftScanner(mockRegistry);
        var games = scanner.Scan();

        Assert.Single(games);
        Assert.Equal("Far Cry 6", games[0].Title);
        Assert.Equal("Ubisoft Connect", games[0].Launcher);
        Assert.Equal("uplay://launch/5105/0", games[0].LaunchUri);
    }

    [Fact]
    public void GogScanner_DiscoversGameFromRegistry()
    {
        var mockRegistry = new MockRegistryReader();
        mockRegistry.AddSubKey(@"HKLM\SOFTWARE\WOW6432Node\GOG.com\Games", "1423");
        mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\1423", "gameName", "Cyberpunk 2077");
        mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\1423", "exe", "Cyberpunk2077.exe");
        mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\1423", "path", @"C:\GOG Games\Cyberpunk 2077");

        var scanner = new GogScanner(mockRegistry);
        var games = scanner.Scan();

        Assert.Single(games);
        Assert.Equal("Cyberpunk 2077", games[0].Title);
        Assert.Equal("GOG Galaxy", games[0].Launcher);
        Assert.Contains("Cyberpunk2077.exe", games[0].LaunchUri);
    }
}
