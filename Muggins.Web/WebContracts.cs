using Muggins.Engine.Api;

namespace Muggins.Web;

public sealed record RequestTokenBody(string? Email);
public sealed record ConnectBody(string? Token);
public sealed record PreparePlayerBody(string? Role, string? Name);
public sealed record StartMachineMatchBody(int TargetScore);
public sealed record HumanActionBody(string? Kind, int? ValueA, int? ValueB, string? Side);

public sealed record PlayerSummary(
    Guid PlayerId,
    string? Name,
    bool Active,
    bool Ready);

public sealed record LobbyView(
    bool Connected,
    string? UserId,
    PlayerSummary? HumanPlayer,
    PlayerSummary? BotPlayer,
    bool CanStartMachineMatch,
    Guid? ActiveMatchId,
    int TargetScore,
    IReadOnlyList<PartidaDisponivelDto> WaitingMatches);

public sealed record BoardTileView(int LeftValue, int RightValue, bool IsDouble, bool IsBotTile);

public sealed record MatchView(
    Guid MatchId,
    int TargetScore,
    int Round,
    long Version,
    string Situation,
    string Phase,
    bool IsHumanTurn,
    string? TurnPlayerName,
    string? HumanName,
    string? BotName,
    int HumanScore,
    int HumanTileCount,
    int BotScore,
    int BotTileCount,
    Guid? WinnerId,
    string? WinnerName,
    int? LeftEnd,
    int? RightEnd,
    int? OpeningTileIndex,
    int EndsTotal,
    IReadOnlyList<BoardTileView> Board,
    IReadOnlyList<PecaPartidaDto> Hand,
    PecaPartidaDto? RequiredOpeningTile);

public sealed record WebError(string Message);
