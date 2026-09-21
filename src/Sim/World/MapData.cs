using RtsGame.Sim.Core;

namespace RtsGame.Sim.World;

public sealed class MapData
{
    public Grid Grid { get; }
    public MapData(Grid grid) { Grid = grid ?? throw new System.ArgumentNullException(nameof(grid)); }
    public ulong Hash()
    {
        var hash = new WorldHasher();
        hash.AddInt32(World.Grid.Size);
        hash.AddInt32(World.Grid.Size);
        for (int y = 0; y < World.Grid.Size; y++)
        for (int x = 0; x < World.Grid.Size; x++)
        {
            Tile tile = Grid[x,y];
            hash.AddByte((byte)((tile.Walkable ? 1 : 0) | (tile.Buildable ? 2 : 0)));
            hash.AddByte(tile.Height);
            hash.AddInt32(tile.ResourceNodeId);
        }
        return hash.Value;
    }
}
