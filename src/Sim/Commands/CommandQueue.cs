using RtsGame.Sim.Core;

namespace RtsGame.Sim.Commands;

internal sealed class CommandQueue
{
    internal const int Capacity=8;
    private readonly Command[] _items=new Command[Capacity];
    private int _head;
    internal int Count {get;private set;}
    internal bool Enqueue(Command command)
    {
        if(Count==Capacity)return false;
        _items[(_head+Count)%Capacity]=command; Count++; return true;
    }
    internal bool TryDequeue(out Command command)
    {
        if(Count==0){command=default;return false;}
        command=_items[_head]; _head=(_head+1)%Capacity; Count--; return true;
    }
    internal void Clear(){_head=0;Count=0;}
    internal void Hash(ref WorldHasher hash)
    {
        hash.AddInt32(Count);
        System.Span<byte> bytes=stackalloc byte[Command.ByteSize];
        for(int i=0;i<Count;i++){_items[(_head+i)%Capacity].Write(bytes);hash.Add(bytes);}
    }
}
