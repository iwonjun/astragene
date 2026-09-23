using System;
using System.Collections.Generic;
namespace RtsGame.Sim.Commands;
public sealed class TurnManager
{
    public const int Window=128;
    private readonly CommandPacket?[,] _packets=new CommandPacket?[Window,4];
    private readonly bool[] _active=new bool[4];
    private readonly int[] _leaveAt={int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue};
    public int NextTurn {get;private set;}
    public int InputDelay {get;private set;}=3;
    public TurnManager(ReadOnlySpan<int> players){foreach(int p in players){if((uint)p>=4 || _active[p])throw new ArgumentException("Invalid participants.");_active[p]=true;}if(players.Length==0)throw new ArgumentException("No participants.");}
    public static int DelayForRtt(int milliseconds)=>Math.Clamp((milliseconds+99)/100+2,2,6);
    public void SetInputDelay(int turns)=>InputDelay=Math.Clamp(turns,2,6);
    public void ScheduleLeave(int player,int turn){if((uint)player>=4 || turn<NextTurn)throw new ArgumentException("Invalid leave turn.");_leaveAt[player]=Math.Min(_leaveAt[player],turn);}
    public bool Submit(CommandPacket packet)
    {
        if(packet.Turn<NextTurn)return false;
        if(packet.Turn>=NextTurn+Window || !_active[packet.PlayerId])throw new ArgumentException("Packet outside turn window.");
        int slot=packet.Turn%Window;var previous=_packets[slot,packet.PlayerId];
        if(previous!=null){if(!previous.Encode().AsSpan().SequenceEqual(packet.Encode()))throw new InvalidOperationException("Conflicting duplicate packet.");return false;}
        _packets[slot,packet.PlayerId]=packet;return true;
    }
    public int MissingMask
    {
        get{int mask=0;for(int p=0;p<4;p++)if(_active[p] && NextTurn<_leaveAt[p] && _packets[NextTurn%Window,p]==null)mask|=1<<p;return mask;}
    }
    public bool TryTake(out CommandPacket[] packets,out Command[] commands)
    {
        packets=Array.Empty<CommandPacket>();commands=Array.Empty<Command>();if(MissingMask!=0)return false;
        var list=new List<CommandPacket>(4);var joined=new List<Command>();int slot=NextTurn%Window;
        for(int p=0;p<4;p++)
        {
            if(!_active[p])continue;
            if(NextTurn>=_leaveAt[p]){if(NextTurn==_leaveAt[p])joined.Add(new Command(CommandType.Leave,p,Entities.EntityId.None,Entities.EntityId.None,Core.Fix2.Zero));_packets[slot,p]=null;continue;}
            var packet=_packets[slot,p]!;list.Add(packet);foreach(var c in packet.Commands)joined.Add(c);_packets[slot,p]=null;
        }
        NextTurn++;packets=list.ToArray();commands=joined.ToArray();return true;
    }
}
