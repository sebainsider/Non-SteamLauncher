namespace NonSteamLauncher.Sync;

public class InstalledGame
{
    public string Title { get; set; } = string.Empty;
    public string Launcher { get; set; } = string.Empty;
    public string LaunchUri { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? IconPath { get; set; }
    public string? InstallDirectory { get; set; }

    public override string ToString() => $"[{Launcher}] {Title} -> {LaunchUri}";
}
