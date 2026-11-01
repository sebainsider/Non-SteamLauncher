using System.Collections.Generic;
using System.Threading;
using NonSteamLauncher.Sync;
using NonSteamLauncher.Sync.Steam;
using NonSteamLauncher.Sync.UI;
using Xunit;

namespace NonSteamLauncher.Tests;

public class SyncStatusWindowTests
{
    [Fact]
    public void SyncStatusWindow_CanInstantiateAndShowOnSTAThread()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var result = new SteamSyncResult
                {
                    Success = true,
                    PendingGames = new List<InstalledGame>
                    {
                        new InstalledGame
                        {
                            Title = "Test Game",
                            Launcher = "Epic Games",
                            InstallDirectory = "C:\\Games\\Test",
                            ExecutablePath = "C:\\Games\\Test\\game.exe"
                        }
                    }
                };
                var win = new SyncStatusWindow(result);
                Assert.NotNull(win);
                win.Show();
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
            throw new Exception($"SyncStatusWindow failed during Show/Close: {error}", error);
        }
    }

    [Fact]
    public void SyncStatusWindow_AllGamesSynced_ShowsAndClosesProperly()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var result = new SteamSyncResult
                {
                    Success = true,
                    PendingGames = new List<InstalledGame>(),
                    ExistingGames = new List<InstalledGame>
                    {
                        new InstalledGame { Title = "Game 1", Launcher = "Epic Games" },
                        new InstalledGame { Title = "Game 2", Launcher = "EA App" },
                        new InstalledGame { Title = "Game 3", Launcher = "GOG Galaxy" },
                        new InstalledGame { Title = "Game 4", Launcher = "GOG Galaxy" }
                    }
                };
                var win = new SyncStatusWindow(result);
                Assert.NotNull(win);
                win.Show();
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
            throw new Exception($"SyncStatusWindow all games synced failed: {error}", error);
        }
    }
}
