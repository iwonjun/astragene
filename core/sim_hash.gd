class_name SimHash
extends RefCounted
## FNV-1a (32-bit) over integers, little-endian bytes. Used for desync detection.

const OFFSET := 0x811C9DC5
const PRIME := 16777619
var value: int = OFFSET


func add(v: int) -> void:
	var x := v & 0xFFFFFFFF
	for i in 4:
		value = ((value ^ (x & 0xFF)) * PRIME) & 0xFFFFFFFF
		x >>= 8


func add64(v: int) -> void:
	add(v & 0xFFFFFFFF)
	add((v >> 32) & 0xFFFFFFFF)


func add_bytes(bytes: PackedByteArray) -> void:
	for b in bytes:
		value = ((value ^ b) * PRIME) & 0xFFFFFFFF
