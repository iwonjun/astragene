class_name WebHost
extends RefCounted
## Tiny static HTTP server so friends can open http://HOST:27502 and play the web build in a
## browser without installing anything. Serves files from one folder only (GET, no directory
## listing, no path traversal). Plain http on purpose: browsers block ws:// from https pages.

const PORT_OFFSET := 2
const TYPES := {"html": "text/html; charset=utf-8", "js": "application/javascript", "wasm": "application/wasm",
	"pck": "application/octet-stream", "png": "image/png", "svg": "image/svg+xml", "ico": "image/x-icon", "json": "application/json"}

var root := ""
var tcp := TCPServer.new()
var clients: Array = []   # {peer, buf, started}


## Web build folder: next to the exported executable, or builds/web when running from source.
static func default_root() -> String:
	var candidates := [OS.get_executable_path().get_base_dir().path_join("web"), ProjectSettings.globalize_path("res://builds/web")]
	for c in candidates:
		if FileAccess.file_exists(c.path_join("index.html")):
			return c
	return ""


func start(port: int) -> bool:
	root = default_root()
	if root == "":
		return false
	return tcp.listen(port) == OK


func poll() -> void:
	if not tcp.is_listening():
		return
	while tcp.is_connection_available():
		clients.append({"peer": tcp.take_connection(), "buf": "", "started": Time.get_ticks_msec()})
	var keep := []
	for c in clients:
		var peer: StreamPeerTCP = c["peer"]
		peer.poll()
		if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED or Time.get_ticks_msec() - int(c["started"]) > 10000:
			continue
		var n := peer.get_available_bytes()
		if n > 0:
			c["buf"] += peer.get_utf8_string(n)
		var buf: String = c["buf"]
		if buf.find("\r\n\r\n") < 0 and buf.length() < 8192:
			keep.append(c)
			continue
		_respond(peer, buf.get_slice("\r\n", 0))
		peer.disconnect_from_host()
	clients = keep


func _respond(peer: StreamPeerTCP, request_line: String) -> void:
	var parts := request_line.split(" ")
	if parts.size() < 2 or parts[0] != "GET":
		_send(peer, 405, "text/plain", "Method Not Allowed".to_utf8_buffer())
		return
	var path := parts[1].get_slice("?", 0).uri_decode()
	if path == "/" or path == "":
		path = "/index.html"
	var name := path.trim_prefix("/")
	if name.contains("..") or name.contains("\\") or name.contains(":") or name.contains("/"):
		_send(peer, 404, "text/plain", "Not Found".to_utf8_buffer())
		return
	var file := root.path_join(name)
	if not FileAccess.file_exists(file):
		_send(peer, 404, "text/plain", "Not Found".to_utf8_buffer())
		return
	_send(peer, 200, TYPES.get(name.get_extension().to_lower(), "application/octet-stream"), FileAccess.get_file_as_bytes(file))


func _send(peer: StreamPeerTCP, code: int, type: String, body: PackedByteArray) -> void:
	var status: String = {200: "OK", 404: "Not Found", 405: "Method Not Allowed"}.get(code, "OK")
	var head := "HTTP/1.1 %d %s\r\nContent-Type: %s\r\nContent-Length: %d\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" % [code, status, type, body.size()]
	peer.set_no_delay(true)
	peer.put_data(head.to_utf8_buffer())
	var sent := 0
	while sent < body.size():
		var chunk := body.slice(sent, mini(sent + 65536, body.size()))
		var err := peer.put_data(chunk)
		if err != OK:
			return
		sent += chunk.size()
