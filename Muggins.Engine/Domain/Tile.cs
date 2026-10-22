namespace Muggins.Engine.Domain;

/// <summary>A domino tile. The order of its pips does not affect its identity.</summary>
public readonly record struct Tile
{
    public int A { get; }
    public int B { get; }

    public Tile(int a, int b)
    {
        if (a is < 0 or > 6 || b is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(a), "Tile values must be between 0 and 6.");
        }

        (A, B) = a <= b ? (a, b) : (b, a);
    }

    public int Total => A + B;
    public bool IsDouble => A == B;

    public int OtherEnd(int matchingValue)
    {
        if (A == matchingValue)
        {
            return B;
        }

        if (B == matchingValue)
        {
            return A;
        }

        throw new InvalidOperationException($"Tile {this} does not match value {matchingValue}.");
    }

    public override string ToString() => $"{A}|{B}";
}
