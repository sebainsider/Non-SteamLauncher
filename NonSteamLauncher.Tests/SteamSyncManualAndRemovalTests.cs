using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NonSteamLauncher.Sync;
using NonSteamLauncher.Sync.Steam;
using NonSteamLauncher.Sync.UI;
using Xunit;

namespace NonSteamLauncher.Tests;

[Collection("WpfUi")]
public class SteamSyncManualAndRemovalTests
{
    private sealed class FakeRegistry : IRegistryReader
    {
        public string? GetValue(string keyPath, string valueName) => null;
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public (string? DisplayName, string? InstallLocation) GetUninstallInfo(string subKeyName) => (null, null);
    }

    [Fact]
    public void Inspect_DiscoversManualExternalShortcuts_InShortcutsVdf()
    {
        string tempSteamDir = Path.Combine(Path.GetTempPath(), $"nsl_steam_test_{Guid.NewGuid():N}");
        string profileDir = Path.Combine(tempSteamDir, "userdata", "12345678");
        string configDir = Path.Combine(profileDir, "config");
        Directory.CreateDirectory(configDir);

        try
        {
            var initialShortcuts = new List<SteamShortcut>
            {
                new SteamShortcut
                {
                    AppName = "Hollow Knight",
                    Exe = "\"C:\\Games\\HollowKnight\\hollow_knight.exe\"",
                    StartDir = "\"C:\\Games\\HollowKnight\"",
                    AppId = 2147589001,
                    Tags = new List<string> { "Indie" }
                },
                new SteamShortcut
                {
                    AppName = "Dolphin Emulator",
                    Exe = "C:\\Emulators\\Dolphin.exe",
                    StartDir = "C:\\Emulators",
                    AppId = 2147589002,
                    Tags = new List<string>() // Empty tags -> should default to "Manual"
                }
            };

            byte[] vdfData = VdfBinaryWriter.WriteShortcuts(initialShortcuts);
            File.WriteAllBytes(Path.Combine(configDir, "shortcuts.vdf"), vdfData);

            var locator = new SteamLocator(new FakeRegistry(), new Logger(false));
            var syncService = new SteamSyncService(steamLocator: locator, logger: new Logger(false));

            var options = new Options
            {
                SteamPath = tempSteamDir
            };

            var inspectResult = syncService.Inspect(options);

            Assert.True(inspectResult.Success);
            Assert.Equal(2, inspectResult.ExistingGames.Count);

            var hollowKnight = inspectResult.ExistingGames.FirstOrDefault(g => g.Title == "Hollow Knight");
            Assert.NotNull(hollowKnight);
            Assert.Equal("Indie", hollowKnight.Launcher);
            Assert.True(hollowKnight.IsManualShortcut);
            Assert.Equal("C:\\Games\\HollowKnight\\hollow_knight.exe", hollowKnight.ExecutablePath);

            var dolphin = inspectResult.ExistingGames.FirstOrDefault(g => g.Title == "Dolphin Emulator");
            Assert.NotNull(dolphin);
            Assert.Equal("Manual", dolphin.Launcher);
            Assert.True(dolphin.IsManualShortcut);
            Assert.Equal("C:\\Emulators\\Dolphin.exe", dolphin.ExecutablePath);
        }
        finally
        {
            if (Directory.Exists(tempSteamDir))
            {
                Directory.Delete(tempSteamDir, recursive: true);
            }
        }
    }

    [Fact]
    public void RemoveShortcut_DeletesShortcutAndArtwork_FromDisk()
    {
        string tempSteamDir = Path.Combine(Path.GetTempPath(), $"nsl_steam_remove_test_{Guid.NewGuid():N}");
        string profileDir = Path.Combine(tempSteamDir, "userdata", "98765432");
        string configDir = Path.Combine(profileDir, "config");
        string gridDir = Path.Combine(configDir, "grid");
        Directory.CreateDirectory(gridDir);

        try
        {
            uint appId = 2233445566;
            var initialShortcuts = new List<SteamShortcut>
            {
                new SteamShortcut
                {
                    AppName = "Game to Remove",
                    Exe = "\"C:\\Games\\RemoveMe\\game.exe\"",
                    StartDir = "\"C:\\Games\\RemoveMe\"",
                    AppId = appId,
                    Tags = new List<string> { "Manual" }
                },
                new SteamShortcut
                {
                    AppName = "Game to Keep",
                    Exe = "\"C:\\Games\\KeepMe\\game.exe\"",
                    StartDir = "\"C:\\Games\\KeepMe\"",
                    AppId = 3344556677,
                    Tags = new List<string> { "Manual" }
                }
            };

            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            File.WriteAllBytes(shortcutsPath, VdfBinaryWriter.WriteShortcuts(initialShortcuts));

            // Create fake artwork in grid directory
            string posterPath = Path.Combine(gridDir, $"{appId}p.jpg");
            string bannerPath = Path.Combine(gridDir, $"{appId}.jpg");
            string heroPath = Path.Combine(gridDir, $"{appId}_hero.png");
            string logoPath = Path.Combine(gridDir, $"{appId}_logo.png");
            string keepPosterPath = Path.Combine(gridDir, "3344556677p.jpg");

            File.WriteAllText(posterPath, "dummy poster");
            File.WriteAllText(bannerPath, "dummy banner");
            File.WriteAllText(heroPath, "dummy hero");
            File.WriteAllText(logoPath, "dummy logo");
            File.WriteAllText(keepPosterPath, "keep poster");

            var locator = new SteamLocator(new FakeRegistry(), new Logger(false));
            var syncService = new SteamSyncService(steamLocator: locator, logger: new Logger(false));

            var options = new Options
            {
                SteamPath = tempSteamDir
            };

            var toRemove = new InstalledGame
            {
                Title = "Game to Remove",
                AppId = appId
            };

            var removeResult = syncService.RemoveShortcut(options, toRemove);

            Assert.Equal(1, removeResult.RemovedShortcutsCount);
            Assert.Single(removeResult.RemovedGames);
            Assert.Equal("Game to Remove", removeResult.RemovedGames[0].Title);

            // Verify shortcuts.vdf on disk
            var updatedShortcuts = VdfBinaryReader.ReadShortcuts(File.ReadAllBytes(shortcutsPath));
            Assert.Single(updatedShortcuts);
            Assert.Equal("Game to Keep", updatedShortcuts[0].AppName);

            // Verify backup exists
            Assert.True(File.Exists(Path.Combine(configDir, "shortcuts.vdf.bak")));

            // Verify artwork for removed game is deleted
            Assert.False(File.Exists(posterPath));
            Assert.False(File.Exists(bannerPath));
            Assert.False(File.Exists(heroPath));
            Assert.False(File.Exists(logoPath));

            // Verify artwork for kept game still exists
            Assert.True(File.Exists(keepPosterPath));
        }
        finally
        {
            if (Directory.Exists(tempSteamDir))
            {
                Directory.Delete(tempSteamDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SyncStatusWindow_RequestDeleteGame_OpensDialogAndExecutesCallback()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var existingGame = new InstalledGame
                {
                    Title = "Custom Game",
                    Launcher = "Manual",
                    ExecutablePath = "C:\\Games\\Custom.exe",
                    AppId = 123456
                };

                var result = new SteamSyncResult
                {
                    Success = true,
                    ExistingGames = new List<InstalledGame> { existingGame }
                };

                bool removedCallbackFired = false;
                var win = new SyncStatusWindow(
                    result,
                    onRemoveGame: g =>
                    {
                        removedCallbackFired = true;
                        return new SteamSyncResult { Success = true, ExistingGames = new List<InstalledGame>() };
                    });

                win.Show();

                Assert.Single(win.GameItems);
                var item = win.GameItems[0];
                Assert.True(item.IsSynced);

                // Request delete
                win.RequestDeleteGame(item);
                Assert.True(win.IsDeleteDialogVisible);

                // Confirm delete
                win.ConfirmDelete();
                Assert.False(win.IsDeleteDialogVisible);
                Assert.True(removedCallbackFired);

                win.Close();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw new Exception($"SyncStatusWindow delete test failed: {error}", error);
        }
    }
}
