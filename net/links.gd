class_name NetLinks
extends RefCounted
## Transports for the client side: a real MultiplayerPeer (ENet or WebSocket), or an in-process
## loopback to a server running in the same program (the host's own seat).


class Link extends RefCounted:
	func send(_msg: Dictionary) -> void: pass
	func poll() -> Array: return []
	func connected() -> bool: return true
	func closed() -> bool: return false
	func close() -> void: pass


class PeerLink extends Link:
	var peer: MultiplayerPeer
	var _open := false
	var _gone := false

	func _init(p: MultiplayerPeer) -> void:
		peer = p
		peer.transfer_mode = MultiplayerPeer.TRANSFER_MODE_RELIABLE
		peer.peer_connected.connect(func(_id): _open = true)
		peer.peer_disconnected.connect(func(_id): _gone = true)

	func send(msg: Dictionary) -> void:
		if not _open:
			return
		peer.set_target_peer(1)
		peer.put_packet(Protocol.encode(msg))

	func poll() -> Array:
		var out := []
		if _gone or peer.get_connection_status() == MultiplayerPeer.CONNECTION_DISCONNECTED:
			if _open:
				_gone = true
			return out
		peer.poll()
		if peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED:
			_open = true
		elif _open and peer.get_connection_status() == MultiplayerPeer.CONNECTION_DISCONNECTED:
			_gone = true
		while peer.get_available_packet_count() > 0:
			var msg := Protocol.decode(peer.get_packet())
			if not msg.is_empty():
				out.append(msg)
		return out

	func connected() -> bool: return _open and not _gone
	func closed() -> bool: return _gone
	func close() -> void:
		_gone = true
		peer.close()


class LocalLink extends Link:
	var server: GameServer
	var key: String
	var inbox: Array = []

	func send(msg: Dictionary) -> void:
		if server != null and is_instance_valid(server):
			server.receive(key, msg)

	func poll() -> Array:
		var out := inbox
		inbox = []
		return out


static func connect_enet(address: String, port: int) -> Link:
	var peer := ENetMultiplayerPeer.new()
	if peer.create_client(address, port) != OK:
		return null
	return PeerLink.new(peer)


static func connect_websocket(address: String, port: int) -> Link:
	var peer := WebSocketMultiplayerPeer.new()
	if peer.create_client("ws://%s:%d" % [address, port]) != OK:
		return null
	return PeerLink.new(peer)
