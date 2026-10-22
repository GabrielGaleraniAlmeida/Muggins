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
