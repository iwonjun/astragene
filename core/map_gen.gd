class_name MapGen
extends RefCounted
## Procedural continents from integer value noise. Every peer regenerates the identical map from the
## shared seed, so the map never crosses the network (AGENTS.md R1/R3).

const OCEAN := 0
const PLAINS := 1
const FOREST := 2
const HILLS := 3
const MOUNTAIN := 4
const ONE := 1024
const OCTAVES := [[64, 8], [32, 4], [16, 2], [8, 1]]


static func generate(seed_value: int, width: int, height: int, land_permille: int) -> PackedByteArray:
	var elevation := _fbm(seed_value, width, height, OCTAVES)
	var edge := 24
	for y in height:
		for x in width:
			var e: int = mini(mini(x, width - 1 - x), mini(y, height - 1 - y))
			if e < edge:
				var i := y * width + x
				elevation[i] = maxi(0, elevation[i] - (edge - e) * 26)
	var threshold := _percentile(elevation, 1000 - land_permille)
	var moisture := _fbm(seed_value ^ 0x5BD1E995, width, height, [[24, 2], [12, 1]])
	var terrain := PackedByteArray()
	terrain.resize(width * height)
	var span: int = maxi(1, 1023 - threshold)
	for i in width * height:
		var v := elevation[i]
		if v < threshold:
			terrain[i] = OCEAN
			continue
		var rel: int = (v - threshold) * 1000 / span
		if rel > 620:
			terrain[i] = MOUNTAIN
		elif rel > 430:
			terrain[i] = HILLS
		elif moisture[i] > 560:
			terrain[i] = FOREST
		else:
			terrain[i] = PLAINS
	return terrain


## Fractal value noise in [0, 1023]. Lattices are precomputed per octave, then bilinearly
## interpolated with an integer smoothstep; no floating point anywhere.
static func _fbm(seed_value: int, width: int, height: int, octaves: Array) -> PackedInt32Array:
	var out := PackedInt32Array()
	out.resize(width * height)
	var total_weight := 0
	for o in octaves:
		total_weight += int(o[1])
	var octave_index := 0
	for o in octaves:
		var cell: int = o[0]
		var weight: int = o[1]
		var gw: int = width / cell + 2
		var gh: int = height / cell + 2
		var lattice := PackedInt32Array()
		lattice.resize(gw * gh)
		for gy in gh:
			for gx in gw:
				lattice[gy * gw + gx] = SimRng.mix(gx * 73856093 ^ gy * 19349663 ^ seed_value ^ (octave_index * 83492791)) & 1023
		var smooth := PackedInt32Array()
		smooth.resize(cell)
		for f in cell:
			var t: int = f * ONE / cell
			smooth[f] = t * t * (3 * ONE - 2 * t) / (ONE * ONE)
		for y in height:
			var gy: int = y / cell
			var sy: int = smooth[y % cell]
			var row0: int = gy * gw
			var row1: int = row0 + gw
			for x in width:
				var gx: int = x / cell
				var sx: int = smooth[x % cell]
				var a: int = lattice[row0 + gx]
				var b: int = lattice[row0 + gx + 1]
				var c: int = lattice[row1 + gx]
				var d: int = lattice[row1 + gx + 1]
				var top: int = a + (b - a) * sx / ONE
				var bottom: int = c + (d - c) * sx / ONE
				out[y * width + x] += (top + (bottom - top) * sy / ONE) * weight
		octave_index += 1
	for i in out.size():
		out[i] = out[i] / total_weight
	return out


static func _percentile(values: PackedInt32Array, permille: int) -> int:
	var histogram := PackedInt32Array()
	histogram.resize(1024)
	for v in values:
		histogram[clampi(v, 0, 1023)] += 1
	var target: int = values.size() * permille / 1000
	var seen := 0
	for level in 1024:
		seen += histogram[level]
		if seen >= target:
			return level
	return 1023
