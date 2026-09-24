class_name MapView
extends Node2D
## Draws the simulation: one shader-driven sprite for terrain + territory (owner ids in an R8 texture
## updated only where tiles changed) and a vector overlay for names, buildings, boats, strikes, pings.

const TILE := 6.0
const BUILDING_GLYPH := ["도", "요", "시", "연", "항", "사"]

var sim: Sim
var my_pid := 0
var focus_pid := 0
var sprite := Sprite2D.new()
var overlay := MapOverlay.new()
var owner_image: Image
var owner_tex: ImageTexture
var palette_image: Image
var palette_tex: ImageTexture
var material_ref: ShaderMaterial
var pings: Array = []      # {tile, until, color}
var flashes: Array = []    # {tile, until, radius}
var font: Font
var _palette_timer := 0.0


func _ready() -> void:
	font = load("res://assets/fonts/Pretendard-Regular.otf")
	add_child(sprite)
	add_child(overlay)
	overlay.view = self
	overlay.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR  # smooth text over the pixel-art map


func setup(new_sim: Sim, pid: int) -> void:
	sim = new_sim
	my_pid = pid
	var w := sim.width
	var h := sim.height
	var terrain := Image.create(w, h, false, Image.FORMAT_RGB8)
	for y in h:
		for x in w:
			var i := y * w + x
			var c := Palette.terrain_color(sim.rules, sim.terrain[i])
			# Deterministic grain so large plains are not a flat colour.
			var n := float(SimRng.mix(i * 2654435761) & 255) / 255.0
			c = c.lightened(0.05 * n) if sim.terrain[i] != MapGen.OCEAN else c.darkened(0.08 * n)
			terrain.set_pixel(x, y, c)
	owner_image = Image.create(w, h, false, Image.FORMAT_R8)
	for i in sim.owner.size():
		var o: int = sim.owner[i]
		if o != 0:
			owner_image.set_pixel(i % w, i / w, Color(o / 255.0, 0, 0))
	owner_tex = ImageTexture.create_from_image(owner_image)
	palette_image = Image.create(256, 1, false, Image.FORMAT_RGBA8)
	palette_tex = ImageTexture.create_from_image(palette_image)
	_refresh_palette()
	material_ref = ShaderMaterial.new()
	material_ref.shader = load("res://presentation/territory.gdshader")
	material_ref.set_shader_parameter("terrain_tex", ImageTexture.create_from_image(terrain))
	material_ref.set_shader_parameter("owner_tex", owner_tex)
	material_ref.set_shader_parameter("palette_tex", palette_tex)
	material_ref.set_shader_parameter("map_size", Vector2(w, h))
	material_ref.set_shader_parameter("me", float(pid))
	sprite.texture = ImageTexture.create_from_image(terrain)
	sprite.centered = false
	sprite.scale = Vector2(TILE, TILE)
	sprite.material = material_ref
	sim.take_dirty()


## Rebinds after a snapshot replaced the simulation object.
func rebind(new_sim: Sim) -> void:
	sim = new_sim
	for i in sim.owner.size():
		owner_image.set_pixel(i % sim.width, i / sim.width, Color(sim.owner[i] / 255.0, 0, 0))
	owner_tex.update(owner_image)
	sim.take_dirty()


func _refresh_palette() -> void:
	for pid in mini(sim.players.size(), 256):
		var p: SimPlayer = sim.players[pid]
		var c := Palette.color(pid, p.kind)
		c.a = 0.9 if p.is_traitor(sim.tick) else 1.0
		palette_image.set_pixel(pid, 0, c)
	palette_tex.update(palette_image)


func _process(delta: float) -> void:
	if sim == null:
		return
	var dirty := sim.take_dirty()
	if dirty.size() > 0:
		var w := sim.width
		for t in dirty:
			owner_image.set_pixel(t % w, t / w, Color(sim.owner[t] / 255.0, 0, 0))
		owner_tex.update(owner_image)
	_palette_timer -= delta
	if _palette_timer <= 0:
		_palette_timer = 0.5
		_refresh_palette()
	var pulse := 0.5 + 0.5 * sin(Time.get_ticks_msec() * 0.006)
	material_ref.set_shader_parameter("pulse", pulse)
	material_ref.set_shader_parameter("focus", float(focus_pid))
	var now := Time.get_ticks_msec()
	pings = pings.filter(func(p): return p["until"] > now)
	flashes = flashes.filter(func(f): return f["until"] > now)
	overlay.queue_redraw()


func tile_at(world: Vector2) -> int:
	var x := int(floor(world.x / TILE))
	var y := int(floor(world.y / TILE))
	if sim == null or x < 0 or y < 0 or x >= sim.width or y >= sim.height:
		return -1
	return y * sim.width + x


func tile_center(t: int) -> Vector2:
	return Vector2((t % sim.width + 0.5) * TILE, (t / sim.width + 0.5) * TILE)


func centroid(p: SimPlayer) -> Vector2:
	if p.tiles <= 0:
		return Vector2.ZERO
	return Vector2((float(p.sum_x) / p.tiles + 0.5) * TILE, (float(p.sum_y) / p.tiles + 0.5) * TILE)


func ping(t: int, color: Color = Color(1, 0.9, 0.3)) -> void:
	pings.append({"tile": t, "until": Time.get_ticks_msec() + 6000, "color": color})


func flash(t: int, radius: int) -> void:
	flashes.append({"tile": t, "until": Time.get_ticks_msec() + 900, "radius": radius})


class MapOverlay extends Node2D:
	var view: MapView

	func _draw() -> void:
		var sim := view.sim
		if sim == null:
			return
		var zoom := get_viewport().get_canvas_transform().get_scale().x
		# Buildings (only readable when zoomed in a little).
		if zoom >= 0.7:
			for b in sim.buildings:
				if not b["alive"]:
					continue
				var t: int = b["tile"]
				var o: int = sim.owner[t]
				var c := view.tile_center(t)
				var col := Palette.color(o, sim.players[o].kind) if o > 0 else Color.GRAY
				var hacked: bool = b["disabled_until"] > sim.tick
				draw_circle(c, MapView.TILE * 0.95, Color(0.05, 0.07, 0.1, 0.85))
				draw_arc(c, MapView.TILE * 0.95, 0, TAU, 16, Color(0.5, 0.5, 0.5) if hacked else col, 1.2)
				draw_string(view.font, c + Vector2(-4.2, 3.6), MapView.BUILDING_GLYPH[b["type"]], HORIZONTAL_ALIGNMENT_LEFT, -1, 9, Color.WHITE if not hacked else Color(0.6, 0.6, 0.6))
		# Capitals.
		for p in sim.players:
			if p.pid != 0 and p.alive and p.capital >= 0:
				var c := view.tile_center(p.capital)
				var r := 5.0
				draw_colored_polygon(PackedVector2Array([c + Vector2(0, -r), c + Vector2(r, 0), c + Vector2(0, r), c + Vector2(-r, 0)]), Color(1, 0.9, 0.45))
				draw_polyline(PackedVector2Array([c + Vector2(0, -r), c + Vector2(r, 0), c + Vector2(0, r), c + Vector2(-r, 0), c + Vector2(0, -r)]), Color(0.1, 0.08, 0.02), 1.0)
		# Boats and strikes in flight.
		for b in sim.boats:
			var path: PackedInt32Array = b["path"]
			var i: int = mini(int(b["pos"]), path.size() - 1)
			if i >= 0:
				var c := view.tile_center(path[i])
				draw_circle(c, 3.5, Palette.color(b["pid"], sim.players[b["pid"]].kind))
				draw_circle(c, 1.5, Color.WHITE)
		for s in sim.strikes:
			var target := view.tile_center(s["tile"])
			draw_arc(target, 10 + 3 * sin(Time.get_ticks_msec() * 0.02), 0, TAU, 24, Color(1, 0.3, 0.2, 0.9), 2)
		for f in view.flashes:
			var c := view.tile_center(f["tile"])
			var life: float = (f["until"] - Time.get_ticks_msec()) / 900.0
			draw_circle(c, f["radius"] * MapView.TILE * (1.2 - life * 0.2), Color(1, 0.85, 0.5, 0.45 * life))
		for p in view.pings:
			var c := view.tile_center(p["tile"])
			var r := 8 + 6 * sin(Time.get_ticks_msec() * 0.01)
			draw_arc(c, r, 0, TAU, 24, p["color"], 2.5)
			draw_arc(c, r * 1.8, 0, TAU, 24, Color(p["color"], 0.4), 1.5)
		# Names at territory centres, sized by area.
		var inv := 1.0 / maxf(zoom, 0.05)
		for p in sim.players:
			if p.pid == 0 or not p.alive or p.tiles < 25:
				continue
			var size := clampf(sqrt(float(p.tiles)) * 0.9, 9.0, 40.0)
			var screen_size := size * zoom
			if screen_size < 8:
				continue
			var pos := view.centroid(p)
			var text: String = p.name
			var fs := int(size)
			var width := view.font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
			var shade := Color(0, 0, 0, 0.75)
			draw_string_outline(view.font, pos - Vector2(width / 2, 0), text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, maxi(2, int(3 * inv)), shade)
			draw_string(view.font, pos - Vector2(width / 2, 0), text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, Color.WHITE if p.pid != view.my_pid else Color(1, 0.95, 0.6))
			var sub := "%s · %s" % [Fmt.num(p.pop), sim.rules.eras[p.era]["name_ko"]]
			var sw := view.font.get_string_size(sub, HORIZONTAL_ALIGNMENT_LEFT, -1, int(fs * 0.6)).x
			draw_string_outline(view.font, pos + Vector2(-sw / 2, fs * 0.75), sub, HORIZONTAL_ALIGNMENT_LEFT, -1, int(fs * 0.6), maxi(2, int(3 * inv)), shade)
			draw_string(view.font, pos + Vector2(-sw / 2, fs * 0.75), sub, HORIZONTAL_ALIGNMENT_LEFT, -1, int(fs * 0.6), Color(0.85, 0.9, 1))
