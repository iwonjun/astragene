namespace RtsGame.Sim.Core;

public sealed class SimClock
{
    public const int TicksPerSecond = 20;
    public const int TicksPerTurn = 2;
    public long Tick { get; private set; }
    public long Turn => Tick / TicksPerTurn;
    public Fix64 Time => Fix64.FromRatio(Tick, TicksPerSecond);
    public void Advance() => Tick = checked(Tick + 1);
}
