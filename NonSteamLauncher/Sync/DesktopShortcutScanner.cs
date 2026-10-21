namespace NonSteamLauncher.Sync;

public class DesktopShortcutScanner : IScanner
{
    private readonly IEnumerable<string>? _customDirectories;
    private readonly Logger? _logger;

    public string ScannerName => "Desktop & Start Menu Shortcuts";

    public DesktopShortcutScanner(IEnumerable<string>? customDirectories = null, Logger? logger = null)
    {
        _customDirectories = customDirectories;
        _logger = logger;
    }

    public IReadOnlyList<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();
        var directories = _customDirectories?.ToList() ?? GetDefaultSearchDirectories();

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
                continue;

            try
            {
                var urlFiles = Directory.GetFiles(dir, "*.url", SearchOption.AllDirectories);
                foreach (var file in urlFiles)
                {
                    try
                    {
                        var game = ParseUrlFile(file);
                        if (game != null)
                        {
                            games.Add(game);
                            _logger?.LogDebug($"Found shortcut game: {game.Title} [{game.Launcher}] ({game.LaunchUri})");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug($"Could not parse URL file '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Could not read directory '{dir}': {ex.Message}");
            }
        }

        return games;
    }

    public static InstalledGame? ParseUrlFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        string[] lines = File.ReadAllLines(filePath);
        return ParseUrlLines(Path.GetFileNameWithoutExtension(filePath), lines);
    }

    public static InstalledGame? ParseUrlLines(string gameTitle, IEnumerable<string> lines)
    {
        string? url = null;
        string? iconFile = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
            {
                url = line["URL=".Length..].Trim();
            }
            else if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
            {
                iconFile = line["IconFile=".Length..].Trim().Trim('"', '\'');
            }
        }

        if (string.IsNullOrWhiteSpace(url))
            return null;

        string? launcher = DetectLauncherFromUri(url);
        if (launcher == null)
            return null;

        return new InstalledGame
        {
            Title = gameTitle,
            Launcher = launcher,
            LaunchUri = url,
            IconPath = (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile)) ? iconFile : null
        };
    }

    public static string? DetectLauncherFromUri(string uri)
    {
        if (uri.StartsWith("com.epicgames.launcher://", StringComparison.OrdinalIgnoreCase))
            return "Epic Games";

        if (uri.StartsWith("uplay://", StringComparison.OrdinalIgnoreCase))
            return "Ubisoft Connect";

        if (uri.StartsWith("origin2://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("ea://", StringComparison.OrdinalIgnoreCase))
            return "EA App";

        if (uri.StartsWith("battlenet://", StringComparison.OrdinalIgnoreCase))
            return "Battle.net";

        if (uri.StartsWith("goggalaxy://", StringComparison.OrdinalIgnoreCase))
            return "GOG Galaxy";

        if (uri.StartsWith("xbox:", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("ms-xbl-", StringComparison.OrdinalIgnoreCase))
            return "Xbox";

        return null;
    }

    private static List<string> GetDefaultSearchDirectories()
    {
        var list = new List<string>();

        void TryAdd(Environment.SpecialFolder folder)
        {
            try
            {
                string path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    list.Add(path);
                }
            }
            catch { }
        }

        TryAdd(Environment.SpecialFolder.CommonDesktopDirectory);
        TryAdd(Environment.SpecialFolder.Desktop);
        TryAdd(Environment.SpecialFolder.CommonStartMenu);
        TryAdd(Environment.SpecialFolder.StartMenu);

        return list;
    }
}
