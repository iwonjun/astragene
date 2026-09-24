class_name SimRng
extends RefCounted
## Deterministic 32-bit xorshift generator. The only randomness the simulation may use (AGENTS.md R1).
## All arithmetic is masked to 32 bits so every platform (desktop, web/wasm) produces the same stream.

const MASK := 0xFFFFFFFF
var state: int = 1


func _init(seed_value: int = 1) -> void:
	state = mix(seed_value)
	if state == 0:
		state = 0x6D2B79F5


## Integer avalanche mix (lowbias32). Pure function, also used for hashing.
static func mix(value: int) -> int:
	var x := value & MASK
	x ^= x >> 16
	x = (x * 0x7FEB352D) & MASK
	x ^= x >> 15
	x = (x * 0x846CA68B) & MASK
	x ^= x >> 16
	return x


func next_u32() -> int:
	var x := state
	x ^= (x << 13) & MASK
	x ^= x >> 17
	x ^= (x << 5) & MASK
	state = x & MASK
	return state


## Uniform-ish integer in [0, n). n must be positive.
func below(n: int) -> int:
	return next_u32() % n


## True with probability permille / 1000.
func chance(permille: int) -> bool:
	return below(1000) < permille
