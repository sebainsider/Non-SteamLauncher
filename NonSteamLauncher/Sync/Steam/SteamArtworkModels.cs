namespace NonSteamLauncher.Sync.Steam;

public enum ArtworkPieceType
{
    Poster,
    Wide,
    Hero,
    Logo,
    Icon
}

public sealed record ArtworkCandidate(
    ArtworkPieceType PieceType,
    string Url,
    string ThumbUrl,
    string Extension,
    int Score,
    int Width,
    int Height,
    string? Style,
    string? Author,
    string Source)
{
    public string DimensionsText => Width > 0 && Height > 0 ? $"{Width}×{Height}" : "";
}

public sealed class GameArtworkOptions
{
    public required InstalledGame Game { get; init; }
    public uint AppId { get; init; }
    public Dictionary<ArtworkPieceType, List<ArtworkCandidate>> Pieces { get; } = new()
    {
        [ArtworkPieceType.Poster] = new List<ArtworkCandidate>(),
        [ArtworkPieceType.Wide] = new List<ArtworkCandidate>(),
        [ArtworkPieceType.Hero] = new List<ArtworkCandidate>(),
        [ArtworkPieceType.Logo] = new List<ArtworkCandidate>(),
        [ArtworkPieceType.Icon] = new List<ArtworkCandidate>()
    };

    public Dictionary<ArtworkPieceType, bool> ExistingPieces { get; } = new();

    public bool HasAnyOptions => Pieces.Values.Any(list => list.Count > 0);
}

public sealed class GameArtworkSelection
{
    public required InstalledGame Game { get; init; }
    public uint AppId { get; init; }
    public Dictionary<ArtworkPieceType, ArtworkCandidate?> SelectedPieces { get; } = new();
}
