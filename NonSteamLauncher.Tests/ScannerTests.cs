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
    [InlineData("xbox:launch?titleId=123", "Xbox")]
    [InlineData("ms-xbl-712398://launch", "Xbox")]
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

    [Fact]
    public void EAAppScanner_DiscoversGameFromRegistry()
    {
        var mockRegistry = new MockRegistryReader();
        mockRegistry.AddSubKey(@"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Games", "Unravel Two");
        mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Unravel Two", "Install Dir", @"C:\Program Files\EA Games\Unravel Two");
        mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Unravel Two", "DisplayName", "Unravel Two");

        var scanner = new EAAppScanner(mockRegistry);
        Assert.Equal("EA App", scanner.ScannerName);
    }

    [Fact]
    public void EAAppScanner_ParseInstallerDataXml_ExtractsNonTrialLauncherAndTitle()
    {
        string xml = """
        <?xml version='1.0' encoding='utf-8'?>
        <DiPManifest version="4.0">
          <contentIDs>
            <contentID>1035049</contentID>
          </contentIDs>
          <gameTitles>
            <gameTitle locale="en_US">UNRAVEL™ two</gameTitle>
          </gameTitles>
          <runtime>
            <launcher uid="1-2">
              <name locale="en_US">UNRAVEL™ two</name>
              <filePath>[HKEY_LOCAL_MACHINE\SOFTWARE\Coldwood\UnravelTwo\Install Dir]UnravelTwo.exe</filePath>
              <trial>0</trial>
            </launcher>
            <launcher uid="2-2">
              <name locale="en_US">UNRAVEL™ two Trial</name>
              <filePath>[HKEY_LOCAL_MACHINE\SOFTWARE\Coldwood\UnravelTwo\Install Dir]UnravelTwo_trial.exe</filePath>
              <trial>1</trial>
            </launcher>
          </runtime>
        </DiPManifest>
        """;

        var mockRegistry = new MockRegistryReader();
        mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Origin Games\1035049", "DisplayName", "Unravel Two");

        var (title, exePath, contentId) = EAAppScanner.ParseInstallerDataXml(xml, @"C:\Games\UnravelTwo", mockRegistry);

        Assert.Equal("Unravel Two", title);
        Assert.Equal(@"C:\Games\UnravelTwo\UnravelTwo.exe", exePath);
        Assert.Equal("1035049", contentId);
    }

    [Fact]
    public void EAAppScanner_DiscoversGameViaUninstallRegistryAndManifest()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "EAAppScannerTest_" + Guid.NewGuid().ToString("N"));
        string gameDir = Path.Combine(tempDir, "UnravelTwo");
        string installerDir = Path.Combine(gameDir, "__Installer");
        Directory.CreateDirectory(installerDir);

        try
        {
            string exePath = Path.Combine(gameDir, "UnravelTwo.exe");
            string trialPath = Path.Combine(gameDir, "UnravelTwo_trial.exe");
            File.WriteAllText(exePath, "main_exe");
            File.WriteAllText(trialPath, "trial_exe_much_larger_content_1234567890");

            File.WriteAllText(Path.Combine(installerDir, "installerdata.xml"), """
            <?xml version='1.0' encoding='utf-8'?>
            <DiPManifest version="4.0">
              <contentIDs><contentID>1035049</contentID></contentIDs>
              <gameTitles><gameTitle locale="en_US">UNRAVEL™ two</gameTitle></gameTitles>
              <runtime>
                <launcher uid="1-2">
                  <filePath>[HKEY_LOCAL_MACHINE\SOFTWARE\Coldwood\UnravelTwo\Install Dir]UnravelTwo.exe</filePath>
                  <trial>0</trial>
                </launcher>
                <launcher uid="2-2">
                  <filePath>[HKEY_LOCAL_MACHINE\SOFTWARE\Coldwood\UnravelTwo\Install Dir]UnravelTwo_trial.exe</filePath>
                  <trial>1</trial>
                </launcher>
              </runtime>
            </DiPManifest>
            """);

            var mockRegistry = new MockRegistryReader();
            mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Origin Games\1035049", "DisplayName", "Unravel Two");
            mockRegistry.AddSubKey(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "{5DB117FE-6F05-40AC-B7A3-5C67641F14C0}");
            mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{5DB117FE-6F05-40AC-B7A3-5C67641F14C0}", "DisplayName", "UNRAVEL™ two");
            mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{5DB117FE-6F05-40AC-B7A3-5C67641F14C0}", "InstallLocation", gameDir);
            mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{5DB117FE-6F05-40AC-B7A3-5C67641F14C0}", "Publisher", "Electronic Arts, Inc.");
            mockRegistry.AddValue(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{5DB117FE-6F05-40AC-B7A3-5C67641F14C0}", "UninstallString", @"""C:\Program Files\Common Files\EAInstaller\UnravelTwo\Cleanup.exe"" uninstall_game");

            var scanner = new EAAppScanner(mockRegistry, customDirectories: new[] { tempDir });
            var games = scanner.Scan();

            Assert.Single(games);
            Assert.Equal("Unravel Two", games[0].Title);
            Assert.Equal("EA App", games[0].Launcher);
            Assert.Equal(exePath, games[0].ExecutablePath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData(@"C:\Program Files\EA Games\Unravel Two\UnravelTwo.exe", null, "EA App")]
    [InlineData(@"D:\Origin Games\Dead Space\DeadSpace.exe", null, "EA App")]
    [InlineData(@"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe", null, null)]
    [InlineData(@"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Far Cry 6\FarCry6.exe", null, "Ubisoft Connect")]
    [InlineData(@"C:\Games\Epic Games\AlanWake2\AlanWake2.exe", null, "Epic Games")]
    [InlineData(@"C:\GOG Games\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe", null, "GOG Galaxy")]
    [InlineData(@"C:\Program Files (x86)\Battle.net\Games\Diablo IV\Diablo IV.exe", null, "Battle.net")]
    [InlineData(@"C:\XboxGames\Forza Horizon 5\gamelaunchhelper.exe", null, "Xbox")]
    [InlineData(@"D:\XboxGames\Lies of P\Content\LiesOfP.exe", null, "Xbox")]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.SeaofThieves_8wekyb3d8bbwe\SoTGame.exe", null, "Xbox")]
    [InlineData(@"C:\Emulators\RetroArch\retroarch.exe", null, null)]
    public void DetectLauncherFromPath_IdentifiesLaunchersCorrectly(string exe, string? startDir, string? expectedLauncher)
    {
        string? result = NonSteamLauncher.Sync.Steam.SteamSyncService.DetectLauncherFromPath(exe, startDir);
        Assert.Equal(expectedLauncher, result);
    }

    [Fact]
    public void XboxScanner_ParseMicrosoftGameConfigXml_ExtractsMetadataSuccessfully()
    {
        string xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Game configVersion="1">
            <Identity Name="Microsoft.624F8B84B80" Publisher="CN=Pocketpair" Version="1.0.0.0"/>
            <ExecutableList>
                <Executable Name="Content\Palworld.exe" TargetDeviceFamily="PC" Id="Game"/>
            </ExecutableList>
            <ShellVisuals DefaultDisplayName="Palworld"
                          Square150x150Logo="Square150x150Logo.png"
                          Square44x44Logo="Square44x44Logo.png"
                          Description="Palworld"
                          ForegroundText="light"
                          BackgroundColor="#000000"
                          SplashScreenImage="SplashScreenImage.png"
                          StoreLogo="StoreLogo.png"/>
        </Game>
        """;

        var (title, exe, logo) = XboxScanner.ParseMicrosoftGameConfigXml(xml);

        Assert.Equal("Palworld", title);
        Assert.Equal(@"Content\Palworld.exe", exe);
        Assert.Equal("Square150x150Logo.png", logo);
    }

    [Fact]
    public void XboxScanner_ParseMicrosoftGameConfigXml_FallsBackWhenResourceTitle()
    {
        string xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Game configVersion="1">
            <ExecutableList>
                <Executable Name="Game.exe" />
            </ExecutableList>
            <ShellVisuals DefaultDisplayName="ms-resource:AppName" StoreLogo="Logo.png"/>
        </Game>
        """;

        var (title, exe, logo) = XboxScanner.ParseMicrosoftGameConfigXml(xml);

        Assert.Null(title); // Should fallback to folder name in caller
        Assert.Equal("Game.exe", exe);
        Assert.Equal("Logo.png", logo);
    }

    [Fact]
    public void XboxScanner_ParseXboxGameFolder_PrefersGameLaunchHelper()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "XboxScannerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string helperPath = Path.Combine(tempDir, "gamelaunchhelper.exe");
            File.WriteAllText(helperPath, "stub");

            string configPath = Path.Combine(tempDir, "MicrosoftGame.config");
            File.WriteAllText(configPath, """
            <Game configVersion="1">
                <ExecutableList><Executable Name="Game.exe"/></ExecutableList>
                <ShellVisuals DefaultDisplayName="Hi-Fi RUSH" StoreLogo="StoreLogo.png"/>
            </Game>
            """);

            string logoPath = Path.Combine(tempDir, "StoreLogo.png");
            File.WriteAllText(logoPath, "dummy_png");

            var game = XboxScanner.ParseXboxGameFolder(tempDir);

            Assert.NotNull(game);
            Assert.Equal("Hi-Fi RUSH", game.Title);
            Assert.Equal("Xbox", game.Launcher);
            Assert.Equal(helperPath, game.LaunchUri);
            Assert.Equal(helperPath, game.ExecutablePath);
            Assert.Equal(logoPath, game.IconPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
