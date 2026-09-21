using RtsGame.Sim.World;

namespace RtsGame.Sim.Pathing;

public sealed class FlowFieldCache
{
    public const int Capacity = 32;
    private readonly Grid _grid;
    private readonly FlowField?[] _fields = new FlowField[Capacity];
    private readonly long[] _used = new long[Capacity];
    private long _serial;
    public int BuildCount { get; private set; }
    public FlowFieldCache(Grid grid) { _grid = grid; }
    public FlowField Get(int x, int y)
    {
        int victim = 0;
        for (int i = 0; i < Capacity; i++)
        {
            if (_fields[i] is FlowField field && field.TargetX == x && field.TargetY == y)
            { _used[i] = checked(++_serial); return field; }
            if (_used[i] < _used[victim]) victim = i;
        }
        var created = new FlowField(_grid,x,y);
        _fields[victim] = created; _used[victim] = checked(++_serial); BuildCount++;
        return created;
    }
    public void Invalidate(int x, int y)
    {
        for (int i = 0; i < Capacity; i++)
            if (_fields[i] is FlowField field && field.TargetX == x && field.TargetY == y)
            { _fields[i] = null; _used[i] = 0; }
    }
}
