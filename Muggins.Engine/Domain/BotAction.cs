namespace Muggins.Engine.Domain;

public enum BotActionKind
{
    Play,
    Buy,
    Pass,
    None
}

/// <summary>Decision returned by the bot, ready for the API adapter to translate.</summary>
public sealed record BotAction
{
    public BotActionKind Kind { get; }
    public Tile? Tile { get; }
    public MoveSide? Side { get; }
    public int EstimatedPoints { get; }

    private BotAction(BotActionKind kind, Tile? tile, MoveSide? side, int estimatedPoints)
    {
        Kind = kind;
        Tile = tile;
        Side = side;
        EstimatedPoints = estimatedPoints;
    }

    public static BotAction Play(Tile tile, MoveSide side, int estimatedPoints) =>
        new(BotActionKind.Play, tile, side, estimatedPoints);

    public static BotAction Buy() => new(BotActionKind.Buy, null, null, 0);
    public static BotAction Pass() => new(BotActionKind.Pass, null, null, 0);
    public static BotAction None() => new(BotActionKind.None, null, null, 0);
}
