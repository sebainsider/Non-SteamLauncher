using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NonSteamLauncher;

public interface IProcessProvider
{
    ProcessSnapshot CaptureSnapshot();
    bool Launch(string commandOrUri);
    int GetRunningInstanceCount(string processName);
    void CloseLauncherProcesses(string launchCommand);
    void KillOverlayProcesses();
}

public class DefaultProcessProvider : IProcessProvider
{
    private readonly Logger _logger;

    public DefaultProcessProvider(Logger logger)
    {
        _logger = logger;
    }

    public ProcessSnapshot CaptureSnapshot()
    {
        return ProcessSnapshot.Capture();
    }

    public bool Launch(string commandOrUri)
    {
        try
        {
            string cleanCommand = commandOrUri.Trim('"', '\'');

            // Pre-launch check: If Epic launcher is being invoked, clean up any orphaned/zombie helpers
            if (cleanCommand.Contains("epic", StringComparison.OrdinalIgnoreCase))
            {
                CleanOrphanedEpicHelpers();
            }

            _logger.LogInfo($"Launching: '{cleanCommand}'");

            var startInfo = new ProcessStartInfo
            {
                FileName = cleanCommand,
                UseShellExecute = true
            };

            if (File.Exists(cleanCommand))
            {
                string? dir = Path.GetDirectoryName(cleanCommand);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    startInfo.WorkingDirectory = dir;
                }
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"\"{cleanCommand}\"",
                        UseShellExecute = false
                    };
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{cleanCommand}\"",
                        UseShellExecute = false
                    };
                }
            }

            using var proc = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Standard Process.Start failed: {ex.Message}. Trying Windows shell fallback...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    string cleanCommand = commandOrUri.Trim('"', '\'');
                    var fallbackPsi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start \"\" \"{cleanCommand}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var fallbackProc = Process.Start(fallbackPsi);
                    return true;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError($"Windows shell fallback also failed: {fallbackEx.Message}");
                }
            }

            _logger.LogError($"Failed to launch command/URI '{commandOrUri}': {ex.Message}");
            return false;
        }
    }

    private void CleanOrphanedEpicHelpers()
    {
        try
        {
            var mainProcs = Process.GetProcessesByName("EpicGamesLauncher");
            bool isMainRunning = mainProcs.Length > 0;
            foreach (var m in mainProcs) { m.Dispose(); }

            // If main launcher is NOT running, but background helper/service is running, clean them up
            if (!isMainRunning)
            {
                var helpers = new[] { "EpicWebHelper", "EpicOnlineServicesHost", "EOSOverlayRenderer" };
                foreach (var h in helpers)
                {
                    var procs = Process.GetProcessesByName(h);
                    if (procs.Length > 0)
                    {
                        _logger.LogDebug($"Cleaning up orphaned helper '{h}' before launch...");
                        foreach (var p in procs)
                        {
                            try { p.Kill(entireProcessTree: true); } catch { }
                            finally { p.Dispose(); }
                        }
                        ForceKillProcessTreeWindows(h);
                    }
                }
            }
        }
        catch
        {
            // Ignore pre-launch clean errors
        }
    }

    public int GetRunningInstanceCount(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            int count = processes.Length;

            foreach (var p in processes)
            {
                p.Dispose();
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    public void CloseLauncherProcesses(string launchCommand)
    {
        var targetProcesses = GetLauncherProcessNames(launchCommand);

        foreach (var procName in targetProcesses)
        {
            try
            {
                var processes = Process.GetProcessesByName(procName);
                foreach (var p in processes)
                {
                    try
                    {
                        _logger.LogInfo($"Terminating launcher process '{p.ProcessName}' (PID: {p.Id})...");
                        p.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Could not terminate process '{procName}' via Process.Kill (PID: {p.Id}): {ex.Message}");
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }

                // Reinforce on Windows with taskkill to eliminate zombie process trees
                ForceKillProcessTreeWindows(procName);
            }
            catch
            {
                // Ignore process enum errors
            }
        }
    }

    private static readonly string[] OverlayProcessPrefixes = new[]
    {
        "EOSOverlayRenderer"
    };

    private readonly HashSet<int> _terminatedOverlayPids = new();

    public void KillOverlayProcesses()
    {
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    string pName = p.ProcessName;
                    if (OverlayProcessPrefixes.Any(prefix => pName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (_terminatedOverlayPids.Add(p.Id))
                        {
                            _logger.LogInfo($"Disabling overlay process: '{pName}' (PID: {p.Id})...");
                        }
                        try
                        {
                            p.Kill(entireProcessTree: true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug($"Could not kill overlay process '{pName}' (PID: {p.Id}): {ex.Message}");
                        }

                        ForceKillProcessTreeWindows(pName);
                    }
                }
                catch
                {
                    // Ignore individual process errors
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch
        {
            // Ignore process enum errors
        }
    }

    private void ForceKillProcessTreeWindows(string procName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /T /IM \"{procName}.exe\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
        }
        catch
        {
            // Ignore if taskkill fails or is not available
        }
    }

    public static string[] GetLauncherProcessNames(string launchCommand)
    {
        if (string.IsNullOrWhiteSpace(launchCommand))
            return Array.Empty<string>();

        string clean = launchCommand.Trim('"', '\'');

        if (clean.StartsWith("com.epicgames.launcher://", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                "EpicGamesLauncher",
                "EpicWebHelper",
                "EpicOnlineServicesHost",
                "EpicOnlineServices",
                "EpicOnlineServicesUser",
                "EpicInstaller",
                "EOSOverlayRenderer",
                "EOSOverlayRenderer-Win64-Shipping",
                "EOSOverlayRenderer-Win32-Shipping",
                "EOSSDK-Win64-Shipping",
                "EOSSDK-Win32-Shipping",
                "CrashReportClient",
                "CrashReportClient-Win64-Shipping",
                "CrashReportClient-Win32-Shipping",
                "EasyAntiCheat_EOS"
            };
        }
        if (clean.StartsWith("origin2://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("origin://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("eadm://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("ea://", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("EADesktop", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("EALauncher", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "EADesktop", "EABackgroundService", "EAWebKit", "EALauncher", "EACrashReporter", "EACoreServer", "Origin", "OriginClientService", "OriginWebHelperService" };
        }
        if (clean.StartsWith("uplay://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("ubisoft://", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("UbisoftConnect", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("UbisoftGameLauncher", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "UbisoftConnect", "upc", "Uplay", "UplayWebCore", "UbisoftGameLauncher", "UbisoftGameLauncher64", "UplayService" };
        }
        if (clean.StartsWith("battlenet://", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Battle.net", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "Battle.net", "Agent" };
        }
        if (clean.StartsWith("goggalaxy://", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("GalaxyClient", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "GalaxyClient", "GalaxyClientService", "GalaxyCommunication", "GalaxyOverlay", "GOG Galaxy Notifications Renderer", "GalaxyClient Helper" };
        }
        if (clean.StartsWith("xbox://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("ms-xbl-", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("XboxGames", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("gamelaunchhelper", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "XboxPcApp", "XboxApp" };
        }

        // Direct executables (e.g., DRM-free GOG games like witcher3.exe or standalone games)
        // do not invoke a third-party store launcher, so no launcher should be closed.
        return Array.Empty<string>();
    }

}

public class ProcessTracker
{
    private readonly IProcessProvider _provider;
    private readonly Logger _logger;

    public ProcessTracker(IProcessProvider provider, Logger logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task<int> RunAsync(Options options, CancellationToken cancellationToken = default)
    {
        // 1. Take initial process snapshot
        _logger.LogDebug("Capturing initial process snapshot...");
        var initialSnapshot = _provider.CaptureSnapshot();
        _logger.LogDebug($"Snapshot recorded with {initialSnapshot.Processes.Count} running processes.");

        // 2. Launch target command/URI
        if (!_provider.Launch(options.LaunchCommand))
        {
            return 1;
        }

        // 3. Track game session lifetime
        _logger.LogInfo($"Monitoring game session (timeout waiting for start: {options.TimeoutSeconds}s)...");
        bool gameStarted = await TrackGameSessionAsync(initialSnapshot, options, cancellationToken);

        if (!gameStarted)
        {
            _logger.LogError($"Target process failed to start within {options.TimeoutSeconds} seconds.");
            return 1;
        }

        // 4. Close ONLY the specific launcher associated with this game, and ONLY if --close-launcher was requested
        if (options.CloseLauncher)
        {
            var launcherTargets = DefaultProcessProvider.GetLauncherProcessNames(options.LaunchCommand);
            if (launcherTargets.Length > 0)
            {
                if (options.SyncDelaySeconds > 0)
                {
                    _logger.LogInfo($"Waiting {options.SyncDelaySeconds}s for launcher cloud saves to sync before closing...");
                    await Task.Delay(TimeSpan.FromSeconds(options.SyncDelaySeconds), cancellationToken);
                }

                _logger.LogInfo($"Closing launcher processes associated with '{options.LaunchCommand}'...");
                _provider.CloseLauncherProcesses(options.LaunchCommand);
            }
            else
            {
                _logger.LogDebug("Direct executable launch: no third-party launcher needs to be closed.");
            }
        }

        _logger.LogInfo("Game session ended. Non-Steam Launcher exiting with code 0.");
        return 0;
    }


    public static string? InferTargetProcessName(string? launchCommand)
    {
        if (string.IsNullOrWhiteSpace(launchCommand))
            return null;

        string clean = launchCommand.Trim('"', '\'');

        if (clean.Contains("://", StringComparison.Ordinal))
            return null;

        if (!clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;

        string name = Path.GetFileNameWithoutExtension(clean);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (LauncherFilter.IsLauncherOrSystemProcess(name))
            return null;

        if (name.Contains("launcher", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("bootstrap", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("setup", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return name;
    }

    public async Task<bool> TrackGameSessionAsync(ProcessSnapshot initialSnapshot, Options options, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var timeoutSpan = TimeSpan.FromSeconds(options.TimeoutSeconds);

        string? effectiveProcessName = !string.IsNullOrEmpty(options.ProcessName)
            ? options.ProcessName
            : InferTargetProcessName(options.LaunchCommand);

        if (string.IsNullOrEmpty(options.ProcessName) && !string.IsNullOrEmpty(effectiveProcessName))
        {
            _logger.LogInfo($"Inferred target game process from executable: '{effectiveProcessName}'");
        }

        var trackedGamePids = new HashSet<int>();
        string? primaryProcessName = effectiveProcessName;
        bool hasGameStarted = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (options.DisableOverlay)
            {
                _provider.KillOverlayProcesses();
            }

            var currentSnapshot = _provider.CaptureSnapshot();
            var newProcesses = initialSnapshot.GetNewProcesses(currentSnapshot);

            var gameCandidates = newProcesses
                .Where(p => !LauncherFilter.IsLauncherOrSystemProcess(p.ProcessName))
                .ToList();

            if (!string.IsNullOrEmpty(effectiveProcessName))
            {
                gameCandidates = gameCandidates
                    .Where(p => p.ProcessName.Equals(effectiveProcessName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var p in gameCandidates)
            {
                if (trackedGamePids.Add(p.Id))
                {
                    _logger.LogInfo($"Detected active game process: '{p.ProcessName}' (PID: {p.Id})");
                    primaryProcessName ??= p.ProcessName;
                }
            }

            // Waiting for initial game process to start
            if (!hasGameStarted)
            {
                if (trackedGamePids.Count > 0)
                {
                    hasGameStarted = true;
                }
                else if (!string.IsNullOrEmpty(effectiveProcessName) && _provider.GetRunningInstanceCount(effectiveProcessName) > 0)
                {
                    _logger.LogInfo($"Detected running instance of explicit process: '{effectiveProcessName}'");
                    hasGameStarted = true;
                }
                else
                {
                    if (DateTime.UtcNow - startTime > timeoutSpan)
                    {
                        return false;
                    }

                    _logger.LogDebug("Waiting for game process to start...");
                    await Task.Delay(500, cancellationToken);
                    continue;
                }
            }

            // Count how many tracked game processes are currently active
            int activeCount = 0;
            foreach (var pid in trackedGamePids)
            {
                if (currentSnapshot.Processes.ContainsKey(pid))
                {
                    activeCount++;
                }
            }

            if (!string.IsNullOrEmpty(primaryProcessName))
            {
                int nameCount = _provider.GetRunningInstanceCount(primaryProcessName);
                if (nameCount > activeCount)
                {
                    activeCount = nameCount;
                }
            }

            if (activeCount == 0)
            {
                // Brief pause and re-check to ensure no sub-process transitions
                await Task.Delay(1500, cancellationToken);
                var recheckSnapshot = _provider.CaptureSnapshot();
                var recheckNew = initialSnapshot.GetNewProcesses(recheckSnapshot);
                var recheckGame = recheckNew
                    .Where(p => !LauncherFilter.IsLauncherOrSystemProcess(p.ProcessName))
                    .ToList();

                if (!string.IsNullOrEmpty(effectiveProcessName))
                {
                    recheckGame = recheckGame
                        .Where(p => p.ProcessName.Equals(effectiveProcessName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                int recheckActiveCount = 0;
                foreach (var p in recheckGame)
                {
                    if (trackedGamePids.Contains(p.Id) || (primaryProcessName != null && p.ProcessName.Equals(primaryProcessName, StringComparison.OrdinalIgnoreCase)))
                    {
                        recheckActiveCount++;
                    }
                }

                if (!string.IsNullOrEmpty(primaryProcessName))
                {
                    int nameCount = _provider.GetRunningInstanceCount(primaryProcessName);
                    if (nameCount > recheckActiveCount)
                    {
                        recheckActiveCount = nameCount;
                    }
                }

                if (recheckActiveCount == 0)
                {
                    _logger.LogInfo("All game processes have exited.");
                    return true;
                }
            }

            _logger.LogDebug($"Monitoring game session: {activeCount} active process(es)");
            await Task.Delay(1000, cancellationToken);
        }

        return trackedGamePids.Count > 0;
    }

    private static bool CheckIfLauncherWasRunningInitially(ProcessSnapshot initialSnapshot, string launchCommand)
    {
        var targetProcesses = DefaultProcessProvider.GetLauncherProcessNames(launchCommand);
        foreach (var procName in targetProcesses)
        {
            if (initialSnapshot.Processes.Values.Any(name => name.Equals(procName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }
}


