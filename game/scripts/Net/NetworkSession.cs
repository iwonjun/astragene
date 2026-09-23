using System;
using System.Collections.Generic;
using Godot;
using RtsGame.Sim.Commands;
using RtsGame.Sim.World;

namespace RtsGame.Net;

public enum SessionPhase { Connecting, Lobby, InMatch, Closed }

/// <summary>
/// ENet transport. Only CommandPacket bytes cross the wire on the single reliable channel; no RPC,
/// MultiplayerSynchronizer or scene replication is used. The host relays packets and coordinates
/// lobby/turn membership but has no simulation authority.
/// </summary>
public sealed class NetworkSession
{
    public const int DefaultPort = 27415, MapCapacity = 2;
    public const int RejectVersion = 1, RejectFull = 2, RejectStarted = 3;
    private readonly ENetMultiplayerPeer _peer = new();
    private readonly Dictionary<int, int> _peerSlot = new();
    private readonly int[] _slotPeer = new int[4];
    private readonly int[] _lastTurn = { -1, -1, -1, -1 };
    private readonly ulong _content = MatchSetup.ContentHash(), _mapHash;
    private bool _helloSent;
    public bool IsHost { get; }
    public SessionPhase Phase { get; private set; } = SessionPhase.Connecting;
    public int LocalPlayer { get; private set; } = -1;
    public LobbySlots Slots { get; private set; }
    public ulong Seed { get; private set; }
    public int InputDelay { get; private set; } = 3;
    public int Map { get; private set; }
    public string LastError { get; private set; } = "";
    public event Action? LobbyChanged, Started;
    // Turn and leave notices are queued, never evented: they may arrive while the scene changes from lobby to match.
    private readonly Queue<CommandPacket> _turnsIn = new();
    private readonly Queue<(int Player, int Turn)> _leaves = new();
    public event Action<int, string, bool>? ChatReceived;
    public bool TryDequeueTurn(out CommandPacket packet) => _turnsIn.TryDequeue(out packet!);
    public bool TryDequeueLeave(out (int Player, int Turn) leave) => _leaves.TryDequeue(out leave);
    public event Action<string>? Failed;

    private NetworkSession(bool host, ulong mapHash) { IsHost = host; _mapHash = mapHash; _peer.TransferMode = MultiplayerPeer.TransferModeEnum.Reliable; _peer.TransferChannel = 0; }

    public static NetworkSession Host(int port, int faction, ulong mapHash)
    {
        var s = new NetworkSession(true, mapHash);
        var error = s._peer.CreateServer(port, 3);
        if (error != Error.Ok) { s.Fail($"호스트 생성 실패 ({error})"); return s; }
        s._peer.PeerConnected += s.OnConnected; s._peer.PeerDisconnected += s.OnDisconnected;
        s.LocalPlayer = 0; s.Slots = new LobbySlots(0).With(0, true, faction, false); s.Phase = SessionPhase.Lobby;
        s.Seed = GD.Randi() | (ulong)GD.Randi() << 32;
        return s;
    }
    public static NetworkSession Join(string address, int port, ulong mapHash)
    {
        var s = new NetworkSession(false, mapHash);
        var error = s._peer.CreateClient(address, port);
        if (error != Error.Ok) { s.Fail($"접속 실패 ({error})"); return s; }
        s._peer.PeerConnected += s.OnConnected; s._peer.PeerDisconnected += s.OnDisconnected;
        return s;
    }

    public int RoundTripMs
    {
        get
        {
            if (Phase == SessionPhase.Closed) return 0;
            int worst = 0;
            if (IsHost) { foreach (var pair in _peerSlot) worst = Math.Max(worst, Rtt(pair.Key)); }
            else worst = Rtt(1);
            return worst;
        }
    }
    private int Rtt(int peer) { var p = _peer.GetPeer(peer); return p == null ? 0 : (int)p.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime); }

    public void Poll()
    {
        if (Phase == SessionPhase.Closed) return;
        _peer.Poll();
        while (Phase != SessionPhase.Closed && _peer.GetAvailablePacketCount() > 0)
        {
            int from = _peer.GetPacketPeer();
            byte[] bytes = _peer.GetPacket();
            CommandPacket packet;
            try { packet = CommandPacket.Decode(bytes); }
            catch (Exception) { if (IsHost) Kick(from); continue; } // malformed data never reaches the simulation
            if (IsHost) HostReceive(from, packet, bytes); else ClientReceive(packet);
        }
    }

    private void OnConnected(long id)
    {
        if (!IsHost && id == 1 && !_helloSent) { _helloSent = true; Send(1, SessionMessage.Packet(0, SessionMessage.Hello(_content, _mapHash))); }
    }
    private void OnDisconnected(long id)
    {
        if (!IsHost) { Fail(Phase == SessionPhase.InMatch ? "호스트와의 연결이 끊어졌습니다." : "접속이 종료되었습니다."); return; }
        if (!_peerSlot.Remove((int)id, out int slot)) return;
        _slotPeer[slot] = 0;
        if (Phase == SessionPhase.InMatch)
        {
            // Every packet from the leaver was already relayed in order, so the first missing turn is agreed by all.
            int turn = _lastTurn[slot] + 1;
            Broadcast(SessionMessage.Packet(0, SessionMessage.Leave(slot, turn)), 0);
            _leaves.Enqueue((slot, turn));
        }
        else { Slots = Slots.With(slot, false, 0, false); BroadcastLobby(); }
    }

    private void HostReceive(int from, CommandPacket packet, byte[] bytes)
    {
        _peerSlot.TryGetValue(from, out int slot);
        bool known = _peerSlot.ContainsKey(from);
        if (packet.Turn >= 0)
        {
            if (!known || Phase != SessionPhase.InMatch || packet.PlayerId != slot) { Kick(from); return; }
            _lastTurn[slot] = Math.Max(_lastTurn[slot], packet.Turn);
            Broadcast(bytes, from); _turnsIn.Enqueue(packet); return;
        }
        if (SessionMessage.TryReadChat(packet, out string text, out bool team))
        {
            if (!known || packet.PlayerId != slot) return;
            if (!team) Broadcast(bytes, from); // no alliances exist yet, so team chat stays with its author
            if (!team) ChatReceived?.Invoke(slot, text, false);
            return;
        }
        if (packet.Commands.Length != 1 || !SessionMessage.TryFrom(packet.Commands[0], out var m)) return;
        switch (m.Kind)
        {
            case SessionKind.Hello:
                if (known) return;
                if (m.A != SessionMessage.Protocol || unchecked((ulong)m.D) != _content || unchecked((ulong)m.E) != _mapHash) { Reject(from, RejectVersion); return; }
                if (Phase != SessionPhase.Lobby) { Reject(from, RejectStarted); return; }
                int free = -1; for (int s = 1; s < MapCapacity; s++) if (!Slots.Occupied(s)) { free = s; break; }
                if (free < 0) { Reject(from, RejectFull); return; }
                _peerSlot[from] = free; _slotPeer[free] = from;
                Slots = Slots.With(free, true, free % 2, false);
                Send(from, SessionMessage.Packet(0, SessionMessage.Assign(free)));
                BroadcastLobby(); break;
            case SessionKind.SetFaction when known && Phase == SessionPhase.Lobby && (uint)m.A <= 1:
                Slots = Slots.With(slot, true, m.A, false); BroadcastLobby(); break;
            case SessionKind.SetReady when known && Phase == SessionPhase.Lobby:
                Slots = Slots.With(slot, true, Slots.Faction(slot), m.A == 1); BroadcastLobby(); break;
        }
    }

    private void ClientReceive(CommandPacket packet)
    {
        if (packet.Turn >= 0) { if (Phase == SessionPhase.InMatch) _turnsIn.Enqueue(packet); return; }
        if (SessionMessage.TryReadChat(packet, out string text, out bool team)) { ChatReceived?.Invoke(packet.PlayerId, text, team); return; }
        if (packet.Commands.Length != 1 || !SessionMessage.TryFrom(packet.Commands[0], out var m)) return;
        switch (m.Kind)
        {
            case SessionKind.Assign: LocalPlayer = m.A; Phase = SessionPhase.Lobby; LobbyChanged?.Invoke(); break;
            case SessionKind.Lobby: Slots = new LobbySlots(m.A); InputDelay = (int)m.B; Map = m.C; Seed = unchecked((ulong)m.D); LobbyChanged?.Invoke(); break;
            case SessionKind.Start:
                Slots = new LobbySlots(m.A); InputDelay = (int)m.B; Map = m.C; Seed = unchecked((ulong)m.D);
                Phase = SessionPhase.InMatch; Started?.Invoke(); break;
            case SessionKind.Leave: _leaves.Enqueue((m.A, m.C)); break;
            case SessionKind.Reject:
                Fail(m.A switch { RejectVersion => "게임 버전 또는 맵이 호스트와 다릅니다.", RejectFull => "방이 가득 찼습니다.", _ => "이미 경기가 시작되었습니다." }); break;
        }
    }

    public void SetFaction(int faction)
    {
        if (Phase != SessionPhase.Lobby) return;
        if (IsHost) { Slots = Slots.With(0, true, faction, Slots.Ready(0)); BroadcastLobby(); }
        else Send(1, SessionMessage.Packet(LocalPlayer, SessionMessage.SetFaction(faction)));
    }
    public void SetReady(bool ready)
    {
        if (Phase != SessionPhase.Lobby) return;
        if (IsHost) { Slots = Slots.With(0, true, Slots.Faction(0), ready); BroadcastLobby(); }
        else Send(1, SessionMessage.Packet(LocalPlayer, SessionMessage.SetReady(ready)));
    }
    public bool CanStart
    {
        get { if (!IsHost || Phase != SessionPhase.Lobby || Slots.Count < 2) return false; for (int s = 0; s < 4; s++) if (Slots.Occupied(s) && !Slots.Ready(s)) return false; return true; }
    }
    public bool StartMatch()
    {
        if (!CanStart) return false;
        _peer.RefuseNewConnections = true;
        Broadcast(SessionMessage.Packet(0, SessionMessage.Start(Slots, Map, InputDelay, Seed)), 0);
        Phase = SessionPhase.InMatch; Started?.Invoke(); return true;
    }
    public void SendTurn(CommandPacket packet)
    {
        if (Phase != SessionPhase.InMatch) return;
        if (IsHost) Broadcast(packet.Encode(), 0); else Send(1, packet);
    }
    public void SendChat(string text, bool team)
    {
        if (Phase == SessionPhase.Closed || LocalPlayer < 0 || team) return;
        var packet = SessionMessage.ChatPacket(LocalPlayer, text, false);
        if (IsHost) Broadcast(packet.Encode(), 0); else Send(1, packet);
    }
    public int[] Factions() { var f = new int[4]; for (int s = 0; s < 4; s++) f[s] = Slots.Faction(s); return f; }
    public void Close()
    {
        if (Phase == SessionPhase.Closed) return;
        Phase = SessionPhase.Closed; _peer.Close();
    }

    private void BroadcastLobby() { Broadcast(SessionMessage.Packet(0, SessionMessage.Lobby(Slots, Map, InputDelay, Seed)), 0); LobbyChanged?.Invoke(); }
    private void Reject(int peer, int reason) { Send(peer, SessionMessage.Packet(0, SessionMessage.Reject(reason))); _peer.GetPeer(peer)?.PeerDisconnectLater(0); }
    private void Kick(int peer) => _peer.DisconnectPeer(peer);
    private void Fail(string message) { LastError = message; Close(); Failed?.Invoke(message); }
    private void Send(int peer, CommandPacket packet) => Send(peer, packet.Encode());
    private void Send(int peer, byte[] bytes) { _peer.SetTargetPeer(peer); _peer.PutPacket(bytes); }
    private void Broadcast(CommandPacket packet, int except) => Broadcast(packet.Encode(), except);
    private void Broadcast(byte[] bytes, int except) { foreach (var pair in _peerSlot) if (pair.Key != except) Send(pair.Key, bytes); }
}
