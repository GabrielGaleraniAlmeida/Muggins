using System.Text.Json;
using Muggins.Engine.Api;
using Muggins.Engine.Bot;
using Muggins.Engine.Domain;
using Muggins.Engine.Rules;

namespace Muggins.Engine.Tests;

internal static class EngineTests
{
    public static Task RulesScoreAndFindLegalPlaysAsync()
    {
        TestAssert.Equal(1, MugginsRules.PointsForEnds(new BoardEnds(2, 3)), "A total of five scores one point.");
        TestAssert.Equal(0, MugginsRules.PointsForEnds(new BoardEnds(2, 4)), "A non-multiple of five scores zero.");
        TestAssert.Equal(3, MugginsRules.PointsForEnds(new BoardEnds(3, 6, rightIsDouble: true)),
            "An exposed 6|6 counts both halves, making 3 + 12 score three points.");

        var doubleOpening = MugginsRules.ResultingEnds(new BoardEnds(), new Tile(5, 5), MoveSide.Left);
        TestAssert.Equal(2, MugginsRules.PointsForEnds(doubleOpening),
            "An opening double contributes its two exposed halves once each.");
        var extendedDoubleOpening = MugginsRules.ResultingEnds(doubleOpening, new Tile(3, 5), MoveSide.Left);
        TestAssert.True(extendedDoubleOpening.RightIsDouble,
            "When one side is covered, the remaining exposed side of the opening double counts twice.");

        var playedAfterDoubleSix = MugginsRules.ResultingEnds(
            new BoardEnds(4, 6, rightIsDouble: true), new Tile(3, 4), MoveSide.Left);
        TestAssert.Equal(3, MugginsRules.PointsForEnds(playedAfterDoubleSix),
            "Playing 3|4 opposite an open 6|6 leaves 3 + 12 and scores three points.");

        var openingSixes = MugginsRules.ResultingEnds(new BoardEnds(), new Tile(6, 6), MoveSide.Left);
        var afterSixTwo = MugginsRules.ResultingEnds(openingSixes, new Tile(2, 6), MoveSide.Left);
        var afterDoubleTwo = MugginsRules.ResultingEnds(afterSixTwo, new Tile(2, 2), MoveSide.Left);
        var afterTwoThree = MugginsRules.ResultingEnds(afterDoubleTwo, new Tile(2, 3), MoveSide.Left);
        TestAssert.Equal(3, MugginsRules.PointsForEnds(afterTwoThree),
            "After 6|6, 6|2, and 2|2, playing 2|3 leaves 12 + 3 and scores three points.");

        var plays = MugginsRules.LegalPlays(
            [new Tile(1, 4), new Tile(2, 5)],
            new BoardEnds(1, 5));

        TestAssert.Equal(2, plays.Count, "Legal plays should include tiles matching either end.");
        TestAssert.True(plays.Any(play => play.Tile == new Tile(1, 4) && play.Side == MoveSide.Left),
            "The left matching play should be available.");
        TestAssert.True(plays.Any(play => play.Tile == new Tile(2, 5) && play.Side == MoveSide.Right),
            "The right matching play should be available.");
        return Task.CompletedTask;
    }

    public static Task BotChoosesRequiredOpeningAsync()
    {
        var required = new Tile(6, 6);
        var state = new TurnState([required, new Tile(2, 3)], new BoardEnds(), TurnPhase.Opening, required);
        var action = new MugginsBot().ChooseAction(state);

        TestAssert.Equal(BotActionKind.Play, action.Kind, "The bot should play the opening tile.");
        TestAssert.Equal(required, action.Tile!.Value, "The bot should play the required tile.");
        TestAssert.Equal(MoveSide.Left, action.Side!.Value, "The opening uses the left side.");
        return Task.CompletedTask;
    }

    public static Task BotHandlesAuthorizedPhasesAsync()
    {
        var bot = new MugginsBot();
        var expectedActions = new[]
        {
            (TurnPhase.Wait, BotActionKind.None),
            (TurnPhase.Finished, BotActionKind.None),
            (TurnPhase.Buy, BotActionKind.Buy),
            (TurnPhase.Pass, BotActionKind.Pass)
        };

        foreach (var (phase, expectedKind) in expectedActions)
        {
            var action = bot.ChooseAction(new TurnState([], new BoardEnds(), phase));
            TestAssert.Equal(expectedKind, action.Kind,
                $"The bot should return {expectedKind} when the API phase is {phase}.");
        }

        return Task.CompletedTask;
    }

    public static Task BotChoosesHighestScoringLegalPlayAsync()
    {
        var state = new TurnState(
            [new Tile(6, 4), new Tile(2, 3), new Tile(0, 1)],
            new BoardEnds(2, 6, rightIsDouble: true),
            TurnPhase.Play);
        var action = new MugginsBot().ChooseAction(state);

        TestAssert.Equal(BotActionKind.Play, action.Kind, "A playable turn should produce a play action.");
        TestAssert.Equal(new Tile(2, 3), action.Tile!.Value,
            "The bot should choose the legal play that scores three points immediately.");
        TestAssert.Equal(MoveSide.Left, action.Side!.Value,
            "The chosen 2|3 must be played at the left endpoint to score.");
        TestAssert.Equal(3, action.EstimatedPoints,
            "The bot's estimate should count the exposed double-six as twelve.");
        TestAssert.True(MugginsRules.LegalPlays(state.Hand, state.Ends)
                .Contains(new LegalPlay(action.Tile.Value, action.Side.Value)),
            "The bot must only return a move currently legal at the API-reported ends.");
        return Task.CompletedTask;
    }

    public static Task ApiJsonAndBoardMappingAsync()
    {
        const string json = """{"partidaId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","versaoEstado":9,"situacao":1,"numeroRodada":2,"proximaAcao":2,"minhaMao":[{"valorA":1,"valorB":4}],"tabuleiro":[],"participantes":[],"pedraAberturaObrigatoria":{"valorA":6,"valorB":6},"participacaoId":"cccccccc-cccc-cccc-cccc-cccccccccccc"}""";
        var dto = JsonSerializer.Deserialize<EstadoPartidaParticipanteDto>(json, MugginsApiJson.Options)
            ?? throw new InvalidOperationException("API state JSON deserialized to null.");

        TestAssert.Equal(ProximaAcaoPartida.Jogar, dto.ProximaAcao, "Numeric API enums should deserialize.");
        var emptyBoardState = ApiGameStateMapper.ToTurnState(dto);
        TestAssert.Equal(new Tile(1, 4), emptyBoardState.Hand[0], "CamelCase hand fields should map to engine tiles.");
        TestAssert.True(emptyBoardState.Ends.IsEmpty, "An empty API board should map to empty ends.");

        var occupiedState = dto with
        {
            Tabuleiro =
            [
                new PecaPartidaDto { ValorA = 2, ValorB = 4 },
                new PecaPartidaDto { ValorA = 4, ValorB = 4 }
            ]
        };
        var mappedBoard = ApiGameStateMapper.ToTurnState(occupiedState);
        TestAssert.Equal(new BoardEnds(2, 4, rightIsDouble: true), mappedBoard.Ends,
            "An oriented API board should map its endpoints and exposed double.");
        TestAssert.Equal(2, MugginsRules.PointsForEnds(mappedBoard.Ends),
            "An exposed double should count both halves when calculating points.");

        var doubleOpeningState = dto with
        {
            Tabuleiro = [new PecaPartidaDto { ValorA = 6, ValorB = 6 }]
        };
        var doubleOpening = ApiGameStateMapper.ToTurnState(doubleOpeningState);
        TestAssert.Equal(new BoardEnds(6, 6, sameDoubleTileAtBothEnds: true), doubleOpening.Ends,
            "A single opening double should be represented at both endpoints.");

        var disconnectedState = dto with
        {
            Tabuleiro =
            [
                new PecaPartidaDto { ValorA = 1, ValorB = 4 },
                new PecaPartidaDto { ValorA = 2, ValorB = 3 }
            ]
        };
        TestAssert.Throws<InvalidOperationException>(
            () => ApiGameStateMapper.ToTurnState(disconnectedState),
            "A disconnected API board must be rejected.");

        var overridden = ApiGameStateMapper.ToTurnState(occupiedState, _ => new BoardEnds(1, 4));
        TestAssert.Equal(new BoardEnds(1, 4), overridden.Ends, "A supplied resolver should override default mapping.");
        return Task.CompletedTask;
    }
}
