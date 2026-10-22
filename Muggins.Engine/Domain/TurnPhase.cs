namespace Muggins.Engine.Domain;

/// <summary>Action currently authorized by the API's confirmed player state.</summary>
public enum TurnPhase
{
    Opening,
    Play,
    Buy,
    Pass,
    Wait,
    Finished
}
