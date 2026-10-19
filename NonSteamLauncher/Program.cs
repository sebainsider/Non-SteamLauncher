namespace NonSteamLauncher;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var (options, errorMessage) = Options.Parse(args);
        var logger = new Logger(options?.Verbose ?? true);

        logger.LogInfo("=== Non-Steam Launcher Session Starting ===");
        logger.LogInfo($"Log file: {logger.LogFilePath}");
        logger.LogInfo($"Arguments: {string.Join(" ", args.Select(a => $"\"{a}\""))}");

        if (!string.IsNullOrEmpty(errorMessage))
        {
            logger.LogError(errorMessage);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {errorMessage}");
            Console.ResetColor();
            Console.WriteLine();
            Options.PrintHelp();
            return 1;
        }

        if (options == null || options.ShowHelp)
        {
            Options.PrintHelp();
            return 0;
        }

        // 1. List installed games without modifying Steam
        if (options.ListGames)
        {
            var detector = new Sync.GameDetector(logger: logger);
            var games = detector.DetectAllGames();

            Console.WriteLine();
            Console.WriteLine($"Found {games.Count} installed game(s) across launchers:");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine($"{"Launcher",-18} | {"Game Title",-35} | {"Launch URI / Target"}");
            Console.WriteLine(new string('-', 100));
            foreach (var g in games)
            {
                Console.WriteLine($"{g.Launcher,-18} | {g.Title,-35} | {g.LaunchUri}");
            }
            Console.WriteLine(new string('-', 100));
            Console.WriteLine();
            Console.WriteLine("To automatically add these games to your Steam Library for couch gaming, run:");
            Console.WriteLine("  NonSteamLauncher --sync-steam");
            Console.WriteLine();
            return 0;
        }

        // 2. Synchronize installed games into Steam's shortcuts.vdf
        if (options.SyncSteam)
        {
            var syncService = new Sync.Steam.SteamSyncService(logger: logger);
            var result = syncService.Sync(options);

            if (!options.Silent)
            {
                Sync.UI.SyncStatusDialog.Show(result);
            }

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine($"✔ Steam sync complete! {result.AddedShortcutsCount} new game(s) added across {result.UpdatedProfilesCount} Steam profile(s).");
                Console.ResetColor();
                Console.WriteLine("Pick up your controller, launch Steam Big Picture, and your games are ready to play!");
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine($"✖ Steam sync failed: {result.ErrorMessage}");
                Console.ResetColor();
                return 1;
            }
        }

        logger.LogInfo($"Launch Command: {options.LaunchCommand}");
        if (!string.IsNullOrEmpty(options.ProcessName))
        {
            logger.LogInfo($"Explicit Process Name: {options.ProcessName}");
        }
        logger.LogInfo($"Timeout: {options.TimeoutSeconds}s | Sync Delay: {options.SyncDelaySeconds}s | Close Launcher: {options.CloseLauncher} | Disable Overlay: {options.DisableOverlay}");

        var provider = new DefaultProcessProvider(logger);
        var tracker = new ProcessTracker(provider, logger);

        try
        {
            int result = await tracker.RunAsync(options);
            logger.LogInfo($"Non-Steam Launcher finished with exit code {result}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError($"Unhandled exception: {ex.Message}");
            if (options.Verbose)
            {
                logger.LogError(ex.ToString());
            }
            return 1;
        }
    }
}

