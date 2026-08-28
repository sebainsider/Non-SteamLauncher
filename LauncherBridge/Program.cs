namespace LauncherBridge;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var (options, errorMessage) = Options.Parse(args);
        var logger = new Logger(options?.Verbose ?? true);

        logger.LogInfo("=== LauncherBridge Session Starting ===");
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

        logger.LogInfo($"Launch Command: {options.LaunchCommand}");
        if (!string.IsNullOrEmpty(options.ProcessName))
        {
            logger.LogInfo($"Explicit Process Name: {options.ProcessName}");
        }
        logger.LogInfo($"Timeout: {options.TimeoutSeconds}s | Sync Delay: {options.SyncDelaySeconds}s | Close Launcher: {options.CloseLauncher}");

        var provider = new DefaultProcessProvider(logger);
        var tracker = new ProcessTracker(provider, logger);

        try
        {
            int result = await tracker.RunAsync(options);
            logger.LogInfo($"LauncherBridge finished with exit code {result}");
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

