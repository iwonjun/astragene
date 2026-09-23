using Godot;
using RtsGame.Net;
using RtsGame.Sim.Commands;
using RtsGame.Sim.World;

/// <summary>In-process ENet loopback: lobby handshake, seed agreement, chat relay and 200 lockstep ticks.</summary>
public partial class NetChecks : Node
{
    public string RunChecks()
    {
        ulong mapHash = MapLoader.Load(FileAccess.GetFileAsBytes(MatchLaunch.DefaultMap)).Hash();
        var host = NetworkSession.Host(27455, 0, mapHash);
        if (host.Phase != SessionPhase.Lobby) return "Host failed: " + host.LastError;
        var client = NetworkSession.Join("127.0.0.1", 27455, mapHash);
        string chat = "";
        host.ChatReceived += (player, text, team) => chat = $"{player}:{text}";
        try
        {
            if (!Pump(host, client, () => client.Phase == SessionPhase.Lobby && host.Slots.Count == 2)) return "Client was not admitted";
            if (client.LocalPlayer != 1 || host.Slots.Faction(1) != 1) return "Seat or default faction mismatch";
            client.SetFaction(0); client.SetReady(true); host.SetReady(true);
            if (!Pump(host, client, () => host.CanStart && client.Slots.Ready(1) && client.Slots.Faction(1) == 0)) return "Ready state did not reach host";
            client.SendChat("준비 완료!", false);
            if (!host.StartMatch()) return "Host could not start";
            if (!Pump(host, client, () => client.Phase == SessionPhase.InMatch && chat != "")) return "Start or chat did not arrive";
            if (client.Seed != host.Seed) return "Seed disagreement";
            if (chat != "1:준비 완료!") return "Chat relay mismatch: " + chat;

            var map = MapLoader.Load(FileAccess.GetFileAsBytes(MatchLaunch.DefaultMap));
            var spawns = MatchSetup.Spawns(new[] { 0, 0 });
            var a = new LockstepSession(new SimWorld(map, spawns, host.Seed), 0, new[] { 0, 1 }, host.InputDelay);
            var b = new LockstepSession(new SimWorld(map, spawns, client.Seed), 1, new[] { 0, 1 }, client.InputDelay);
            for (int frame = 0; frame < 4000 && (a.World.TickNumber < 200 || b.World.TickNumber < 200); frame++)
            {
                while (a.TryDequeueOutgoing(out var p)) host.SendTurn(p);
                while (b.TryDequeueOutgoing(out var p)) client.SendTurn(p);
                host.Poll(); client.Poll();
                while (host.TryDequeueTurn(out var p)) a.Receive(p);
                while (client.TryDequeueTurn(out var p)) b.Receive(p);
                if (a.World.TickNumber < 200) a.TryStep();
                if (b.World.TickNumber < 200) b.TryStep();
                if (frame % 4 == 0) OS.DelayMsec(1);
            }
            if (a.World.TickNumber != 200 || b.World.TickNumber != 200) return $"Lockstep stalled at {a.World.TickNumber}/{b.World.TickNumber}";
            if (a.World.Hash() != b.World.Hash() || a.VerifiedCheckpoints == 0) return "Lockstep hash mismatch";
            client.Close();
            if (!Pump(host, null, () => host.TryDequeueLeave(out var leave) && leave.Player == 1)) return "Leave notice missing";
            return "";
        }
        finally { host.Close(); client.Close(); }
    }
    private static bool Pump(NetworkSession host, NetworkSession? client, System.Func<bool> done)
    {
        for (int i = 0; i < 600; i++) { host.Poll(); client?.Poll(); if (done()) return true; OS.DelayMsec(5); }
        return false;
    }
}
