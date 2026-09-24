class_name Protocol
extends RefCounted
## Wire format: one Dictionary per packet, var_to_bytes without objects. Only intents, chat and
## control messages travel; tile ownership never does (AGENTS.md R3).
##
## client -> server: hello{name, digest, token}, intent{i}, chat{ch, to, text, ping}, hash{t, h}, setup{...host only}, start{}
## server -> client: welcome{key, host}, lobby{players, settings}, start{config, pid, token}, turn{t, i},
##                   snapshot{t, data}, chat{from, name, ch, to, text, ping}, notice{text}, reject{reason}

const VERSION := 1
const DEFAULT_PORT := 27500   # ENet (UDP). WebSocket listens on DEFAULT_PORT + 1.
const INTENT_TYPES := ["spawn", "attack", "retreat", "boat", "build", "perk", "strike", "propose", "respond", "break", "donate"]


static func encode(msg: Dictionary) -> PackedByteArray:
	return var_to_bytes(msg)


static func decode(bytes: PackedByteArray) -> Dictionary:
	if bytes.size() == 0 or bytes.size() > 4 * 1024 * 1024:
		return {}
	var v: Variant = bytes_to_var(bytes)  # objects are refused by default
	return v if typeof(v) == TYPE_DICTIONARY else {}


## Keeps only known intent types with integer/bool fields; the server stamps the sender's pid.
static func clean_intent(raw: Variant, pid: int) -> Dictionary:
	if typeof(raw) != TYPE_DICTIONARY or not INTENT_TYPES.has(str(raw.get("type", ""))):
		return {}
	var out := {"type": str(raw["type"]), "p": pid}
	for key in ["target", "ratio", "tile", "from", "kind", "choice", "to", "treaty", "id", "troops", "gold"]:
		if raw.has(key) and typeof(raw[key]) in [TYPE_INT, TYPE_FLOAT]:
			out[key] = int(raw[key])
	if raw.has("accept"):
		out["accept"] = bool(raw["accept"])
	return out
