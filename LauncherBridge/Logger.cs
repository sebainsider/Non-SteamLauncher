namespace LauncherBridge;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class Logger
{
    private readonly bool _verbose;
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public Logger(bool verbose)
    {
        _verbose = verbose;

        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LauncherBridge");

        try
        {
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, "launcherbridge.log");
        }
        catch
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcherbridge.log");
        }
    }

    public string LogFilePath => _logFilePath;

    public void LogDebug(string message)
    {
        if (_verbose)
        {
            Log(LogLevel.Debug, message);
        }
    }

    public void LogInfo(string message)
    {
        Log(LogLevel.Info, message);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    public void LogError(string message)
    {
        Log(LogLevel.Error, message);
    }

    private void Log(LogLevel level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelString = level switch
        {
            LogLevel.Debug => "[DEBUG]",
            LogLevel.Info => "[INFO ]",
            LogLevel.Warning => "[WARN ]",
            LogLevel.Error => "[ERROR]",
            _ => "[LOG  ]"
        };

        var line = $"{timestamp} {levelString} {message}";

        // Write to Console (if console exists)
        try
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Info => ConsoleColor.Cyan,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => originalColor
            };
            Console.WriteLine(line);
            Console.ForegroundColor = originalColor;
        }
        catch
        {
            // Ignore in WinExe mode
        }

        // Write to Log File
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore file write errors
        }
    }
}

