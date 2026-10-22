namespace Muggins.Engine.Domain;

/// <summary>Private hand and turn information mapped from the API response.</summary>
public sealed record TurnState
{
    public IReadOnlyList<Tile> Hand { get; }
    public BoardEnds Ends { get; }
    public TurnPhase Phase { get; }
    public Tile? OpeningTile { get; }

    public TurnState(
        IEnumerable<Tile> hand,
        BoardEnds ends,
        TurnPhase phase,
        Tile? openingTile = null)
    {
        ArgumentNullException.ThrowIfNull(hand);
        Hand = hand.ToArray();
        Ends = ends;
        Phase = phase;
        OpeningTile = openingTile;
    }
}
