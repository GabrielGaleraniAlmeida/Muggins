using Muggins.Engine.Domain;

namespace Muggins.Engine.Rules;

public readonly record struct LegalPlay(Tile Tile, MoveSide Side);
