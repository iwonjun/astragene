namespace RtsGame.Sim.World;

/// <summary>Named tick sections for profiling hooks.</summary>
public static class TickSection
{
    public const int Commands = 0, Ai = 1, Orders = 2, Vision = 3, Economy = 4, Production = 5, Movement = 6, Combat = 7, Outcome = 8, Count = 9;
    public static readonly string[] Names = { "Commands", "AI", "Orders/Queues", "Vision", "Economy", "Production", "Movement", "Combat", "Outcome" };
}

/// <summary>
/// Implemented outside the simulation (tests, engine-side diagnostics). Sim only calls Begin/End and never reads a
/// clock itself, so profiling cannot influence determinism.
/// </summary>
public interface ITickProfiler
{
    void Begin(int section);
    void End(int section);
}
