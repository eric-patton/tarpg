namespace Tarpg.Core;

public readonly record struct Position(int X, int Y)
{
    public static readonly Position Zero = new(0, 0);

    public Position North => this with { Y = Y - 1 };
    public Position South => this with { Y = Y + 1 };
    public Position East  => this with { X = X + 1 };
    public Position West  => this with { X = X - 1 };

    public Position Translate(int dx, int dy) => new(X + dx, Y + dy);

    public int ManhattanTo(Position other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public int ChebyshevTo(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
}
