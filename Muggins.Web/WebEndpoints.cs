using System.Net;
using Muggins.Engine.Api;
using Muggins.Engine.Bot;
using Muggins.Engine.Domain;
using Muggins.Engine.Rules;
using Microsoft.AspNetCore.Http;

namespace Muggins.Web;

public static class WebEndpoints
{
    public static void MapMugginsWebEndpoints(this WebApplication app)
    {
        app.MapPost("/app-api/auth/request-token", (RequestTokenBody body, MugginsApiClient api, CancellationToken ct) =>
            ProtectAsync(async () =>
            {
                await api.RequestTokenAsync(body.Email ?? string.Empty, ct);
                return Results.Ok(new { message = "Se o endereço FIAP for válido, o token será enviado por e-mail." });
            }, ct));

        app.MapPost("/app-api/auth/connect", (ConnectBody body, HttpContext context, EncryptedSessionStore sessions,
            MugginsApiClient api, CancellationToken ct) => ProtectAsync(async () =>
        {
            var token = body.Token?.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new WebRequestException(StatusCodes.Status400BadRequest, "Cole o token recebido por e-mail.");
            }

            await using var lease = await sessions.OpenAsync(context, ct);
            var session = lease.Data;
            if (session.AccessToken is { Length: > 0 } oldToken &&
                !string.Equals(oldToken, token, StringComparison.Ordinal) &&
                session.ActivationKeys.Count > 0)
            {
                throw new WebRequestException(StatusCodes.Status409Conflict,
                    "Desconecte e libere os jogadores da conta atual antes de conectar outra conta.");
            }

            var identity = await api.GetSessionAsync(token, ct);
            if (!string.Equals(session.AccessToken, token, StringComparison.Ordinal))
            {
                session.ResetMatch();
                session.HumanPlayerId = null;
                session.BotPlayerId = null;
                session.ActivationKeys.Clear();
            }

            session.AccessToken = token;
            session.UserId = identity.UsuarioId;
            await lease.SaveAsync(ct);
            return Results.Ok(new { connected = true, userId = session.UserId });
        }, ct));

        app.MapPost("/app-api/auth/disconnect", (HttpContext context, EncryptedSessionStore sessions,
            MugginsApiClient api, CancellationToken ct) => ProtectAsync(async () =>
        {
            await using var lease = await sessions.OpenAsync(context, ct);
            var session = lease.Data;
            var token = RequireToken(session);

            if (session.MatchId is { } activeMatchId &&
                session.HumanPlayerId is { } activeHumanId &&
                session.ActivationKeys.TryGetValue(activeHumanId, out var humanActivationKey))
            {
                var currentMatch = await api.GetMatchStateAsync(token, humanActivationKey, activeMatchId, activeHumanId, ct);
                if (currentMatch.Situacao == SituacaoPartida.EmAndamento)
                {
                    throw new WebRequestException(StatusCodes.Status409Conflict,
                        "A partida está em andamento. Termine-a antes de liberar os perfis.");
                }
            }

            foreach (var playerId in new[] { session.HumanPlayerId, session.BotPlayerId }.OfType<Guid>().Distinct())
            {
                if (!session.ActivationKeys.TryGetValue(playerId, out var activationKey))
                {
                    continue;
                }

                await api.DeactivatePlayerAsync(token, playerId, activationKey, ct);
            }

            session.ResetMatch();
            session.HumanPlayerId = null;
            session.BotPlayerId = null;
            session.AccessToken = null;
            session.UserId = null;
            session.ActivationKeys.Clear();
            await lease.SaveAsync(ct);
            return Results.Ok(new { connected = false });
        }, ct));

        app.MapGet("/app-api/lobby", (HttpContext context, EncryptedSessionStore sessions,
            MugginsApiClient api, CancellationToken ct) => ProtectAsync(async () =>
        {
            await using var lease = await sessions.OpenAsync(context, ct);
            var session = lease.Data;
            if (string.IsNullOrWhiteSpace(session.AccessToken))
            {
                return Results.Ok(new LobbyView(false, null, null, null, false,
                    session.MatchId, session.TargetScore, []));
            }

            var profiles = await api.ListPlayersAsync(session.AccessToken, ct);
            var human = FindProfile(profiles, session.HumanPlayerId);
            var bot = FindProfile(profiles, session.BotPlayerId);
            var waiting = await api.ListAvailableMatchesAsync(session.AccessToken, ct);
            var visibleMatchId = session.MatchId;
            if (visibleMatchId is { } currentMatchId &&
                human is not null &&
                session.ActivationKeys.TryGetValue(human.JogadorId, out var humanActivationKey))
            {
                try
                {
                    var matchState = await api.GetMatchStateAsync(
                        session.AccessToken, humanActivationKey, currentMatchId, human.JogadorId, ct);
                    if (matchState.Situacao == SituacaoPartida.Encerrada)
                    {
                        visibleMatchId = null;
                    }
                }
                catch (MugginsApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
                {
                    visibleMatchId = null;
                }
            }

            var canStart = human is not null && bot is not null &&
                session.ActivationKeys.ContainsKey(human.JogadorId) &&
                session.ActivationKeys.ContainsKey(bot.JogadorId) &&
                human.JogadorId != bot.JogadorId;

            return Results.Ok(new LobbyView(
                true,
                session.UserId,
                ToSummary(human, session),
                ToSummary(bot, session),
                canStart,
                visibleMatchId,
                session.TargetScore,
                waiting));
        }, ct));

        app.MapPost("/app-api/players/prepare", (PreparePlayerBody body, HttpContext context,
            EncryptedSessionStore sessions, MugginsApiClient api, CancellationToken ct) => ProtectAsync(async () =>
        {
            await using var lease = await sessions.OpenAsync(context, ct);
            var session = lease.Data;
            var token = RequireToken(session);
            var role = NormalizeRole(body.Role);
            var name = body.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            {
                throw new WebRequestException(StatusCodes.Status400BadRequest,
                    "O nome do jogador deve ter entre 1 e 150 caracteres.");
            }

            var playerId = role == "human" ? session.HumanPlayerId : session.BotPlayerId;
            PerfilJogadorDto profile;
            if (playerId is null)
            {
                profile = await api.CreatePlayerAsync(token, name, ct);
                playerId = profile.JogadorId;
                var activationKey = MugginsApiClient.GeneratePlayerActivationKey();
                if (role == "human")
                {
                    session.HumanPlayerId = playerId;
                }
                else
                {
                    session.BotPlayerId = playerId;
                }

                session.ActivationKeys[playerId.Value] = activationKey;
                await lease.SaveAsync(ct);
            }
            else
            {
                profile = FindProfile(await api.ListPlayersAsync(token, ct), playerId)
                    ?? throw new WebRequestException(StatusCodes.Status404NotFound,
                        "O perfil salvo nesta sessão não existe mais na conta.");
                if (!session.ActivationKeys.ContainsKey(playerId.Value))
                {
                    throw new WebRequestException(StatusCodes.Status409Conflict,
                        "A chave de ativação deste perfil não está disponível nesta sessão. Crie outro perfil ou reconecte a chave original.");
                }
            }

            var key = session.ActivationKeys[playerId.Value];
            profile = await api.ActivatePlayerAsync(token, playerId.Value, key, ct);
            await lease.SaveAsync(ct);
            return Results.Ok(new { role, player = ToSummary(profile, session) });
        }, ct));

        app.MapPost("/app-api/matches/vs-bot", (StartMachineMatchBody body, HttpContext context,
            EncryptedSessionStore sessions, MugginsApiClient api, MugginsBot bot, CancellationToken ct) => ProtectAsync(async () =>
        {
            await using var lease = await sessions.OpenAsync(context, ct);
            var session = lease.Data;
            var token = RequireToken(session);
            var (humanId, humanKey) = GetCredentials(session, "human");
            var (botId, botKey) = GetCredentials(session, "bot");
            if (humanId == botId)
            {
                throw new WebRequestException(StatusCodes.Status400BadRequest,
                    "O perfil humano e o perfil do Muggins precisam ser diferentes.");
            }

            if (body.TargetScore is not (50 or 100 or 150))
            {
                throw new WebRequestException(StatusCodes.Status400BadRequest,
                    "Escolha uma partida de 50, 100 ou 150 pontos.");
            }

            if (session.MatchId is { } oldMatchId)
            {
                try
                {
                    var oldState = await api.GetMatchStateAsync(token, humanKey, oldMatchId, humanId, ct);
                    if (oldState.Situacao != SituacaoPartida.Encerrada)
                    {
                        throw new WebRequestException(StatusCodes.Status409Conflict,
                            "Finalize ou abandone a partida atual antes de abrir outra.");
                    }
                }
                catch (MugginsApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
                {
                    // The saved match was removed from the API; a new one can replace it.
                }
            }

            session.ResetMatch();
            session.TargetScore = body.TargetScore;
            await lease.SaveAsync(ct);

            var created = await api.CreateMatchAsync(token, humanKey, new CriarPartidaRequestDto
            {
                PontuacaoAlvo = body.TargetScore,
                JogadorId = humanId
            }, ct);

            session.MatchId = created.PartidaId;
            session.Round = created.NumeroRodada;
            await lease.SaveAsync(ct);

            var joined = await api.JoinMatchAsync(token, botKey, created.PartidaId, new IngressarRequestDto
            {
                VersaoEsperada = created.VersaoEstado,
                ChaveIdempotencia = Guid.NewGuid(),
                JogadorId = botId
            }, ct);

            var joinedState = joined.Estado ?? await api.GetMatchStateAsync(token, botKey, created.PartidaId, botId, ct);
            var started = await api.StartMatchAsync(token, humanKey, created.PartidaId, new AcaoRequestDto
            {
                VersaoEsperada = joinedState.VersaoEstado,
                ChaveIdempotencia = Guid.NewGuid(),
                JogadorId = humanId
            }, ct);

            var startedState = started.Estado ?? await api.GetMatchStateAsync(token, humanKey, created.PartidaId, humanId, ct);
            session.Round = startedState.NumeroRodada;
            EnsureKnownBoard(session, startedState);
            await lease.SaveAsync(ct);

            var view = await RefreshMatchAsync(session, lease, api, bot, ct);
            return Results.Ok(view);
        }, ct));

        app.MapGet("/app-api/match", (HttpContext context, EncryptedSessionStore sessions,
            MugginsApiClient api, MugginsBot bot, CancellationToken ct) => ProtectAsync(async () =>
        {
            await using var lease = await sessions.OpenAsync(context, ct);
            var view = await RefreshMatchAsync(lease.Data, lease, api, bot, ct);
            return Results.Ok(view);
        }, ct));

        app.MapPost("/app-api/match/action", (HumanActionBody body, HttpContext context,
            EncryptedSessionStore sessions, MugginsApiClient api, MugginsBot bot, CancellationToken ct) => ProtectAsync(async () =>
        {
            await using var lease = await sessions.OpenAsync(context, ct);
            var session = lease.Data;
            var token = RequireToken(session);
            var (humanId, humanKey) = GetCredentials(session, "human");
            var matchId = RequireMatch(session);

            if (session.PendingAction is not null)
            {
                await SubmitPendingActionAsync(session, lease, api, token, ct);
            }

            var state = await api.GetMatchStateAsync(token, humanKey, matchId, humanId, ct);
            EnsureKnownBoard(session, state);
            if (state.Situacao == SituacaoPartida.Encerrada)
            {
                throw new WebRequestException(StatusCodes.Status409Conflict, "A partida já terminou.");
            }

            if (state.JogadorDoTurnoId != humanId)
            {
                throw new WebRequestException(StatusCodes.Status409Conflict, "Ainda não é a sua vez.");
            }

            var ends = GetBoardEnds(session);
            var turn = ApiGameStateMapper.ToTurnState(state, _ => ends);
            var pending = ValidateHumanAction(body, turn, humanId, state.VersaoEstado);
            session.PendingAction = pending;
            await lease.SaveAsync(ct);
            var confirmation = await SubmitPendingActionAsync(session, lease, api, token, ct);

            var view = await RefreshMatchAsync(
                session,
                lease,
                api,
                bot,
                ct,
                runBotTurn: false,
                confirmedHumanState: confirmation.Estado);
            return Results.Ok(view);
        }, ct));

    }

    private static async Task<MatchView> RefreshMatchAsync(
        BrowserSessionData session,
        EncryptedSessionStore.SessionLease lease,
        MugginsApiClient api,
        MugginsBot bot,
        CancellationToken cancellationToken,
        bool runBotTurn = true,
        EstadoPartidaParticipanteDto? confirmedHumanState = null)
    {
        var token = RequireToken(session);
        var (humanId, humanKey) = GetCredentials(session, "human");
        var (botId, botKey) = GetCredentials(session, "bot");
        var matchId = RequireMatch(session);

        if (session.PendingAction is not null)
        {
            var pendingRole = session.PendingAction.Role;
            var confirmation = await SubmitPendingActionAsync(session, lease, api, token, cancellationToken);
            if (pendingRole == "human")
            {
                confirmedHumanState = confirmation.Estado ?? confirmedHumanState;
            }
        }

        var humanState = confirmedHumanState ?? await api.GetMatchStateAsync(
            token, humanKey, matchId, humanId, cancellationToken);
        if (humanState.Situacao == SituacaoPartida.AguardandoInicio)
        {
            var joinedPlayerIds = (humanState.Participantes ?? [])
                .Select(participant => participant.JogadorId)
                .ToHashSet();
            if (!joinedPlayerIds.Contains(botId))
            {
                await api.JoinMatchAsync(token, botKey, matchId, new IngressarRequestDto
                {
                    VersaoEsperada = humanState.VersaoEstado,
                    ChaveIdempotencia = Guid.NewGuid(),
                    JogadorId = botId
                }, cancellationToken);
                humanState = await api.GetMatchStateAsync(token, humanKey, matchId, humanId, cancellationToken);
            }

            if (humanState.Situacao == SituacaoPartida.AguardandoInicio)
            {
                var started = await api.StartMatchAsync(token, humanKey, matchId, new AcaoRequestDto
                {
                    VersaoEsperada = humanState.VersaoEstado,
                    ChaveIdempotencia = Guid.NewGuid(),
                    JogadorId = humanId
                }, cancellationToken);
                humanState = started.Estado ?? await api.GetMatchStateAsync(
                    token, humanKey, matchId, humanId, cancellationToken);
            }

            if (humanState.Situacao == SituacaoPartida.AguardandoInicio)
            {
                throw new WebRequestException(StatusCodes.Status409Conflict,
                    "A API ainda não iniciou esta partida. Atualize a tela e tente continuar novamente.");
            }
        }

        EnsureKnownBoard(session, humanState);

        if (runBotTurn && humanState.Situacao == SituacaoPartida.EmAndamento && humanState.JogadorDoTurnoId == botId)
        {
            var botState = await api.GetMatchStateAsync(token, botKey, matchId, botId, cancellationToken);
            EnsureKnownBoard(session, botState);
            var engineState = ApiGameStateMapper.ToTurnState(botState, _ => GetBoardEnds(session));
            var decision = bot.ChooseAction(engineState);
            if (decision.Kind != BotActionKind.None)
            {
                session.PendingAction = CreateBotPendingAction(decision, botState.VersaoEstado);
                await lease.SaveAsync(cancellationToken);
                await SubmitPendingActionAsync(session, lease, api, token, cancellationToken);
                humanState = await api.GetMatchStateAsync(token, humanKey, matchId, humanId, cancellationToken);
                EnsureKnownBoard(session, humanState);
            }
        }

        return BuildMatchView(session, humanState, humanId, botId);
    }

    private static PendingApiAction ValidateHumanAction(
        HumanActionBody body,
        TurnState state,
        Guid playerId,
        long expectedVersion)
    {
        var kind = body.Kind?.Trim().ToLowerInvariant();
        if (kind == "play")
        {
            if (body.ValueA is not { } a || body.ValueB is not { } b)
            {
                throw new WebRequestException(StatusCodes.Status400BadRequest, "Selecione uma pedra da sua mão.");
            }

            var tile = new Tile(a, b);
            if (!state.Hand.Contains(tile))
            {
                throw new WebRequestException(StatusCodes.Status400BadRequest, "Essa pedra não está na sua mão.");
            }

            MoveSide side;
            if (state.Phase == TurnPhase.Opening)
            {
                if (state.OpeningTile != tile)
                {
                    throw new WebRequestException(StatusCodes.Status400BadRequest,
                        "Jogue primeiro a pedra de abertura indicada pela API.");
                }

                side = MoveSide.Left;
            }
            else if (state.Phase == TurnPhase.Play)
            {
                side = body.Side?.Trim().ToLowerInvariant() switch
                {
                    "left" => MoveSide.Left,
                    "right" => MoveSide.Right,
                    _ => throw new WebRequestException(StatusCodes.Status400BadRequest,
                        "Escolha a ponta esquerda ou direita da mesa.")
                };

                var legal = MugginsRules.LegalPlays(state.Hand, state.Ends);
                if (!legal.Contains(new LegalPlay(tile, side)))
                {
                    throw new WebRequestException(StatusCodes.Status400BadRequest,
                        "A pedra escolhida não encaixa nessa ponta.");
                }
            }
            else
            {
                throw new WebRequestException(StatusCodes.Status409Conflict,
                    "A API não está aguardando uma jogada de pedra.");
            }

            return new PendingApiAction("human", "play", tile.A, tile.B, side,
                expectedVersion, Guid.NewGuid());
        }

        if (kind == "buy" && state.Phase == TurnPhase.Buy)
        {
            return new PendingApiAction("human", "buy", null, null, null,
                expectedVersion, Guid.NewGuid());
        }

        if (kind == "pass" && state.Phase == TurnPhase.Pass)
        {
            return new PendingApiAction("human", "pass", null, null, null,
                expectedVersion, Guid.NewGuid());
        }

        throw new WebRequestException(StatusCodes.Status409Conflict,
            "Essa ação não está disponível no estado atual da partida.");
    }

    private static PendingApiAction CreateBotPendingAction(BotAction action, long expectedVersion) =>
        action.Kind switch
        {
            BotActionKind.Play when action.Tile is { } tile && action.Side is { } side =>
                new PendingApiAction("bot", "play", tile.A, tile.B, side, expectedVersion, Guid.NewGuid()),
            BotActionKind.Buy => new PendingApiAction("bot", "buy", null, null, null, expectedVersion, Guid.NewGuid()),
            BotActionKind.Pass => new PendingApiAction("bot", "pass", null, null, null, expectedVersion, Guid.NewGuid()),
            _ => throw new InvalidOperationException("O motor retornou uma ação incompatível com o turno.")
        };

    private static async Task<ConfirmacaoAcaoPartidaDto> SubmitPendingActionAsync(
        BrowserSessionData session,
        EncryptedSessionStore.SessionLease lease,
        MugginsApiClient api,
        string token,
        CancellationToken cancellationToken)
    {
        var pending = session.PendingAction
            ?? throw new InvalidOperationException("Não há ação pendente para enviar.");
        var (playerId, activationKey) = GetCredentials(session, pending.Role);
        var matchId = RequireMatch(session);

        try
        {
            var confirmation = await (pending.Kind switch
            {
                "play" => api.PlayAsync(token, activationKey, matchId, new JogarRequestDto
                {
                    ValorA = pending.ValueA ?? throw new InvalidOperationException("A ação não contém valorA."),
                    ValorB = pending.ValueB ?? throw new InvalidOperationException("A ação não contém valorB."),
                    Lado = pending.Side == MoveSide.Left ? LadoJogavel.Esquerda : LadoJogavel.Direita,
                    VersaoEsperada = pending.ExpectedVersion,
                    ChaveIdempotencia = pending.IdempotencyKey,
                    JogadorId = playerId
                }, cancellationToken),
                "buy" => api.BuyAsync(token, activationKey, matchId, MakeActionRequest(), cancellationToken),
                "pass" => api.PassAsync(token, activationKey, matchId, MakeActionRequest(), cancellationToken),
                _ => throw new InvalidOperationException("Tipo de ação pendente desconhecido.")
            });

            if (pending.Kind == "play")
            {
                ApplyPlayedTile(session, playerId, pending);
            }

            session.PendingAction = null;
            await lease.SaveAsync(cancellationToken);
            return confirmation;

            AcaoRequestDto MakeActionRequest() => new()
            {
                VersaoEsperada = pending.ExpectedVersion,
                ChaveIdempotencia = pending.IdempotencyKey,
                JogadorId = playerId
            };
        }
        catch (MugginsApiException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            session.PendingAction = null;
            await lease.SaveAsync(cancellationToken);
            throw new WebRequestException(StatusCodes.Status409Conflict,
                exception.ApiError?.Codigo ?? "A partida mudou antes da confirmação. Atualizei a decisão pendente.");
        }
        catch (MugginsApiException exception) when (exception.StatusCode is { } status && (int)status < 500)
        {
            session.PendingAction = null;
            await lease.SaveAsync(cancellationToken);
            throw;
        }
    }

    private static void ApplyPlayedTile(BrowserSessionData session, Guid playerId, PendingApiAction pending)
    {
        if (pending.ValueA is not { } a || pending.ValueB is not { } b || pending.Side is not { } side)
        {
            throw new InvalidOperationException("A jogada pendente está incompleta.");
        }

        var tile = new Tile(a, b);
        var ends = GetBoardEnds(session);
        if (ends.IsEmpty)
        {
            var resulting = MugginsRules.ResultingEnds(ends, tile, MoveSide.Left);
            session.Board.Add(new PlacedTile(tile.A, tile.B, playerId));
            session.LeftEnd = resulting.Left;
            session.RightEnd = resulting.Right;
            session.OpeningTileIndex = 0;
            return;
        }

        var matchingEnd = side == MoveSide.Left ? ends.Left!.Value : ends.Right!.Value;
        var outsideEnd = tile.OtherEnd(matchingEnd);
        var newEnds = MugginsRules.ResultingEnds(ends, tile, side);
        var openingTileIndex = session.OpeningTileIndex
            ?? Math.Max(0, (session.Board.Count - 1) / 2);
        if (side == MoveSide.Left)
        {
            session.Board.Insert(0, new PlacedTile(outsideEnd, matchingEnd, playerId));
            session.OpeningTileIndex = openingTileIndex + 1;
        }
        else
        {
            session.Board.Add(new PlacedTile(matchingEnd, outsideEnd, playerId));
            session.OpeningTileIndex = openingTileIndex;
        }

        session.LeftEnd = newEnds.Left;
        session.RightEnd = newEnds.Right;
    }

    private static void EnsureKnownBoard(BrowserSessionData session, EstadoPartidaParticipanteDto state)
    {
        var apiBoard = state.Tabuleiro;
        if (session.Round > 0 && state.NumeroRodada > session.Round)
        {
            if (apiBoard is { Count: > 0 })
            {
                throw new WebRequestException(StatusCodes.Status409Conflict,
                    "A rodada mudou fora desta tela; não tenho informação suficiente para orientar o tabuleiro com segurança.");
            }

            session.Board.Clear();
            session.LeftEnd = null;
            session.RightEnd = null;
            session.OpeningTileIndex = null;
            session.PendingAction = null;
        }

        session.Round = state.NumeroRodada;
        apiBoard ??= [];

        if (apiBoard.Count != session.Board.Count || !SameTileMultiset(apiBoard, session.Board))
        {
            throw new WebRequestException(StatusCodes.Status409Conflict,
                "O tabuleiro da API não corresponde às jogadas registradas por esta sessão. Comece uma nova partida por esta tela para que o motor possa continuar.");
        }

        if (session.Board.Count == 0)
        {
            session.LeftEnd = null;
            session.RightEnd = null;
        }
        else if (session.LeftEnd is null || session.RightEnd is null)
        {
            throw new WebRequestException(StatusCodes.Status409Conflict,
                "A orientação das pontas não está disponível nesta sessão. O motor não vai adivinhar uma jogada.");
        }
    }

    private static bool SameTileMultiset(
        IReadOnlyList<PecaPartidaDto> apiBoard,
        IReadOnlyList<PlacedTile> localBoard)
    {
        static string Key(int a, int b) => a <= b ? $"{a}:{b}" : $"{b}:{a}";
        var expected = localBoard.GroupBy(tile => Key(tile.LeftValue, tile.RightValue))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (var piece in apiBoard)
        {
            var key = Key(piece.ValorA, piece.ValorB);
            if (!expected.TryGetValue(key, out var count))
            {
                return false;
            }

            if (count == 1)
            {
                expected.Remove(key);
            }
            else
            {
                expected[key] = count - 1;
            }
        }

        return expected.Count == 0;
    }

    private static MatchView BuildMatchView(
        BrowserSessionData session,
        EstadoPartidaParticipanteDto state,
        Guid humanId,
        Guid botId)
    {
        var participants = state.Participantes ?? [];
        var human = participants.FirstOrDefault(player => player.JogadorId == humanId);
        var bot = participants.FirstOrDefault(player => player.JogadorId == botId);
        var turnPlayerId = state.JogadorDoTurnoId;
        var humanTurn = state.Situacao == SituacaoPartida.EmAndamento && turnPlayerId == humanId;
        var turnName = turnPlayerId == humanId ? human?.Nome : turnPlayerId == botId ? bot?.Nome : null;
        var phase = humanTurn ? state.ProximaAcao switch
        {
            ProximaAcaoPartida.JogarAbertura => "Opening",
            ProximaAcaoPartida.Jogar => "Play",
            ProximaAcaoPartida.Comprar => "Buy",
            ProximaAcaoPartida.Passar => "Pass",
            ProximaAcaoPartida.Encerrada => "Finished",
            _ => "Wait"
        } : state.Situacao == SituacaoPartida.Encerrada ? "Finished" : "Wait";
        var ends = GetBoardEnds(session);
        var endsTotal = ends.IsEmpty
            ? 0
            : ends.Left!.Value * (ends.LeftIsDouble ? 2 : 1)
                + ends.Right!.Value * (ends.RightIsDouble ? 2 : 1);

        return new MatchView(
            state.PartidaId,
            session.TargetScore,
            state.NumeroRodada,
            state.VersaoEstado,
            state.Situacao.ToString(),
            phase,
            humanTurn,
            turnName,
            human?.Nome,
            bot?.Nome,
            human?.Pontuacao ?? 0,
            human?.QuantidadePedrasNaMao ?? (state.MinhaMao?.Count ?? 0),
            bot?.Pontuacao ?? 0,
            bot?.QuantidadePedrasNaMao ?? 0,
            state.VencedorId,
            state.VencedorId == humanId ? human?.Nome : state.VencedorId == botId ? bot?.Nome : null,
            session.LeftEnd,
            session.RightEnd,
            session.OpeningTileIndex,
            endsTotal,
            session.Board.Select(tile => new BoardTileView(
                tile.LeftValue,
                tile.RightValue,
                tile.LeftValue == tile.RightValue,
                tile.PlayerId == botId)).ToArray(),
            state.MinhaMao ?? [],
            state.PedraAberturaObrigatoria);
    }

    private static BoardEnds GetBoardEnds(BrowserSessionData session)
    {
        if (session.LeftEnd is null && session.RightEnd is null)
        {
            return new BoardEnds();
        }

        if (session.LeftEnd is null || session.RightEnd is null)
        {
            throw new WebRequestException(StatusCodes.Status409Conflict,
                "A sessão tem apenas uma ponta do tabuleiro registrada.");
        }

        var sameTileAtBothEnds = session.Board.Count == 1;
        var leftIsDouble = session.Board.Count > 1 &&
            session.Board[0].LeftValue == session.Board[0].RightValue;
        var rightmost = session.Board.Count - 1;
        var rightIsDouble = session.Board.Count > 1 &&
            session.Board[rightmost].LeftValue == session.Board[rightmost].RightValue;
        return new BoardEnds(
            session.LeftEnd,
            session.RightEnd,
            leftIsDouble,
            rightIsDouble,
            sameTileAtBothEnds && session.Board[0].LeftValue == session.Board[0].RightValue);
    }

    private static (Guid PlayerId, string ActivationKey) GetCredentials(BrowserSessionData session, string role)
    {
        var playerId = role == "human" ? session.HumanPlayerId : session.BotPlayerId;
        if (playerId is null || !session.ActivationKeys.TryGetValue(playerId.Value, out var activationKey))
        {
            throw new WebRequestException(StatusCodes.Status409Conflict,
                role == "human"
                    ? "Prepare o perfil do jogador antes de continuar."
                    : "Prepare o perfil do Muggins antes de continuar.");
        }

        return (playerId.Value, activationKey);
    }

    private static string RequireToken(BrowserSessionData session) =>
        !string.IsNullOrWhiteSpace(session.AccessToken)
            ? session.AccessToken
            : throw new WebRequestException(StatusCodes.Status401Unauthorized, "Conecte sua conta FIAP primeiro.");

    private static Guid RequireMatch(BrowserSessionData session) =>
        session.MatchId is { } matchId
            ? matchId
            : throw new WebRequestException(StatusCodes.Status404NotFound, "Nenhuma partida está ativa nesta sessão.");

    private static string NormalizeRole(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "human" => "human",
        "bot" => "bot",
        _ => throw new WebRequestException(StatusCodes.Status400BadRequest, "Escolha o perfil humano ou o perfil do Muggins.")
    };

    private static PerfilJogadorDto? FindProfile(IReadOnlyList<PerfilJogadorDto> profiles, Guid? playerId) =>
        playerId is { } id ? profiles.FirstOrDefault(profile => profile.JogadorId == id) : null;

    private static PlayerSummary? ToSummary(PerfilJogadorDto? profile, BrowserSessionData session) =>
        profile is null
            ? null
            : new PlayerSummary(profile.JogadorId, profile.Nome, profile.Ativo,
                profile.Ativo && session.ActivationKeys.ContainsKey(profile.JogadorId));

    private static async Task<IResult> ProtectAsync(Func<Task<IResult>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return WebErrors.ToResult(exception);
        }
    }
}
