namespace RtsGame.Sim.Commands;

public enum UnitState { Idle, Moving, Attacking, Holding, Patrolling, Following, Building, Training, Gathering, Repairing }
internal static class CommandStateMachine
{
    private static readonly UnitState[] Transitions =
    {
        UnitState.Moving, UnitState.Moving, UnitState.Attacking, UnitState.Idle, UnitState.Holding,
        UnitState.Patrolling, UnitState.Following, UnitState.Building, UnitState.Training, UnitState.Idle,
        UnitState.Idle, UnitState.Gathering, UnitState.Repairing
    };
    internal static UnitState Next(CommandType command) => Transitions[(int)command];
}
