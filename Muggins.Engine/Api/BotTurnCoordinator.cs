using System.Net;
using Muggins.Engine.Bot;
using Muggins.Engine.Domain;

namespace Muggins.Engine.Api;

public enum BotTurnStatus
{
    NoAction,
    ActionConfirmed,
    StateConflict
}

/// <summary>The result of one read-decide-submit cycle.</summary>
public sealed record BotTurnResult(
    BotTurnStatus Status,
    BotAction Action,
    EstadoPartidaParticipanteDto State,
    ConfirmacaoAcaoPartidaDto? Confirmation = null);

/// <summary>Runs one autonomous turn against the API's authoritative state.</summary>
public sealed class BotTurnCoordinator
{
    private readonly MugginsApiClient _api;
    private readonly MugginsBot _bot;

    public BotTurnCoordinator(MugginsApiClient api, MugginsBot? bot = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _bot = bot ?? new MugginsBot();
    }

    /// <summary>
    /// Fetches the latest participant state, chooses one action, and submits it once.
    /// When a 409 occurs, returns the authoritative state so the caller can decide again.
    /// </summary>
    public async Task<BotTurnResult> RunOnceAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        Guid playerId,
        Func<IReadOnlyList<PecaPartidaDto>, BoardEnds>? boardEndsResolver = null,
        CancellationToken cancellationToken = default)
    {
        var currentState = await _api.GetMatchStateAsync(
            accessToken,
            activationKey,
            matchId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        var turnState = ApiGameStateMapper.ToTurnState(currentState, boardEndsResolver);
        var action = _bot.ChooseAction(turnState);

        if (action.Kind == BotActionKind.None)
        {
            return new BotTurnResult(BotTurnStatus.NoAction, action, currentState);
        }

        try
        {
            var confirmation = await _api.SubmitBotActionAsync(
                accessToken,
                activationKey,
                matchId,
                playerId,
                currentState.VersaoEstado,
                action,
                cancellationToken).ConfigureAwait(false);

            // The contract includes the updated state. Refresh if a server response omits it,
            // so the next decision still starts from an authoritative snapshot.
            var confirmedState = confirmation.Estado ?? await _api.GetMatchStateAsync(
                accessToken,
                activationKey,
                matchId,
                playerId,
                cancellationToken).ConfigureAwait(false);

            return new BotTurnResult(
                BotTurnStatus.ActionConfirmed,
                action,
                confirmedState,
                confirmation);
        }
        catch (MugginsApiException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            // A competing client may have acted after our read. Do not resubmit this decision.
            var authoritativeState = exception.ApiError?.EstadoAutoritativo ?? await _api.GetMatchStateAsync(
                accessToken,
                activationKey,
                matchId,
                playerId,
                cancellationToken).ConfigureAwait(false);

            return new BotTurnResult(BotTurnStatus.StateConflict, action, authoritativeState);
        }
    }
}
