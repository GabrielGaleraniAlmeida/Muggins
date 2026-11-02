using Muggins.Engine.Domain;

namespace Muggins.Engine.Api;

/// <summary>Converts private API snapshots to the engine's decision state.</summary>
public static class ApiGameStateMapper
{
    /// <summary>
    /// Maps a participant snapshot. The API returns <c>tabuleiro</c> from left to right: the first
    /// tile's <c>valorA</c> is the left endpoint and the last tile's <c>valorB</c> is the right
    /// endpoint. A resolver can override this mapping for a nonstandard API implementation.
    /// </summary>
    public static TurnState ToTurnState(
        EstadoPartidaParticipanteDto state,
        Func<IReadOnlyList<PecaPartidaDto>, BoardEnds>? boardEndsResolver = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var hand = (state.MinhaMao ?? Array.Empty<PecaPartidaDto>())
            .Select(piece => new Tile(piece.ValorA, piece.ValorB))
            .ToArray();
        var board = state.Tabuleiro ?? Array.Empty<PecaPartidaDto>();
        var ends = board.Count == 0
            ? new BoardEnds()
            : boardEndsResolver?.Invoke(board) ?? ResolveBoardEnds(board);

        var phase = state.ProximaAcao switch
        {
            ProximaAcaoPartida.Aguardar => TurnPhase.Wait,
            ProximaAcaoPartida.JogarAbertura => TurnPhase.Opening,
            ProximaAcaoPartida.Jogar => TurnPhase.Play,
            ProximaAcaoPartida.Comprar => TurnPhase.Buy,
            ProximaAcaoPartida.Passar => TurnPhase.Pass,
            ProximaAcaoPartida.Encerrada => TurnPhase.Finished,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.ProximaAcao, "Unknown API next action value.")
        };

        Tile? openingTile = state.PedraAberturaObrigatoria is { } opening
            ? new Tile(opening.ValorA, opening.ValorB)
            : null;

        return new TurnState(hand, ends, phase, openingTile);
    }

    private static BoardEnds ResolveBoardEnds(IReadOnlyList<PecaPartidaDto> board)
    {
        for (var index = 1; index < board.Count; index++)
        {
            if (board[index - 1].ValorB != board[index].ValorA)
            {
                throw new InvalidOperationException(
                    "The API returned a domino board whose pieces are not connected from left to right.");
            }
        }

        var first = board[0];
        var last = board[^1];
        var isSingleOpeningDouble = board.Count == 1 && first.ValorA == first.ValorB;
        return new BoardEnds(
            first.ValorA,
            last.ValorB,
            leftIsDouble: board.Count > 1 && first.ValorA == first.ValorB,
            rightIsDouble: board.Count > 1 && last.ValorA == last.ValorB,
            sameDoubleTileAtBothEnds: isSingleOpeningDouble);
    }
}
