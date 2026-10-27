namespace Muggins.Engine.Domain;

/// <summary>Exposed values and double-tile state at both ends of the table.</summary>
public readonly record struct BoardEnds
{
    public int? Left { get; }
    public int? Right { get; }
    public bool LeftIsDouble { get; }
    public bool RightIsDouble { get; }
    public bool SameDoubleTileAtBothEnds { get; }

    public BoardEnds(
        int? left = null,
        int? right = null,
        bool leftIsDouble = false,
        bool rightIsDouble = false,
        bool sameDoubleTileAtBothEnds = false)
    {
        if (left.HasValue != right.HasValue)
        {
            throw new ArgumentException("Both ends must be set, or both must be null.");
        }

        if (left is < 0 or > 6 || right is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(left), "End values must be between 0 and 6.");
        }

        if (left is null && (leftIsDouble || rightIsDouble || sameDoubleTileAtBothEnds))
        {
            throw new ArgumentException("An empty board cannot have double-tile endpoints.");
        }

        if (sameDoubleTileAtBothEnds && (left != right || leftIsDouble || rightIsDouble))
        {
            throw new ArgumentException("Shared double endpoints must refer to one unweighted double tile.");
        }

        Left = left;
        Right = right;
        LeftIsDouble = leftIsDouble;
        RightIsDouble = rightIsDouble;
        SameDoubleTileAtBothEnds = sameDoubleTileAtBothEnds;
    }

    public bool IsEmpty => Left is null;
}
