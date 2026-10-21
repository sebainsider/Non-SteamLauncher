namespace NonSteamLauncher;

public class Options
{
    public string LaunchCommand { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public int SyncDelaySeconds { get; set; } = 5;
    public bool CloseLauncher { get; set; }
    public bool DisableOverlay { get; set; }
    public bool SyncSteam { get; set; }
    public bool ListGames { get; set; }
    public string? SteamPath { get; set; }
    public string? SteamGridDbApiKey { get; set; }
    public bool SkipArtwork { get; set; }
    public string? SettingsFilePath { get; set; }
    public bool DryRun { get; set; }
    public bool Silent { get; set; }
    public bool Verbose { get; set; }
    public bool ShowHelp { get; set; }

    public static (Options? options, string? errorMessage) Parse(string[] args)
    {
        var options = new Options();

        if (args.Length == 0)
        {
            // Double-click / No arguments: execute Steam library sync by default
            options.SyncSteam = true;
            return (options, null);
        }

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowHelp = true;
                return (options, null);
            }

            if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;
                continue;
            }

            if (arg.Equals("--close-launcher", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                options.CloseLauncher = true;
                continue;
            }

            if (arg.Equals("--disable-overlay", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-o", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--kill-overlay", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--no-overlay", StringComparison.OrdinalIgnoreCase))
            {
                options.DisableOverlay = true;
                continue;
            }

            if (arg.Equals("--sync-steam", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--sync", StringComparison.OrdinalIgnoreCase))
            {
                options.SyncSteam = true;
                continue;
            }

            if (arg.Equals("--list-games", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--scan", StringComparison.OrdinalIgnoreCase))
            {
                options.ListGames = true;
                continue;
            }

            if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options.DryRun = true;
                continue;
            }

            if (arg.Equals("--no-artwork", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--no-images", StringComparison.OrdinalIgnoreCase))
            {
                options.SkipArtwork = true;
                continue;
            }

            if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-q", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--no-ui", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--headless", StringComparison.OrdinalIgnoreCase))
            {
                options.Silent = true;
                continue;
            }

            if (TryParseOption(arg, "--steam-path", "--steam", args, ref i, out var steamPathValue))
            {
                options.SteamPath = steamPathValue;
                continue;
            }

            if (TryParseOption(arg, "--sgdb-key", "--steamgriddb-key", args, ref i, out var sgdbKeyValue) ||
                TryParseOption(arg, "--api-key", "-k", args, ref i, out sgdbKeyValue))
            {
                options.SteamGridDbApiKey = sgdbKeyValue;
                continue;
            }

            if (TryParseOption(arg, "--settings", "--config", args, ref i, out var settingsFileValue))
            {
                options.SettingsFilePath = settingsFileValue;
                continue;
            }

            if (TryParseOption(arg, "--launch", "-l", args, ref i, out var launchValue))
            {
                options.LaunchCommand = launchValue;
                continue;
            }

            if (TryParseOption(arg, "--process", "-p", args, ref i, out var processValue))
            {
                // Remove .exe suffix if user passed it
                if (processValue.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processValue = processValue[..^4];
                }
                options.ProcessName = processValue;
                continue;
            }

            if (TryParseOption(arg, "--timeout", "-t", args, ref i, out var timeoutValue))
            {
                if (int.TryParse(timeoutValue, out var timeout) && timeout > 0)
                {
                    options.TimeoutSeconds = timeout;
                }
                else
                {
                    return (null, $"Invalid timeout value: '{timeoutValue}'. Must be a positive integer.");
                }
                continue;
            }

            if (TryParseOption(arg, "--sync-delay", "-s", args, ref i, out var syncDelayValue) ||
                TryParseOption(arg, "--delay", "-d", args, ref i, out syncDelayValue))
            {
                if (int.TryParse(syncDelayValue, out var delay) && delay >= 0)
                {
                    options.SyncDelaySeconds = delay;
                }
                else
                {
                    return (null, $"Invalid sync delay value: '{syncDelayValue}'. Must be a non-negative integer.");
                }
                continue;
            }

            // If an unrecognized positional argument is passed and LaunchCommand is empty, treat it as launch command if not starting with -
            if (string.IsNullOrEmpty(options.LaunchCommand) && !arg.StartsWith('-'))
            {
                options.LaunchCommand = arg;
                continue;
            }

            return (null, $"Unrecognized argument: '{arg}'");
        }

        if (!options.ShowHelp && !options.SyncSteam && !options.ListGames && string.IsNullOrWhiteSpace(options.LaunchCommand))
        {
            if (options.Silent || options.DryRun || !string.IsNullOrEmpty(options.SteamGridDbApiKey))
            {
                options.SyncSteam = true;
            }
            else
            {
                return (null, "Missing required argument: --launch <command or URI>, or specify --sync-steam / --list-games");
            }
        }

        return (options, null);
    }

    private static bool TryParseOption(string arg, string longName, string shortName, string[] args, ref int index, out string value)
    {
        value = string.Empty;

        if (arg.Equals(longName, StringComparison.OrdinalIgnoreCase) ||
            arg.Equals(shortName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < args.Length && !args[index + 1].StartsWith('-'))
            {
                index++;
                value = args[index];
                return true;
            }
            return false;
        }

        if (arg.StartsWith(longName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(longName.Length + 1)..];
            return true;
        }

        if (arg.StartsWith(shortName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(shortName.Length + 1)..];
            return true;
        }

        return false;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Non-Steam Launcher - Steam Third-Party Launcher Monitor & Library Sync (.NET 9)");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  NonSteamLauncher                             (Default / Double-click: Syncs games with Steam)");
        Console.WriteLine("  NonSteamLauncher --launch <command or URI> [options]");
        Console.WriteLine("  NonSteamLauncher --sync-steam [options]");
        Console.WriteLine("  NonSteamLauncher --list-games [options]");
        Console.WriteLine();
        Console.WriteLine("Launch & Tracking Options:");
        Console.WriteLine("  --launch, -l <command/URI>       Command line or URI to launch the game.");
        Console.WriteLine("  --process, -p <name>             (Optional) Specific process name to monitor (without .exe).");
        Console.WriteLine("                                   If omitted, automatically detects new processes started after launch.");
        Console.WriteLine("  --timeout, -t <seconds>          (Optional) Maximum time to wait for the target process to start.");
        Console.WriteLine("                                   Default: 60 seconds.");
        Console.WriteLine("  --sync-delay, -s <seconds>       (Optional) Delay in seconds after game exits before closing launcher,");
        Console.WriteLine("                                   allowing cloud saves to finish syncing. Default: 5 seconds.");
        Console.WriteLine("  --close-launcher, -c             (Optional) Close/terminate third-party launcher processes after game exits.");
        Console.WriteLine("  --disable-overlay, -o            (Optional) Terminate launcher overlay processes (e.g. EOSOverlayRenderer)");
        Console.WriteLine("                                   to suppress in-game popups and notifications.");
        Console.WriteLine();
        Console.WriteLine("Couch Gaming & Steam Library Sync:");
        Console.WriteLine("  --sync-steam, --sync             Automatically scan installed games across launchers (Epic, EA,");
        Console.WriteLine("                                   Ubisoft, Battle.net, GOG) and add them to Steam shortcuts.vdf.");
        Console.WriteLine("  --list-games, --scan             Scan and display installed games across launchers without modifying Steam.");
        Console.WriteLine("  --sgdb-key, -k <key>             (Optional) Save and use a SteamGridDB API Key for automatic artwork & icons.");
        Console.WriteLine("  --no-artwork                     (Optional) Skip downloading covers, heroes, logos, and icons during sync.");
        Console.WriteLine("  --settings <path>                (Optional) Custom path to settings.json file.");
        Console.WriteLine("  --silent, -q                     (Optional) Suppress the sync status dialog window (silent mode).");
        Console.WriteLine("  --steam-path <path>              (Optional) Override Steam installation directory path.");
        Console.WriteLine("  --dry-run                        (Optional) Simulate Steam sync without writing shortcuts to disk.");
        Console.WriteLine();
        Console.WriteLine("General Options:");
        Console.WriteLine("  --verbose, -v                    (Optional) Enable verbose debug logging.");
        Console.WriteLine("  --help, -h                       Display this help message.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Double-click or run directly to sync:");
        Console.WriteLine("  NonSteamLauncher");
        Console.WriteLine();
        Console.WriteLine("  # Configure SteamGridDB API Key and sync with artwork:");
        Console.WriteLine("  NonSteamLauncher --sgdb-key \"YOUR_STEAMGRIDDB_API_KEY\"");
        Console.WriteLine();
        Console.WriteLine("  # Silent background sync:");
        Console.WriteLine("  NonSteamLauncher --sync-steam --silent");
        Console.WriteLine();
        Console.WriteLine("  # Preview detected games across launchers:");
        Console.WriteLine("  NonSteamLauncher --list-games");
        Console.WriteLine();
        Console.WriteLine("  # Manual game launch via URI:");
        Console.WriteLine("  NonSteamLauncher --launch \"com.epicgames.launcher://apps/Item?action=launch\" --close-launcher --disable-overlay");
    }
}
