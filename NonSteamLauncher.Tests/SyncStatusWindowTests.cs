using System.Collections.Generic;
using System.Threading;
using NonSteamLauncher.Sync;
using NonSteamLauncher.Sync.Steam;
using NonSteamLauncher.Sync.UI;
using Xunit;

namespace NonSteamLauncher.Tests;

[Collection("WpfUi")]
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

    [Fact]
    public void GameCardItem_IsSyncing_ControlsVisibilitiesCorrectly()
    {
        var game = new InstalledGame { Title = "Test Game", Launcher = "Epic Games" };
        var item = new GameCardItem
        {
            Game = game,
            Title = game.Title,
            LauncherName = game.Launcher,
            BrandBrush = System.Windows.Media.Brushes.Blue,
            IconBgBrush = System.Windows.Media.Brushes.Transparent,
            IsSynced = false
        };

        // Initially unsynced and not syncing
        Assert.False(item.IsSyncing);
        Assert.Equal(System.Windows.Visibility.Collapsed, item.SyncingVisibility);
        Assert.Equal(System.Windows.Visibility.Visible, item.NormalStatusVisibility);
        Assert.Equal(System.Windows.Visibility.Visible, item.CheckBoxVisibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, item.DeleteButtonVisibility);

        // Transition to syncing
        item.IsSyncing = true;
        Assert.Equal(System.Windows.Visibility.Visible, item.SyncingVisibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, item.NormalStatusVisibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, item.CheckBoxVisibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, item.DeleteButtonVisibility);

        // Transition to synced (sync completed)
        item.IsSyncing = false;
        item.IsSynced = true;
        Assert.Equal(System.Windows.Visibility.Collapsed, item.SyncingVisibility);
        Assert.Equal(System.Windows.Visibility.Visible, item.NormalStatusVisibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, item.CheckBoxVisibility);
        Assert.Equal(System.Windows.Visibility.Visible, item.DeleteButtonVisibility);
    }

    [Fact]
    public void SyncStatusWindow_SyncingVisualElements_RenderAndCloseProperly()
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
                            Title = "The Witcher 3: Wild Hunt",
                            Launcher = "GOG Galaxy",
                            InstallDirectory = "C:\\Games\\The Witcher 3",
                            ExecutablePath = "C:\\Games\\The Witcher 3\\bin\\x64_dx12\\witcher3.exe"
                        }
                    }
                };
                var win = new SyncStatusWindow(result);
                Assert.NotNull(win);
                Assert.Single(win.GameItems);

                // Set item to syncing to verify XAML template with spinning storyboard builds and runs
                win.GameItems[0].IsSyncing = true;
                Assert.Equal(System.Windows.Visibility.Visible, win.GameItems[0].SyncingVisibility);
                Assert.Equal(System.Windows.Visibility.Collapsed, win.GameItems[0].NormalStatusVisibility);

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
            throw new Exception($"SyncStatusWindow syncing visual elements failed: {error}", error);
        }
    }

    [Fact]
    public void SyncStatusWindow_IsSyncingProperty_ChangesNotifyView()
    {
        var game = new InstalledGame { Title = "Cyberpunk 2077", Launcher = "GOG Galaxy" };
        var item = new GameCardItem
        {
            Game = game,
            Title = game.Title,
            LauncherName = game.Launcher,
            BrandBrush = System.Windows.Media.Brushes.Purple,
            IconBgBrush = System.Windows.Media.Brushes.Transparent,
            IsSynced = false
        };

        var propChanges = new List<string>();
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null) propChanges.Add(e.PropertyName);
        };

        item.IsSyncing = true;
        Assert.Contains(nameof(GameCardItem.IsSyncing), propChanges);
        Assert.Contains(nameof(GameCardItem.SyncingVisibility), propChanges);
        Assert.Contains(nameof(GameCardItem.NormalStatusVisibility), propChanges);
        Assert.Contains(nameof(GameCardItem.CheckBoxVisibility), propChanges);

        item.SyncSpinAngle = 45.0;
        Assert.Contains(nameof(GameCardItem.SyncSpinAngle), propChanges);
    }

    [Fact]
    public void ArtworkPickerWindow_Displays4OptionsPerPieceAndReturnsUserSelection()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var game = new InstalledGame { Title = "Hades", Launcher = "Epic Games", AppId = 555666 };
                var opt = new GameArtworkOptions { Game = game, AppId = 555666 };
                for (int i = 1; i <= 4; i++)
                {
                    opt.Pieces[ArtworkPieceType.Poster].Add(new ArtworkCandidate(
                        ArtworkPieceType.Poster, $"https://example.invalid/p{i}.png", "", ".png", 100 - i * 10, 600, 900, "Official", "User", "SteamGridDB"));
                    opt.Pieces[ArtworkPieceType.Hero].Add(new ArtworkCandidate(
                        ArtworkPieceType.Hero, $"https://example.invalid/h{i}.png", "", ".png", 80 - i * 10, 1920, 620, "Alternate", "User", "SteamGridDB"));
                }

                var picker = new ArtworkPickerWindow(new[] { opt }, Localization.GetStrings("es"));
                picker.Show();

                Assert.Equal(ArtworkPieceType.Poster, picker.CurrentPieceType);
                Assert.Equal(4, picker.CurrentOptionCount);

                // User selects option #3 (index 2) for Poster and option #2 (index 1) for Hero
                picker.SelectOptionIndex(2);
                picker.SelectPiece(ArtworkPieceType.Hero);
                Assert.Equal(4, picker.CurrentOptionCount);
                picker.SelectOptionIndex(1);

                var selections = picker.GetSelections();
                Assert.Single(selections);
                Assert.Equal("https://example.invalid/p3.png", selections[0].SelectedPieces[ArtworkPieceType.Poster]?.Url);
                Assert.Equal("https://example.invalid/h2.png", selections[0].SelectedPieces[ArtworkPieceType.Hero]?.Url);
                Assert.Null(selections[0].SelectedPieces[ArtworkPieceType.Wide]);

                picker.Close();
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
            throw new Exception($"ArtworkPickerWindow test failed: {error}", error);
    }
}
