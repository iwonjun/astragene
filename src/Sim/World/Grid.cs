using System;

namespace RtsGame.Sim.World;

public sealed class Grid
{
    public const int Size = 128;
    private readonly Tile[] _tiles;
    public Grid(ReadOnlySpan<Tile> tiles)
    {
        if (tiles.Length != Size * Size) throw new ArgumentException("Expected 128x128 tiles.");
        _tiles = tiles.ToArray();
    }
    public static bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
    public Tile this[int x, int y]
    {
        get
        {
            if (!Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
            return _tiles[y * Size + x];
        }
    }
}
