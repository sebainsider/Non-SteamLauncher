namespace NonSteamLauncher;

public static class LauncherFilter
{
    private static readonly HashSet<string> KnownLauncherProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Epic Games & EOS
        "EpicGamesLauncher",
        "EpicGamesLauncher.exe",
        "EpicGamesLauncherMigrate",
        "EpicGamesLauncherSetup",
        "EpicGamesLauncherUpdate",
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
        "UnrealEngineLauncher",
        "UnrealEditor",
        "UnrealVersionSelector",

        // Crash Reporters & Utilities
        "CrashReportClient",
        "CrashReportClient-Win64-Shipping",
        "CrashReportClient-Win32-Shipping",
        "UnityCrashHandler32",
        "UnityCrashHandler64",
        "QtWebEngineProcess",

        // Anti-Cheat Systems
        "EasyAntiCheat",
        "EasyAntiCheat_EOS",
        "EasyAntiCheat_Setup",
        "EasyAntiCheat_x64",
        "EasyAntiCheat_x86",
        "BEService",
        "BEService_x64",
        "BattlEye",
        "AntiCheatExpert",
        "ACE-Base",

        // EA / Origin
        "EADesktop",
        "EABackgroundService",
        "EAWebKit",
        "EALauncher",
        "EACrashReporter",
        "EACoreServer",
        "Origin",
        "OriginClientService",
        "OriginWebHelperService",
        "OriginER",

        // Ubisoft Connect / Uplay
        "UbisoftConnect",
        "upc",
        "Uplay",
        "UplayWebCore",
        "UbisoftGameLauncher",
        "UbisoftGameLauncher64",
        "UplayService",
        "OverlayVK64",
        "Overlay64",

        // Rockstar / 2K
        "RockstarGamesLauncher",
        "RockstarService",
        "SocialClubHelper",
        "LauncherPatcher",
        "2KLauncher",

        // Battle.net
        "Battle.net",
        "Agent",
        "Battle.net.exe",
        "Battle.net Authenticator",

        // GOG Galaxy
        "GalaxyClient",
        "GalaxyClientService",
        "GalaxyCommunication",
        "GalaxyOverlay",
        "GalaxyClient Helper",
        "GOG Galaxy Notifications Renderer",

        // CD Projekt Red / REDlauncher
        "REDprelauncher",
        "REDprelauncher.exe",
        "REDlauncher",
        "REDlauncher.exe",

        // Steam
        "steam",
        "steamservice",
        "steamwebhelper",
        "graphedit",
        "gameoverlayui",

        // Riot Games
        "RiotClientServices",
        "RiotClientUx",
        "RiotClientUxRender",
        "vgtray",
        "vgc",

        // Xbox & PC Game Pass / Windows Game Bar
        "gamelaunchhelper",
        "XboxPcApp",
        "XboxPcAppFT",
        "XboxApp",
        "XboxGamingOverlay",
        "GamingServices",
        "gamingservicesnet",
        "GameBar",
        "GameBarFTServer",
        "GameBarPresenceWriter",
        "XboxGameBarWidgets",
        "XboxGameBarSpotify",
        "GameBarElevatedFT_Alias",

        // GPU Drivers & Overlays (NVIDIA / AMD / Intel)
        "nvngx_update",
        "NVDisplay.Container",
        "NVIDIA Web Helper",
        "NVIDIA Share",
        "NVIDIA Overlay",
        "nvsphelper64",
        "NvTelemetryContainer",
        "NvContainer",
        "OAWrapper",
        "RadeonSoftware",
        "AMDRSServ",
        "AMDRSSrcExt",
        "amdow",
        "PresentMon_x64",

        // Browsers & Common Background Apps
        "msedge",
        "msedgewebview2",
        "chrome",
        "firefox",
        "brave",
        "opera",
        "vivaldi",
        "Discord",
        "Spotify",
        "git",
        "ssh-agent",
        "FileCoAuth",
        "OneDrive",
        "OneDriveStandaloneUpdater",
        "Teams",
        "ms-teams",
        "Slack",

        // Windows Shell / System utilities
        "cmd",
        "powershell",
        "pwsh",
        "conhost",
        "wt",
        "explorer",
        "rundll32",
        "dllhost",
        "svchost",
        "RuntimeBroker",
        "ApplicationFrameHost",
        "SearchHost",
        "SearchApp",
        "SearchIndexer",
        "SearchProtocolHost",
        "SearchFilterHost",
        "StartMenuExperienceHost",
        "ShellExperienceHost",
        "TextInputHost",
        "backgroundTaskHost",
        "WmiPrvSE",
        "Taskmgr",
        "smartscreen",
        "SecurityHealthService",
        "SecurityHealthSystray",
        "MsMpEng",
        "NisSrv",
        "SgrmBroker",
        "sihost",
        "ctfmon",
        "dwm",
        "fontdrvhost",
        "audiodg",
        "CompPkgSrv",
        "LockApp",
        "SystemSettings",
        "PhoneExperienceHost",
        "WidgetService",
        "Widgets",
        "MoUsoCoreWorker",
        "TiWorker",
        "TrustedInstaller",
        "consent",
        "msiexec",
        "taskhostw",
        "taskkill",
        "SpatialAudioLicenseSrv",
        "WerFault",
        "WerMgr",
        "LauncherBridge",
        "SteamLauncherManager",
        "NonSteamLauncher",
        "Non-SteamLauncher"
    };

    private static readonly string[] KnownLauncherPrefixes = new[]
    {
        "EpicGamesLauncher",
        "EpicOnlineServices",
        "EOSOverlayRenderer",
        "EOSSDK",
        "CrashReportClient",
        "UnityCrashHandler",
        "EasyAntiCheat",
        "BEService"
    };

    public static bool IsLauncherOrSystemProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return true;

        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = processName[..^4];
        }

        if (KnownLauncherProcesses.Contains(processName))
            return true;

        foreach (var prefix in KnownLauncherPrefixes)
        {
            if (processName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

