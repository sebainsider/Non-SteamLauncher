using NonSteamLauncher;
using Xunit;

namespace NonSteamLauncher.Tests;

public class MockProcessProvider : IProcessProvider
{
    public List<ProcessSnapshot> SnapshotsToReturn { get; set; } = new();
    public Dictionary<string, int> InstanceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool LaunchResult { get; set; } = true;
    public string? LastLaunchedCommand { get; private set; }
    public bool CloseLauncherCalled { get; set; }
    public string? CloseLauncherCommandPassed { get; set; }
    public bool KillOverlayCalled { get; set; }

    private int _snapshotIndex = 0;

    public ProcessSnapshot CaptureSnapshot()
    {
        if (SnapshotsToReturn.Count > 0)
        {
            var snapshot = SnapshotsToReturn[Math.Min(_snapshotIndex, SnapshotsToReturn.Count - 1)];
            _snapshotIndex++;

            var filtered = snapshot.Processes
                .Where(kvp => !InstanceCounts.TryGetValue(kvp.Value, out var count) || count > 0)
                .Select(kvp => new ProcessInfo(kvp.Key, kvp.Value))
                .ToList();

            return new ProcessSnapshot(filtered);
        }

        int pid = 100;
        var procs = new List<ProcessInfo>();
        foreach (var (name, count) in InstanceCounts)
        {
            for (int i = 0; i < count; i++)
            {
                procs.Add(new ProcessInfo(pid++, name));
            }
        }
        return new ProcessSnapshot(procs);
    }

    public bool Launch(string commandOrUri)
    {
        LastLaunchedCommand = commandOrUri;
        return LaunchResult;
    }

    public int GetRunningInstanceCount(string processName)
    {
        if (InstanceCounts.TryGetValue(processName, out var count))
        {
            return count;
        }
        return 0;
    }

    public void CloseLauncherProcesses(string launchCommand)
    {
        CloseLauncherCalled = true;
        CloseLauncherCommandPassed = launchCommand;
    }

    public void KillOverlayProcesses()
    {
        KillOverlayCalled = true;
    }
}


public class ProcessTrackerTests
{
    private readonly Logger _logger = new Logger(verbose: false);

    [Fact]
    public async Task RunAsync_AutoDetectsNewGameProcess_AndMonitorsUntilExit()
    {
        var provider = new MockProcessProvider();

        // Initial snapshot before launch
        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher")
        };

        // Snapshot after launch: Epic helper and new game process start
        var afterLaunchProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(21, "EpicWebHelper"),
            new(100, "AlanWake2")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(afterLaunchProcs)
        };

        // Initially AlanWake2 is running (1 instance), then exits (0 instances)
        provider.InstanceCounts["AlanWake2"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        // Simulate process exiting after 600ms
        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_TimesOut_WhenNoGameProcessStarts()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs)
        };

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            SyncDelaySeconds = 0,
            TimeoutSeconds = 1
        };

        int exitCode = await tracker.RunAsync(options);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_ExplicitProcessMode_WaitsForProcessAndSucceeds()
    {
        var provider = new MockProcessProvider();
        provider.InstanceCounts["Cyberpunk2077"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "steam://run/1091500",
            ProcessName = "Cyberpunk2077",
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["Cyberpunk2077"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_IgnoresEpicServicesAndDetectsGame()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system")
        };

        // Epic Launcher and EOS services start alongside game
        var afterLaunchProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(21, "EpicWebHelper"),
            new(22, "EpicOnlineServicesHost"),
            new(23, "EOSOverlayRenderer-Win64-Shipping"),
            new(24, "CrashReportClient-Win64-Shipping"),
            new(25, "EasyAntiCheat_EOS"),
            new(100, "AlanWake2")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(afterLaunchProcs)
        };

        provider.InstanceCounts["AlanWake2"] = 1;
        provider.InstanceCounts["EpicOnlineServicesHost"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        // Game exits, but EpicOnlineServicesHost is still running!
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        // Non-Steam Launcher must succeed when AlanWake2 exits, even if EpicOnlineServicesHost is still running
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithCloseLauncher_CallsCloseLauncherProcesses()
    {
        var provider = new MockProcessProvider();
        provider.InstanceCounts["AlanWake2"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            ProcessName = "AlanWake2",
            CloseLauncher = true,
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
        Assert.True(provider.CloseLauncherCalled);
        Assert.Equal("com.epicgames.launcher://apps/Item?action=launch", provider.CloseLauncherCommandPassed);
    }

    [Fact]
    public async Task RunAsync_TracksMultiStageGameBootstrapper_WithoutEarlyExit()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system")
        };

        // Step 1: Launcher opens bootstrapper (PID 100)
        var step1Procs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(100, "AlanWake2Launcher")
        };

        // Step 2: Bootstrapper spawns real game (PID 101)
        var step2Procs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(100, "AlanWake2Launcher"),
            new(101, "AlanWake2-Win64-Shipping")
        };

        // Step 3: Bootstrapper (PID 100) exits, but real game (PID 101) remains
        var step3Procs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(101, "AlanWake2-Win64-Shipping")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(step1Procs),
            new(step2Procs),
            new(step3Procs)
        };

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(1200);

        // After 1.2s, step 3 is reached. Real game (PID 101) is still alive.
        // Signal real game exit by setting empty snapshot
        provider.SnapshotsToReturn.Add(new ProcessSnapshot(new[] { new ProcessInfo(10, "system"), new ProcessInfo(20, "EpicGamesLauncher") }));

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithDisableOverlay_CallsKillOverlayProcesses()
    {
        var provider = new MockProcessProvider();
        provider.InstanceCounts["AlanWake2"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            ProcessName = "AlanWake2",
            DisableOverlay = true,
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
        Assert.True(provider.KillOverlayCalled);
    }

    [Fact]
    public async Task RunAsync_DirectExe_ExitsCleanlyEvenWhenBackgroundAppsSpawnAndStayRunning()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "GalaxyClient")
        };

        var duringGameProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "GalaxyClient"),
            new(100, "witcher3"),
            new(200, "GameBar"),
            new(201, "XboxGameBarWidgets"),
            new(202, "RuntimeBroker"),
            new(203, "msedge"),
            new(204, "nvngx_update"),
            new(205, "FileCoAuth"),
            new(206, "git")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(duringGameProcs)
        };

        provider.InstanceCounts["witcher3"] = 1;
        provider.InstanceCounts["GameBar"] = 1;
        provider.InstanceCounts["XboxGameBarWidgets"] = 1;
        provider.InstanceCounts["RuntimeBroker"] = 1;
        provider.InstanceCounts["msedge"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = @"C:\Games\The Witcher 3 Wild Hunt GOTY\bin\x64_dx12\witcher3.exe",
            CloseLauncher = true,
            DisableOverlay = true,
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);

        // Witcher 3 exits, while GameBar, RuntimeBroker, msedge remain running
        provider.InstanceCounts["witcher3"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
        // Direct DRM-free .exe must NOT close GOG Galaxy or any other open launchers
        Assert.False(provider.CloseLauncherCalled);
    }

    [Fact]
    public async Task RunAsync_WithoutCloseLauncherFlag_DoesNotCloseLauncher()
    {
        var provider = new MockProcessProvider();
        provider.InstanceCounts["AlanWake2"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            ProcessName = "AlanWake2",
            CloseLauncher = false,
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
        Assert.False(provider.CloseLauncherCalled);
    }

    [Fact]
    public void GetLauncherProcessNames_MatchesOnlySpecificLauncher_AndEmptyForDirectExe()
    {
        var epicProcs = DefaultProcessProvider.GetLauncherProcessNames("com.epicgames.launcher://apps/Item?action=launch");
        Assert.Contains("EpicGamesLauncher", epicProcs);
        Assert.Contains("EpicGamesLauncherUpdate", epicProcs);
        Assert.Contains("EpicGamesLauncherMigrate", epicProcs);
        Assert.Contains("EpicGamesLauncherSetup", epicProcs);
        Assert.Contains("UnrealVersionSelector", epicProcs);
        Assert.DoesNotContain("GalaxyClient", epicProcs);
        Assert.DoesNotContain("EADesktop", epicProcs);

        var eaProcs = DefaultProcessProvider.GetLauncherProcessNames("origin2://game/launch?offerIds=123");
        Assert.Contains("EADesktop", eaProcs);
        Assert.DoesNotContain("EpicGamesLauncher", eaProcs);
        Assert.DoesNotContain("GalaxyClient", eaProcs);

        var gogUriProcs = DefaultProcessProvider.GetLauncherProcessNames("goggalaxy://openGameView/1495134320");
        Assert.Contains("GalaxyClient", gogUriProcs);
        Assert.DoesNotContain("EpicGamesLauncher", gogUriProcs);

        // Direct DRM-free executables should never close any launcher (including paths containing "ea" like Dead Space)
        Assert.Empty(DefaultProcessProvider.GetLauncherProcessNames(@"C:\Games\The Witcher 3 Wild Hunt GOTY\bin\x64_dx12\witcher3.exe"));
        Assert.Empty(DefaultProcessProvider.GetLauncherProcessNames(@"C:\Games\Dead Space\DeadSpace.exe"));

        // Smart LaunchOptions builder
        Assert.Contains("--close-launcher", NonSteamLauncher.Sync.Steam.SteamSyncService.BuildLaunchOptions("com.epicgames.launcher://apps/Item?action=launch"));
        Assert.DoesNotContain("--close-launcher", NonSteamLauncher.Sync.Steam.SteamSyncService.BuildLaunchOptions(@"C:\Games\The Witcher 3 Wild Hunt GOTY\bin\x64_dx12\witcher3.exe"));
    }

    [Fact]
    public async Task RunAsync_EpicUri_ExitsAndClosesLauncher_WhenUnrelatedProcessesStartDuringSession()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher")
        };

        var gameStartedProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(21, "EpicGamesLauncherUpdate"),
            new(22, "msedgewebview2"),
            new(100, "AlanWake2")
        };

        // Unrelated background apps start while the game is running
        var duringGameWithUnrelatedProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(100, "AlanWake2"),
            new(200, "python"),
            new(201, "clang"),
            new(202, "OAWrapper"),
            new(203, "Antigravity"),
            new(204, "SpatialAudioLicenseSrv"),
            new(205, "taskkill")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(gameStartedProcs),
            new(duringGameWithUnrelatedProcs)
        };

        provider.InstanceCounts["AlanWake2"] = 1;
        provider.InstanceCounts["python"] = 1;
        provider.InstanceCounts["Antigravity"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/UnmappedItem?action=launch&silent=true",
            CloseLauncher = true,
            DisableOverlay = true,
            SyncDelaySeconds = 0,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(1200);

        // AlanWake2 exits, while python and Antigravity remain running
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
        Assert.True(provider.CloseLauncherCalled);
        Assert.Equal(options.LaunchCommand, provider.CloseLauncherCommandPassed);
    }

    [Theory]
    [InlineData(@"C:\Games\The Witcher 3 Wild Hunt GOTY\bin\x64_dx12\witcher3.exe", "witcher3")]
    [InlineData(@"""C:\Games\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe""", "Cyberpunk2077")]
    [InlineData("com.epicgames.launcher://apps/Item?action=launch", null)]
    [InlineData(@"C:\Games\The Witcher 3\REDprelauncher.exe", null)]
    [InlineData(@"C:\Games\SomeGame\GameLauncher.exe", null)]
    public void InferTargetProcessName_ReturnsExpectedProcessName(string command, string? expected)
    {
        Assert.Equal(expected, ProcessTracker.InferTargetProcessName(command));
    }
}




