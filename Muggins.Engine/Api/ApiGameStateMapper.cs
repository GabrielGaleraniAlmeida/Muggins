using Muggins.Engine.Domain;

namespace Muggins.Engine.Api;

/// <summary>Converts private API snapshots to the engine's decision state.</summary>
public static class ApiGameStateMapper
{
    /// <summary>
    /// Maps a participant snapshot. The OpenAPI document does not define tile ordering/orientation
    /// in <c>tabuleiro</c>. For a non-empty board, callers must provide an endpoint resolver based
    /// on the server's confirmed convention; the mapper refuses to guess and risk an invalid move.
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
        if (board.Count > 0 && boardEndsResolver is null)
        {
            throw new InvalidOperationException(
                "The API contract does not define board order/orientation. " +
                "Supply a boardEndsResolver based on the server's confirmed convention.");
        }

        var ends = board.Count == 0
            ? new BoardEnds()
            : boardEndsResolver!(board);

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
}
