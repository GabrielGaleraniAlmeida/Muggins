using Muggins.Engine.Domain;
using Muggins.Engine.Rules;

namespace Muggins.Engine.Bot;

/// <summary>First deterministic policy for the autonomous Muggins player.</summary>
public sealed class MugginsBot
{
    public BotAction ChooseAction(TurnState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase switch
        {
            TurnPhase.Wait or TurnPhase.Finished => BotAction.None(),
            TurnPhase.Buy => BotAction.Buy(),
            TurnPhase.Pass => BotAction.Pass(),
            TurnPhase.Opening => ChooseOpening(state),
            TurnPhase.Play => ChoosePlay(state),
            _ => throw new ArgumentOutOfRangeException(nameof(state), "Unknown turn phase.")
        };
    }

    private static BotAction ChooseOpening(TurnState state)
    {
        if (state.OpeningTile is not { } openingTile)
        {
            throw new InvalidOperationException("Opening state must contain the required tile.");
        }

        if (!state.Hand.Contains(openingTile))
        {
            throw new InvalidOperationException("Required opening tile is not in the received hand.");
        }

        var openedEnds = MugginsRules.ResultingEnds(new BoardEnds(), openingTile, MoveSide.Left);
        return BotAction.Play(openingTile, MoveSide.Left, MugginsRules.PointsForEnds(openedEnds));
    }

    private static BotAction ChoosePlay(TurnState state)
    {
        var options = MugginsRules.LegalPlays(state.Hand, state.Ends);
        if (options.Count == 0)
        {
            throw new InvalidOperationException(
                "API reports a playable turn, but the local hand has no matching tile.");
        }

        var candidates = options.Select(option =>
        {
            var resultingEnds = MugginsRules.ResultingEnds(state.Ends, option.Tile, option.Side);
            var remainingHand = RemoveOne(state.Hand, option.Tile);
            var nextPlays = MugginsRules.LegalPlays(remainingHand, resultingEnds);
            var followUp = nextPlays
                .Select(next => MugginsRules.PointsForEnds(
                    MugginsRules.ResultingEnds(resultingEnds, next.Tile, next.Side)))
                .DefaultIfEmpty(0)
                .Max();

            return new Candidate(
                option.Tile,
                option.Side,
                MugginsRules.PointsForEnds(resultingEnds),
                followUp,
                nextPlays.Count);
        });

        // Prefer current points, then a likely next scoring opportunity, then flexibility.
        var chosen = candidates
            .OrderByDescending(candidate => candidate.Points)
            .ThenByDescending(candidate => candidate.BestFollowUpPoints)
            .ThenByDescending(candidate => candidate.NextOptions)
            .ThenByDescending(candidate => candidate.Tile.Total)
            .ThenBy(candidate => candidate.Tile.A)
            .ThenBy(candidate => candidate.Tile.B)
            .ThenBy(candidate => candidate.Side)
            .First();

        return BotAction.Play(chosen.Tile, chosen.Side, chosen.Points);
    }

    private static IReadOnlyList<Tile> RemoveOne(IReadOnlyList<Tile> hand, Tile tileToRemove)
    {
        var removed = false;
        var remaining = new List<Tile>(hand.Count);

        foreach (var tile in hand)
        {
            if (!removed && tile == tileToRemove)
            {
                removed = true;
                continue;
            }

            remaining.Add(tile);
        }

        return remaining;
    }

    private sealed record Candidate(Tile Tile, MoveSide Side, int Points, int BestFollowUpPoints, int NextOptions);
}
