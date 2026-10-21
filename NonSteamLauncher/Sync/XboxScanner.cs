using System.Diagnostics;
using System.Xml.Linq;

namespace NonSteamLauncher.Sync;

public class XboxScanner : IScanner
{
    private readonly IEnumerable<string>? _customDirectories;
    private readonly Logger? _logger;

    public string ScannerName => "Xbox & PC Game Pass";

    public XboxScanner(IEnumerable<string>? customDirectories = null, Logger? logger = null)
    {
        _customDirectories = customDirectories;
        _logger = logger;
    }

    public IReadOnlyList<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var searchDirs = _customDirectories?.ToList() ?? GetDefaultXboxDirectories();

        foreach (var parentDir in searchDirs)
        {
            if (!Directory.Exists(parentDir))
                continue;

            try
            {
                var subDirs = Directory.GetDirectories(parentDir);
                foreach (var gameDir in subDirs)
                {
                    try
                    {
                        var game = ParseXboxGameFolder(gameDir);
                        if (game != null && seenTitles.Add(game.Title))
                        {
                            games.Add(game);
                            _logger?.LogDebug($"Found Xbox game: {game.Title} ({game.LaunchUri})");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug($"Error scanning Xbox game folder '{gameDir}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Could not scan Xbox directory '{parentDir}': {ex.Message}");
            }
        }

        return games;
    }

    public static InstalledGame? ParseXboxGameFolder(string gameDir)
    {
        if (!Directory.Exists(gameDir))
            return null;

        string folderName = Path.GetFileName(gameDir.TrimEnd('\\', '/'));
        if (string.IsNullOrWhiteSpace(folderName))
            return null;

        // 1. Locate MicrosoftGame.config
        string? configFile = FindConfigFile(gameDir);
        string? configTitle = null;
        string? configExe = null;
        string? configLogo = null;

        if (configFile != null && File.Exists(configFile))
        {
            try
            {
                string xml = File.ReadAllText(configFile);
                (configTitle, configExe, configLogo) = ParseMicrosoftGameConfigXml(xml);
            }
            catch
            {
                // Ignore XML parse errors and fall back to folder/exe inspection
            }
        }

        // 2. Resolve title
        string title = !string.IsNullOrWhiteSpace(configTitle) ? configTitle : FormatGameTitleFromFolder(folderName);

        // 3. Resolve executable & LaunchUri
        // Priority: gamelaunchhelper.exe (validates Game Pass entitlements with Gaming Services)
        string? launchExe = FindGameLaunchHelper(gameDir);

        if (launchExe == null && !string.IsNullOrWhiteSpace(configExe))
        {
            string candidate = Path.Combine(gameDir, configExe);
            if (File.Exists(candidate))
            {
                launchExe = candidate;
            }
            else
            {
                string candidateInContent = Path.Combine(gameDir, "Content", configExe);
                if (File.Exists(candidateInContent))
                {
                    launchExe = candidateInContent;
                }
            }
        }

        if (launchExe == null)
        {
            launchExe = FindFallbackExecutable(gameDir);
        }

        if (launchExe == null)
        {
            // No executable found; folder might be an uninstalled stub or temp cache
            return null;
        }

        // 4. Resolve Icon
        string? iconPath = null;
        if (!string.IsNullOrWhiteSpace(configLogo))
        {
            string candidateIcon = Path.Combine(gameDir, configLogo);
            if (File.Exists(candidateIcon))
            {
                iconPath = candidateIcon;
            }
            else
            {
                string candidateInContent = Path.Combine(gameDir, "Content", configLogo);
                if (File.Exists(candidateInContent))
                {
                    iconPath = candidateInContent;
                }
            }
        }

        if (iconPath == null)
        {
            iconPath = FindIconImage(gameDir) ?? launchExe;
        }

        return new InstalledGame
        {
            Title = title,
            Launcher = "Xbox",
            LaunchUri = launchExe,
            ExecutablePath = launchExe,
            InstallDirectory = gameDir,
            IconPath = iconPath
        };
    }

    public static (string? title, string? exeName, string? logoName) ParseMicrosoftGameConfigXml(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return (null, null, null);

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var gameElem = doc.Root;
            if (gameElem == null)
                return (null, null, null);

            // Title and logo in <ShellVisuals>
            var shellVisuals = gameElem.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("ShellVisuals", StringComparison.OrdinalIgnoreCase));
            string? title = shellVisuals?.Attribute("DefaultDisplayName")?.Value;
            if (title != null && title.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                title = null; // Resource identifier; fallback to folder name
            }

            string? logo = shellVisuals?.Attribute("Square150x150Logo")?.Value
                        ?? shellVisuals?.Attribute("StoreLogo")?.Value
                        ?? shellVisuals?.Attribute("Square44x44Logo")?.Value
                        ?? shellVisuals?.Attribute("SplashScreenImage")?.Value;

            // Executable in <ExecutableList><Executable Name="..." />
            var execElem = gameElem.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Executable", StringComparison.OrdinalIgnoreCase));
            string? exe = execElem?.Attribute("Name")?.Value;

            return (title, exe, logo);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static string? FindConfigFile(string gameDir)
    {
        string rootConfig = Path.Combine(gameDir, "MicrosoftGame.config");
        if (File.Exists(rootConfig))
            return rootConfig;

        string contentConfig = Path.Combine(gameDir, "Content", "MicrosoftGame.config");
        if (File.Exists(contentConfig))
            return contentConfig;

        return null;
    }

    public static string? FindGameLaunchHelper(string gameDir)
    {
        string rootHelper = Path.Combine(gameDir, "gamelaunchhelper.exe");
        if (File.Exists(rootHelper))
            return rootHelper;

        string contentHelper = Path.Combine(gameDir, "Content", "gamelaunchhelper.exe");
        if (File.Exists(contentHelper))
            return contentHelper;

        return null;
    }

    private static string? FindFallbackExecutable(string gameDir)
    {
        string contentDir = Path.Combine(gameDir, "Content");
        if (Directory.Exists(contentDir))
        {
            var exes = Directory.GetFiles(contentDir, "*.exe", SearchOption.TopDirectoryOnly);
            var main = exes.FirstOrDefault(e => !Path.GetFileName(e).StartsWith("CrashReport", StringComparison.OrdinalIgnoreCase) &&
                                                !Path.GetFileName(e).StartsWith("UnityCrash", StringComparison.OrdinalIgnoreCase));
            if (main != null)
                return main;
        }

        var rootExes = Directory.GetFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly);
        return rootExes.FirstOrDefault(e => !Path.GetFileName(e).StartsWith("CrashReport", StringComparison.OrdinalIgnoreCase) &&
                                            !Path.GetFileName(e).StartsWith("UnityCrash", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindIconImage(string gameDir)
    {
        var candidateFiles = new[]
        {
            "Square150x150Logo.png",
            "StoreLogo.png",
            "Square44x44Logo.png",
            "icon.png",
            "logo.png"
        };

        foreach (var candidate in candidateFiles)
        {
            string p1 = Path.Combine(gameDir, candidate);
            if (File.Exists(p1)) return p1;

            string p2 = Path.Combine(gameDir, "Content", candidate);
            if (File.Exists(p2)) return p2;
        }

        return null;
    }

    public static string FormatGameTitleFromFolder(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return folderName;

        // Strip known Xbox package suffixes if present (e.g., _8wekyb3d8bbwe)
        int suffixIndex = folderName.LastIndexOf('_');
        if (suffixIndex > 0 && suffixIndex < folderName.Length - 1 && folderName.Length - suffixIndex <= 15)
        {
            folderName = folderName[..suffixIndex];
        }

        return folderName.Trim();
    }

    public static List<string> GetDefaultXboxDirectories()
    {
        var list = new List<string>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                    {
                        string xboxDir = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
                        if (Directory.Exists(xboxDir))
                        {
                            list.Add(xboxDir);
                        }
                    }
                }
                catch
                {
                    // Ignore inaccessible or offline drives
                }
            }
        }
        catch
        {
            // Ignore DriveInfo failure on non-Windows/restricted environments
        }

        // Standard C:\XboxGames fallback if drives enumeration didn't catch it
        if (list.Count == 0)
        {
            string defaultPath = @"C:\XboxGames";
            if (Directory.Exists(defaultPath))
            {
                list.Add(defaultPath);
            }
        }

        return list;
    }
}
