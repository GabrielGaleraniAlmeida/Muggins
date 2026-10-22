using Muggins.Engine.Domain;

namespace Muggins.Engine.Rules;

/// <summary>
/// Local rule calculations used to choose a move. The API remains authoritative
/// for validation, official scoring, round completion, and the match result.
/// </summary>
public static class MugginsRules
{
    public static BoardEnds ResultingEnds(BoardEnds ends, Tile tile, MoveSide side)
    {
        if (ends.IsEmpty)
        {
            // Opening orientation is deterministic; the adapter sends side 0.
            return new BoardEnds(tile.A, tile.B);
        }

        if (side == MoveSide.Left)
        {
            return new BoardEnds(tile.OtherEnd(ends.Left!.Value), ends.Right);
        }

        if (side == MoveSide.Right)
        {
            return new BoardEnds(ends.Left, tile.OtherEnd(ends.Right!.Value));
        }

        throw new ArgumentOutOfRangeException(nameof(side));
    }

    public static int PointsForEnds(BoardEnds ends)
    {
        if (ends.IsEmpty)
        {
            return 0;
        }

        var total = ends.Left!.Value + ends.Right!.Value;
        return total % 5 == 0 ? total / 5 : 0;
    }

    public static IReadOnlyList<LegalPlay> LegalPlays(IReadOnlyCollection<Tile> hand, BoardEnds ends)
    {
        ArgumentNullException.ThrowIfNull(hand);

        if (ends.IsEmpty)
        {
            return Array.Empty<LegalPlay>();
        }

        var plays = new List<LegalPlay>();
        var seen = new HashSet<LegalPlay>();

        foreach (var tile in hand)
        {
            if (tile.A == ends.Left || tile.B == ends.Left)
            {
                var leftPlay = new LegalPlay(tile, MoveSide.Left);
                if (seen.Add(leftPlay))
                {
                    plays.Add(leftPlay);
                }
            }

            if (tile.A == ends.Right || tile.B == ends.Right)
            {
                var rightPlay = new LegalPlay(tile, MoveSide.Right);
                if (seen.Add(rightPlay))
                {
                    plays.Add(rightPlay);
                }
            }
        }

        return plays;
    }
}
