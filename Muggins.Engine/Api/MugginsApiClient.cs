using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Muggins.Engine.Api;

/// <summary>HTTP adapter for the published Domino Ponta de Quina API.</summary>
public sealed class MugginsApiClient
{
    public const string DefaultBaseAddress = "https://domino-quina-api-b1-pf2261.azurewebsites.net/";

    private readonly HttpClient _httpClient;
    private readonly Uri _baseAddress;

    public MugginsApiClient(HttpClient httpClient, Uri? baseAddress = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseAddress = baseAddress ?? new Uri(DefaultBaseAddress);

        if (!_baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("The API base address must be absolute.", nameof(baseAddress));
        }
    }

    public async Task RequestTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            "api/identidade/token",
            new SolicitarTokenRequestDto { Email = email });
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<SessaoResponseDto> GetSessionAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<SessaoResponseDto>(
            HttpMethod.Get,
            "api/identidade/sessao",
            accessToken,
            cancellationToken: cancellationToken);

    public async Task RevokeTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/identidade/tokens/revogacao", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<PerfilJogadorDto>> ListPlayersAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<IReadOnlyList<PerfilJogadorDto>>(
            HttpMethod.Get,
            "api/jogadores",
            accessToken,
            cancellationToken: cancellationToken);

    public Task<PerfilJogadorDto> CreatePlayerAsync(
        string accessToken,
        string? name,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<PerfilJogadorDto>(
            HttpMethod.Post,
            "api/jogadores",
            accessToken,
            new CriarJogadorRequestDto { Nome = name },
            cancellationToken: cancellationToken);

    public Task<PerfilJogadorDto> ActivatePlayerAsync(
        string accessToken,
        Guid playerId,
        string activationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activationKey);
        return SendForJsonAsync<PerfilJogadorDto>(
            HttpMethod.Post,
            $"api/jogadores/{playerId:D}/ativar-cliente",
            accessToken,
            new AtivarJogadorRequestDto { ChaveAtivacao = activationKey },
            cancellationToken: cancellationToken);
    }

    public Task<PerfilJogadorDto> DeactivatePlayerAsync(
        string accessToken,
        Guid playerId,
        string activationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activationKey);
        return SendForJsonAsync<PerfilJogadorDto>(
            HttpMethod.Delete,
            $"api/jogadores/{playerId:D}/desativar-cliente",
            accessToken,
            activationKey: activationKey,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<PartidaDisponivelDto>> ListAvailableMatchesAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<IReadOnlyList<PartidaDisponivelDto>>(
            HttpMethod.Get,
            "api/partidas",
            accessToken,
            cancellationToken: cancellationToken);

    public Task<EstadoPartidaParticipanteDto> CreateMatchAsync(
        string accessToken,
        string activationKey,
        CriarPartidaRequestDto body,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<EstadoPartidaParticipanteDto>(
            HttpMethod.Post,
            "api/partidas",
            accessToken,
            body,
            activationKey,
            cancellationToken);

    public Task<ConfirmacaoAcaoPartidaDto> JoinMatchAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        IngressarRequestDto body,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<ConfirmacaoAcaoPartidaDto>(
            HttpMethod.Post,
            $"api/partidas/{matchId:D}/entrar",
            accessToken,
            body,
            activationKey,
            cancellationToken);

    public Task<ConfirmacaoAcaoPartidaDto> StartMatchAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        AcaoRequestDto body,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<ConfirmacaoAcaoPartidaDto>(
            HttpMethod.Post,
            $"api/partidas/{matchId:D}/iniciar",
            accessToken,
            body,
            activationKey,
            cancellationToken);

    public Task<EstadoPartidaParticipanteDto> GetMatchStateAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<EstadoPartidaParticipanteDto>(
            HttpMethod.Get,
            WithQuery($"api/partidas/{matchId:D}", ("jogadorId", playerId.ToString("D"))),
            accessToken,
            activationKey: activationKey,
            cancellationToken: cancellationToken);

    /// <summary>Returns null for the API's 204 no-change response.</summary>
    public async Task<EstadoPartidaParticipanteDto?> GetUpdatesAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        Guid playerId,
        long? knownVersion = null,
        CancellationToken cancellationToken = default)
    {
        var path = WithQuery(
            $"api/partidas/{matchId:D}/atualizacoes",
            knownVersion is null
                ? [("jogadorId", playerId.ToString("D"))]
                : [("jogadorId", playerId.ToString("D")), ("versaoConhecida", knownVersion.Value.ToString())]);
        using var request = CreateRequest(HttpMethod.Get, path, accessToken, activationKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<EstadoPartidaParticipanteDto>(
            MugginsApiJson.Options,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<EstadoPartidaObservadorDto> ObserveMatchAsync(
        string accessToken,
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<EstadoPartidaObservadorDto>(
            HttpMethod.Get,
            $"api/partidas/{matchId:D}/visualizar",
            accessToken,
            cancellationToken: cancellationToken);

    public Task<ConfirmacaoAcaoPartidaDto> PlayAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        JogarRequestDto body,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<ConfirmacaoAcaoPartidaDto>(
            HttpMethod.Post,
            $"api/partidas/{matchId:D}/jogar",
            accessToken,
            body,
            activationKey,
            cancellationToken);

    public Task<ConfirmacaoAcaoPartidaDto> BuyAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        AcaoRequestDto body,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<ConfirmacaoAcaoPartidaDto>(
            HttpMethod.Post,
            $"api/partidas/{matchId:D}/comprar",
            accessToken,
            body,
            activationKey,
            cancellationToken);

    public Task<ConfirmacaoAcaoPartidaDto> PassAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        AcaoRequestDto body,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<ConfirmacaoAcaoPartidaDto>(
            HttpMethod.Post,
            $"api/partidas/{matchId:D}/passar",
            accessToken,
            body,
            activationKey,
            cancellationToken);

    /// <summary>Chooses the matching API endpoint and creates a fresh idempotency key for a bot decision.</summary>
    public Task<ConfirmacaoAcaoPartidaDto> SubmitBotActionAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        Guid playerId,
        long expectedVersion,
        Muggins.Engine.Domain.BotAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var idempotencyKey = Guid.NewGuid();
        return action.Kind switch
        {
            Muggins.Engine.Domain.BotActionKind.Play when action.Tile is { } tile && action.Side is { } side =>
                PlayAsync(
                    accessToken,
                    activationKey,
                    matchId,
                    new JogarRequestDto
                    {
                        ValorA = tile.A,
                        ValorB = tile.B,
                        Lado = side == Muggins.Engine.Domain.MoveSide.Left
                            ? LadoJogavel.Esquerda
                            : LadoJogavel.Direita,
                        VersaoEsperada = expectedVersion,
                        ChaveIdempotencia = idempotencyKey,
                        JogadorId = playerId
                    },
                    cancellationToken),
            Muggins.Engine.Domain.BotActionKind.Buy =>
                BuyAsync(accessToken, activationKey, matchId, CreateActionRequest(), cancellationToken),
            Muggins.Engine.Domain.BotActionKind.Pass =>
                PassAsync(accessToken, activationKey, matchId, CreateActionRequest(), cancellationToken),
            Muggins.Engine.Domain.BotActionKind.Play =>
                throw new ArgumentException("A play action must include a tile and side.", nameof(action)),
            _ => throw new ArgumentException("The bot has no API action to submit in this state.", nameof(action))
        };

        AcaoRequestDto CreateActionRequest() => new()
        {
            VersaoEsperada = expectedVersion,
            ChaveIdempotencia = idempotencyKey,
            JogadorId = playerId
        };
    }

    public Task<ResultadoHistoricoPartidaDto> GetMatchResultAsync(
        string accessToken,
        string activationKey,
        Guid matchId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        SendForJsonAsync<ResultadoHistoricoPartidaDto>(
            HttpMethod.Get,
            WithQuery($"api/partidas/{matchId:D}/resultado", ("jogadorId", playerId.ToString("D"))),
            accessToken,
            activationKey: activationKey,
            cancellationToken: cancellationToken);

    public static string GeneratePlayerActivationKey() =>
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private async Task<T> SendForJsonAsync<T>(
        HttpMethod method,
        string path,
        string? accessToken,
        object? body = null,
        string? activationKey = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, path, accessToken, activationKey, body);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<T>(
            MugginsApiJson.Options,
            cancellationToken).ConfigureAwait(false);

        return result ?? throw new JsonException($"The API returned an empty response for {path}.");
    }

    private HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, object body) =>
        CreateRequest(method, path, accessToken: null, body: body);

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string? accessToken,
        string? activationKey = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseAddress, path));

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            // Swagger expects the opaque token only; AuthenticationHeaderValue adds "Bearer ".
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        }

        if (!string.IsNullOrWhiteSpace(activationKey))
        {
            request.Headers.Add("X-Jogador-Ativacao", activationKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: MugginsApiJson.Options);
        }

        return request;
    }

    private static string WithQuery(string path, params (string Name, string Value)[] values)
    {
        var query = string.Join("&", values.Select(value =>
            $"{Uri.EscapeDataString(value.Name)}={Uri.EscapeDataString(value.Value)}"));
        return $"{path}?{query}";
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        RespostaErroDto? apiError = null;

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                apiError = JsonSerializer.Deserialize<RespostaErroDto>(raw, MugginsApiJson.Options);
            }
            catch (JsonException)
            {
                // Framework errors can be plain text or another JSON shape.
            }
        }

        throw new MugginsApiException(response.StatusCode, apiError, raw);
    }
}
