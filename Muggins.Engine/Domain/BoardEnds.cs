namespace Muggins.Engine.Domain;

/// <summary>Exposed values at both ends of the table. Two nulls means an empty table.</summary>
public readonly record struct BoardEnds
{
    public int? Left { get; }
    public int? Right { get; }

    public BoardEnds(int? left = null, int? right = null)
    {
        if (left.HasValue != right.HasValue)
        {
            throw new ArgumentException("Both ends must be set, or both must be null.");
        }

        if (left is < 0 or > 6 || right is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(left), "End values must be between 0 and 6.");
        }

        Left = left;
        Right = right;
    }

    public bool IsEmpty => Left is null;
}
