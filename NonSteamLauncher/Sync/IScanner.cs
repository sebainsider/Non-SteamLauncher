namespace NonSteamLauncher.Sync;

public interface IScanner
{
    string ScannerName { get; }
    IReadOnlyList<InstalledGame> Scan();
}
