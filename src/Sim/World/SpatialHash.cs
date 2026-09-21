using System;
using System.Collections.Generic;

namespace RtsGame.Sim.World;

/// <summary>4-tile cells; queries return sorted IDs, independent of insertion order.</summary>
public sealed class SpatialHash
{
    public const int CellSize = 4;
    private const int Side = Grid.Size / CellSize;
    private readonly List<int>[] _cells = new List<int>[Side * Side];
    private readonly int[] _entityCell;
    public SpatialHash(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _entityCell = new int[capacity];
        Array.Fill(_entityCell, -1);
        for (int i = 0; i < _cells.Length; i++) _cells[i] = new List<int>();
    }
    public void Update(int id, int tileX, int tileY)
    {
        ValidateId(id);
        if (!Grid.Contains(tileX, tileY)) throw new ArgumentOutOfRangeException(nameof(tileX));
        int cell = tileY / CellSize * Side + tileX / CellSize;
        if (_entityCell[id] == cell) return;
        Remove(id);
        List<int> bucket = _cells[cell];
        int insertion = bucket.BinarySearch(id);
        bucket.Insert(~insertion, id);
        _entityCell[id] = cell;
    }
    public void Remove(int id)
    {
        ValidateId(id);
        int previous = _entityCell[id];
        if (previous >= 0) _cells[previous].Remove(id);
        _entityCell[id] = -1;
    }
    public void Query(int minX, int minY, int maxX, int maxY, List<int> output)
    {
        output.Clear();
        if (minX > maxX || minY > maxY) throw new ArgumentException("Invalid query bounds.");
        if (maxX < 0 || maxY < 0 || minX >= Grid.Size || minY >= Grid.Size) return;
        minX = Math.Max(0, minX) / CellSize; minY = Math.Max(0, minY) / CellSize;
        maxX = Math.Min(Grid.Size - 1, maxX) / CellSize; maxY = Math.Min(Grid.Size - 1, maxY) / CellSize;
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++) output.AddRange(_cells[y * Side + x]);
        output.Sort();
    }
    private void ValidateId(int id)
    {
        if ((uint)id >= (uint)_entityCell.Length) throw new ArgumentOutOfRangeException(nameof(id));
    }
}
