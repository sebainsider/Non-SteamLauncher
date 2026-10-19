namespace NonSteamLauncher.Sync.Steam;

public class SteamShortcut
{
    public uint AppId { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string Exe { get; set; } = string.Empty;
    public string StartDir { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ShortcutPath { get; set; } = string.Empty;
    public string LaunchOptions { get; set; } = string.Empty;
    public int IsHidden { get; set; }
    public int AllowDesktopConfig { get; set; } = 1;
    public int AllowOverlay { get; set; } = 1;
    public int OpenVR { get; set; }
    public int Devkit { get; set; }
    public string DevkitGameID { get; set; } = string.Empty;
    public int DevkitOverrideAppId { get; set; }
    public int LastPlayTime { get; set; }
    public string FlatpakAppID { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();

    public Dictionary<string, object> ExtraProperties { get; set; } = new();

    public override string ToString() => $"Shortcut: {AppName} ({Exe}) AppID: {AppId}";
}
