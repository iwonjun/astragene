@tool
extends SceneTree

# Network impairment proxy for Phase 11 validation. Clients connect to --listen, datagrams are
# forwarded to --target after --delay-ms (each direction) and --loss percent are dropped. ENet's
# reliable channel must retransmit the dropped datagrams; the game never sees the loss directly.
# godot --headless --script res://tools/udp_lossy_proxy.gd -- --listen=27416 --target=127.0.0.1:27415 --delay-ms=200 --loss=2

var server := UDPServer.new()
var links: Array = []
var rng := RandomNumberGenerator.new()
var delay_ms := 200
var loss := 2.0
var target_host := "127.0.0.1"
var target_port := 27415
var forwarded := 0
var dropped := 0
var last_report := 0

func arg(name: String, fallback: String) -> String:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--" + name + "="):
			return a.substr(name.length() + 3)
	return fallback

func _initialize() -> void:
	Engine.max_fps = 1000
	rng.seed = int(arg("seed", "20260923"))
	delay_ms = int(arg("delay-ms", "200"))
	loss = float(arg("loss", "2"))
	var target := arg("target", "127.0.0.1:27415").split(":")
	target_host = target[0]
	target_port = int(target[1])
	var listen := int(arg("listen", "27416"))
	var error := server.listen(listen, "127.0.0.1")
	if error != OK:
		push_error("Proxy cannot listen on %d: %s" % [listen, error_string(error)])
		quit(1)
		return
	print("PROXY listening 127.0.0.1:%d -> %s:%d delay=%dms loss=%.1f%%" % [listen, target_host, target_port, delay_ms, loss])

func queue(link: Dictionary, key: String, bytes: PackedByteArray) -> void:
	if rng.randf() * 100.0 < loss:
		dropped += 1
		return
	link[key].append([Time.get_ticks_msec() + delay_ms, bytes])

func _process(_delta: float) -> bool:
	server.poll()
	while server.is_connection_available():
		var client: PacketPeerUDP = server.take_connection()
		var upstream := PacketPeerUDP.new()
		upstream.connect_to_host(target_host, target_port)
		links.append({"client": client, "upstream": upstream, "to_host": [], "to_client": []})
		print("PROXY new client %s:%d" % [client.get_packet_ip(), client.get_packet_port()])
	var now := Time.get_ticks_msec()
	for link: Dictionary in links:
		var client: PacketPeerUDP = link.client
		var upstream: PacketPeerUDP = link.upstream
		while client.get_available_packet_count() > 0:
			queue(link, "to_host", client.get_packet())
		while upstream.get_available_packet_count() > 0:
			queue(link, "to_client", upstream.get_packet())
		while link.to_host.size() > 0 and link.to_host[0][0] <= now:
			upstream.put_packet(link.to_host.pop_front()[1])
			forwarded += 1
		while link.to_client.size() > 0 and link.to_client[0][0] <= now:
			client.put_packet(link.to_client.pop_front()[1])
			forwarded += 1
	if now - last_report > 30000:
		last_report = now
		print("PROXY forwarded=%d dropped=%d (%.2f%%)" % [forwarded, dropped, 100.0 * dropped / max(1, forwarded + dropped)])
	return false
