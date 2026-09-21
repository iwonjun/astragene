namespace RtsGame.Sim.Entities;

public readonly record struct EntityId(int Index, uint Generation)
{
    public static EntityId None => new(-1, 0);
}
