using System;

namespace RtsGame.Sim.World;

public readonly struct Tile
{
    public bool Walkable { get; }
    public bool Buildable { get; }
    public byte Height { get; }
    public int ResourceNodeId { get; }
    public Tile(bool walkable, bool buildable, byte height, int resourceNodeId = -1)
    {
        if (height > 2 || resourceNodeId < -1) throw new ArgumentOutOfRangeException(nameof(height));
        if (buildable && !walkable) throw new ArgumentException("Buildable tile must be walkable.");
        Walkable = walkable; Buildable = buildable; Height = height; ResourceNodeId = resourceNodeId;
    }
}
