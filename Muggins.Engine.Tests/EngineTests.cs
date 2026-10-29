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
            Tabuleiro = [new PecaPartidaDto { ValorA = 1, ValorB = 4 }]
        };
        TestAssert.Throws<InvalidOperationException>(
            () => ApiGameStateMapper.ToTurnState(occupiedState),
            "An occupied board without a confirmed resolver must fail closed.");

        var resolved = ApiGameStateMapper.ToTurnState(occupiedState, _ => new BoardEnds(1, 4));
        TestAssert.Equal(new BoardEnds(1, 4), resolved.Ends, "A supplied resolver should map board ends.");
        return Task.CompletedTask;
    }
}
