using System.Net;
using System.Text;
using System.Text.Json;
using Muggins.Engine.Api;
using Muggins.Engine.Domain;

namespace Muggins.Engine.Tests;

internal static class ApiIntegrationTests
{
    private static readonly Guid MatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PlayerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static async Task RequestsTokenAndCreatesPlayerAsync()
    {
        var profile = new PerfilJogadorDto { JogadorId = PlayerId, Nome = "Gabriel", Ativo = true };
        var handler = new StubHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/api/jogadores"
                ? Json(HttpStatusCode.OK, profile)
                : new HttpResponseMessage(HttpStatusCode.NoContent)));
        var api = new MugginsApiClient(new HttpClient(handler));

        await api.RequestTokenAsync("gabriel@example.com");
        var created = await api.CreatePlayerAsync("account-token", "Gabriel");

        TestAssert.Equal(2, handler.Requests.Count, "Token request and player creation should each send one request.");
        TestAssert.Equal("/api/identidade/token", handler.Requests[0].Uri.AbsolutePath,
            "The account token should be requested from the identity endpoint.");
        TestAssert.True(handler.Requests[0].Body!.Contains("\"email\":\"gabriel@example.com\"", StringComparison.Ordinal),
            "The token request should include the registered email.");
        TestAssert.Equal("/api/jogadores", handler.Requests[1].Uri.AbsolutePath,
            "Player profiles should be created through the players endpoint.");
        TestAssert.Equal("Bearer account-token", handler.Requests[1].Authorization,
            "Player creation should authenticate with the account token.");
        TestAssert.Equal(PlayerId, created.JogadorId, "The created player profile should be returned.");
    }

    public static async Task ActivatesPlayerWithSeparateKeyAsync()
    {
        var profile = new PerfilJogadorDto { JogadorId = PlayerId, Nome = "Gabriel", Ativo = true };
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, profile)));
        var api = new MugginsApiClient(new HttpClient(handler));

        var activated = await api.ActivatePlayerAsync("account-token", PlayerId, "player-activation-key");

        TestAssert.Equal($"/api/jogadores/{PlayerId:D}/ativar-cliente", handler.Requests[0].Uri.AbsolutePath,
            "Player activation should use the player activation endpoint.");
        TestAssert.Equal("Bearer account-token", handler.Requests[0].Authorization,
            "Player activation should include the account token.");
        TestAssert.True(handler.Requests[0].Body!.Contains(
                "\"chaveAtivacao\":\"player-activation-key\"", StringComparison.Ordinal),
            "Player activation should send its separate key in the request body.");
        TestAssert.Equal(PlayerId, activated.JogadorId, "The activated profile should be returned.");
    }

    public static async Task CoordinatorSkipsSubmissionWhenWaitingAsync()
    {
        var state = Snapshot(version: 3, next: ProximaAcaoPartida.Aguardar);
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, state)));
        var coordinator = new BotTurnCoordinator(new MugginsApiClient(new HttpClient(handler)));

        var result = await coordinator.RunOnceAsync("account-token", "activation-key", MatchId, PlayerId);

        TestAssert.Equal(BotTurnStatus.NoAction, result.Status, "Waiting turns should not submit an action.");
        TestAssert.Equal(1, handler.Requests.Count, "Waiting turns should only fetch the current state.");
    }

    public static async Task CoordinatorSubmitsWithVersionAndCredentialsAsync()
    {
        var initial = Snapshot(version: 10, next: ProximaAcaoPartida.Comprar);
        var accepted = Snapshot(version: 11, next: ProximaAcaoPartida.Jogar);
        var handler = new StubHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, initial)
            : Json(HttpStatusCode.OK, new ConfirmacaoAcaoPartidaDto
            {
                ChaveIdempotencia = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Estado = accepted
            })));
        var coordinator = new BotTurnCoordinator(new MugginsApiClient(new HttpClient(handler)));

        var result = await coordinator.RunOnceAsync("opaque-token", "secret-activation", MatchId, PlayerId);

        TestAssert.Equal(BotTurnStatus.ActionConfirmed, result.Status, "A valid buy should be confirmed.");
        TestAssert.Equal(accepted.VersaoEstado, result.State.VersaoEstado, "The confirmed state should be returned.");
        TestAssert.Equal(2, handler.Requests.Count, "A turn should fetch and submit exactly once.");

        var post = handler.Requests[1];
        TestAssert.Equal(HttpMethod.Post, post.Method, "The action should use POST.");
        TestAssert.Equal("/api/partidas/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/comprar", post.Uri.AbsolutePath,
            "Buying should use the comprar endpoint.");
        TestAssert.Equal("Bearer opaque-token", post.Authorization, "The account token should be a Bearer token.");
        TestAssert.Equal("secret-activation", post.ActivationKey, "The player activation key uses its own header.");
        TestAssert.True(post.Body!.Contains("\"versaoEsperada\":10", StringComparison.Ordinal),
            "The action should use the version from the fetched state.");
        TestAssert.True(post.Body.Contains($"\"jogadorId\":\"{PlayerId:D}\"", StringComparison.Ordinal),
            "The action body should identify the selected player.");
        TestAssert.True(post.Body.Contains("\"chaveIdempotencia\":\"", StringComparison.Ordinal),
            "The action body should contain an idempotency key.");
    }

    public static async Task Updates204ReturnsNullAsync()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        var api = new MugginsApiClient(new HttpClient(handler));

        var state = await api.GetUpdatesAsync("token", "activation", MatchId, PlayerId, knownVersion: 7);

        TestAssert.True(state is null, "HTTP 204 should map to no update.");
        TestAssert.True(handler.Requests[0].Uri.Query.Contains("versaoConhecida=7", StringComparison.Ordinal),
            "Updates should send the known version.");
    }

    public static async Task CoordinatorReturnsAuthoritativeStateOnConflictAsync()
    {
        var initial = Snapshot(version: 20, next: ProximaAcaoPartida.Comprar);
        var authoritative = Snapshot(version: 21, next: ProximaAcaoPartida.Aguardar);
        var handler = new StubHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, initial)
            : Json(HttpStatusCode.Conflict, new RespostaErroDto
            {
                Codigo = "CONFLITO_DE_ESTADO",
                EstadoAutoritativo = authoritative
            })));
        var coordinator = new BotTurnCoordinator(new MugginsApiClient(new HttpClient(handler)));

        var result = await coordinator.RunOnceAsync("token", "activation", MatchId, PlayerId);

        TestAssert.Equal(BotTurnStatus.StateConflict, result.Status, "A 409 should be surfaced as a state conflict.");
        TestAssert.Equal(authoritative.VersaoEstado, result.State.VersaoEstado,
            "The conflict response's authoritative state should be returned.");
        TestAssert.Equal(2, handler.Requests.Count, "The coordinator must not resubmit a stale decision.");
    }

    public static async Task CancelAndRestartUseActivationCredentialsAsync()
    {
        var confirmation = new ConfirmacaoAcaoPartidaDto
        {
            ChaveIdempotencia = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")
        };
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, confirmation)));
        var api = new MugginsApiClient(new HttpClient(handler));
        var cancelKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var restartKey = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await api.CancelMatchAsync("opaque-token", "activation-key", MatchId,
            new AcaoRequestDto { JogadorId = PlayerId, VersaoEsperada = 31, ChaveIdempotencia = cancelKey });
        await api.RestartMatchAsync("opaque-token", "activation-key", MatchId,
            new AcaoRequestDto { JogadorId = PlayerId, VersaoEsperada = 32, ChaveIdempotencia = restartKey });

        TestAssert.Equal("/api/partidas/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/cancelar",
            handler.Requests[0].Uri.AbsolutePath, "Cancel should use the official cancel endpoint.");
        TestAssert.Equal("/api/partidas/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/reiniciar",
            handler.Requests[1].Uri.AbsolutePath, "Restart should use the official restart endpoint.");
        foreach (var request in handler.Requests)
        {
            TestAssert.Equal("Bearer opaque-token", request.Authorization,
                "Match management must send the account bearer token.");
            TestAssert.Equal("activation-key", request.ActivationKey,
                "Match management must send the selected player's activation key.");
            TestAssert.True(request.Body!.Contains($"\"jogadorId\":\"{PlayerId:D}\"", StringComparison.Ordinal),
                "Match management must identify the participating player.");
        }

        TestAssert.True(handler.Requests[0].Body!.Contains($"\"chaveIdempotencia\":\"{cancelKey:D}\"", StringComparison.Ordinal),
            "Cancel must submit its idempotency key.");
        TestAssert.True(handler.Requests[1].Body!.Contains($"\"chaveIdempotencia\":\"{restartKey:D}\"", StringComparison.Ordinal),
            "Restart must submit its idempotency key.");
    }

    public static async Task RestartLookupUsesParticipantQueryAsync()
    {
        var restartedId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var state = Snapshot(version: 1, next: ProximaAcaoPartida.JogarAbertura) with { PartidaId = restartedId };
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, state)));
        var api = new MugginsApiClient(new HttpClient(handler));

        var result = await api.GetRestartedMatchAsync("token", "activation", MatchId, PlayerId);

        TestAssert.Equal(restartedId, result.PartidaId, "The restarted match state should be returned.");
        TestAssert.Equal($"/api/partidas/{MatchId:D}/reinicio", handler.Requests[0].Uri.AbsolutePath,
            "Restart lookup should use the previous match ID.");
        TestAssert.True(handler.Requests[0].Uri.Query.Contains($"jogadorId={PlayerId:D}", StringComparison.Ordinal),
            "Restart lookup should be scoped to the participating player.");
        TestAssert.Equal("activation", handler.Requests[0].ActivationKey,
            "Restart lookup should include the activation key.");
    }

    public static async Task PreservesUnauthorizedStatusAndCodeAsync()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized,
            new RespostaErroDto { Codigo = "TOKEN_INVALIDO" })));
        var api = new MugginsApiClient(new HttpClient(handler));

        var exception = await TestAssert.ThrowsAsync<MugginsApiException>(
            () => api.GetSessionAsync("expired-token"),
            "An unauthorized API response should remain an API exception.");

        TestAssert.True(exception.StatusCode == HttpStatusCode.Unauthorized,
            "The adapter should preserve the API's HTTP status.");
        TestAssert.Equal("TOKEN_INVALIDO", exception.ApiError?.Codigo,
            "The adapter should preserve the API's error code for the UI.");
    }

    private static EstadoPartidaParticipanteDto Snapshot(long version, ProximaAcaoPartida next) => new()
    {
        PartidaId = MatchId,
        VersaoEstado = version,
        Situacao = SituacaoPartida.EmAndamento,
        NumeroRodada = 1,
        MinhaMao = [],
        Tabuleiro = [],
        Participantes = [],
        ProximaAcao = next,
        ParticipacaoId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")
    };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, MugginsApiJson.Options), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
        public List<CapturedRequest> Requests { get; } = [];

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
            _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Jogador-Ativacao", out var values) ? values.Single() : null,
                body));
            return await _respond(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? ActivationKey,
        string? Body);
}
