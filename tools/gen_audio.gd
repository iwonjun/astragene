@tool
extends SceneTree

# Procedural placeholder audio (100% original, no external samples) and the project audio bus layout.
# godot --headless --script res://tools/gen_audio.gd
const RATE := 22050
const DIR := "res://game/assets/generated/audio"
var rng := RandomNumberGenerator.new()

func save_wave(name: String, samples: PackedFloat32Array, loop := false) -> void:
	var wave := AudioStreamWAV.new()
	wave.format = AudioStreamWAV.FORMAT_16_BITS
	wave.mix_rate = RATE
	var bytes := PackedByteArray()
	bytes.resize(samples.size() * 2)
	for i in samples.size():
		bytes.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 30000.0))
	wave.data = bytes
	if loop:
		wave.loop_mode = AudioStreamWAV.LOOP_FORWARD
		wave.loop_begin = 0
		wave.loop_end = samples.size()
	var error := ResourceSaver.save(wave, "%s/%s.tres" % [DIR, name])
	if error != OK:
		push_error("Cannot save %s: %s" % [name, error_string(error)])
		quit(1)

func env(i: int, n: int, attack := 0.01, release := 0.6) -> float:
	var t := float(i) / n
	var a := minf(1.0, t / maxf(attack, 0.0001))
	var r := clampf((1.0 - t) / release, 0.0, 1.0)
	return a * r

# Voice placeholder: two formant-ish chirps. LUMINA is bright and even, VERGE low and warbling.
func voice(base: float, steps: Array, warble: float, length: float) -> PackedFloat32Array:
	var n := int(RATE * length)
	var out := PackedFloat32Array(); out.resize(n)
	var phase := 0.0
	for i in n:
		var t := float(i) / RATE
		var segment: int = mini(int(float(i) / n * steps.size()), steps.size() - 1)
		var f: float = base * float(steps[segment]) * (1.0 + warble * sin(TAU * 7.0 * t))
		phase += TAU * f / RATE
		var tone := sin(phase) * 0.55 + sin(phase * 2.0) * 0.25 + sin(phase * 3.01) * 0.12
		out[i] = tone * env(i, n, 0.05, 0.5) * 0.7
	return out

func noise_burst(length: float, lowpass: float, pitch_drop: float, gain: float) -> PackedFloat32Array:
	var n := int(RATE * length)
	var out := PackedFloat32Array(); out.resize(n)
	var last := 0.0
	var phase := 0.0
	for i in n:
		var t := float(i) / n
		last = lerpf(last, rng.randf_range(-1.0, 1.0), lowpass)
		phase += TAU * lerpf(pitch_drop, pitch_drop * 0.35, t) / RATE
		out[i] = (last * 0.8 + sin(phase) * 0.5) * env(i, n, 0.005, 0.95) * gain
	return out

func arpeggio(notes: Array, note_length: float) -> PackedFloat32Array:
	var per := int(RATE * note_length)
	var out := PackedFloat32Array(); out.resize(per * notes.size())
	for k in notes.size():
		for i in per:
			var t := float(i) / RATE
			out[k * per + i] = (sin(TAU * float(notes[k]) * t) * 0.5 + sin(TAU * float(notes[k]) * 2.0 * t) * 0.15) * env(i, per, 0.02, 0.8) * 0.6
	return out

# Ambient BGM slot: 16 s seamless pad loop (A minor, slow chord changes) used until real music exists.
func pad() -> PackedFloat32Array:
	var seconds := 16.0
	var n := int(RATE * seconds)
	var out := PackedFloat32Array(); out.resize(n)
	var chords := [[220.0, 261.63, 329.63], [174.61, 220.0, 261.63], [196.0, 246.94, 293.66], [164.81, 207.65, 246.94]]
	for i in n:
		var t := float(i) / RATE
		var c: int = int(t / 4.0) % 4
		var local := fmod(t, 4.0) / 4.0
		var fade := sin(PI * local)
		var v := 0.0
		for f: float in chords[c]:
			v += sin(TAU * f * t) * 0.2 + sin(TAU * f * 0.5 * t + 0.3) * 0.08
		v += sin(TAU * 0.25 * t) * sin(TAU * 440.0 * t) * 0.02
		out[i] = v * (0.35 + 0.65 * fade) * 0.45
	return out

func _initialize() -> void:
	rng.seed = 20260924
	DirAccess.make_dir_recursive_absolute(DIR)
	save_wave("voice_select_lumina", voice(520.0, [1.0, 1.26], 0.0, 0.22))
	save_wave("voice_ack_lumina", voice(620.0, [1.0, 1.5, 1.2], 0.0, 0.26))
	save_wave("voice_select_verge", voice(180.0, [1.0, 0.84], 0.08, 0.28))
	save_wave("voice_ack_verge", voice(200.0, [1.0, 1.19, 0.9], 0.1, 0.3))
	save_wave("sfx_shot", noise_burst(0.12, 0.55, 900.0, 0.5))
	save_wave("sfx_melee", noise_burst(0.16, 0.25, 320.0, 0.45))
	save_wave("sfx_hit", noise_burst(0.07, 0.8, 1400.0, 0.3))
	save_wave("sfx_explosion", noise_burst(0.7, 0.08, 90.0, 0.9))
	save_wave("sfx_death", noise_burst(0.35, 0.15, 160.0, 0.6))
	save_wave("sfx_produced", arpeggio([523.25, 659.25, 783.99], 0.08))
	save_wave("sfx_build", arpeggio([392.0, 523.25], 0.1))
	save_wave("sfx_victory", arpeggio([523.25, 659.25, 783.99, 1046.5], 0.18))
	save_wave("sfx_defeat", arpeggio([392.0, 329.63, 261.63, 196.0], 0.22))
	save_wave("bgm_ambient", pad(), true)
	# Bus layout: Master <- Music, SFX, Voice, UI. Saved where Godot loads it by default.
	while AudioServer.bus_count > 1:
		AudioServer.remove_bus(1)
	for bus_name in ["Music", "SFX", "Voice", "UI"]:
		AudioServer.add_bus()
		var index := AudioServer.bus_count - 1
		AudioServer.set_bus_name(index, bus_name)
		AudioServer.set_bus_send(index, "Master")
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index("Music"), -8.0)
	var error := ResourceSaver.save(AudioServer.generate_bus_layout(), "res://default_bus_layout.tres")
	if error != OK:
		push_error("Cannot save bus layout: %s" % error_string(error))
		quit(1)
		return
	print("Generated placeholder voices, SFX, BGM loop and audio bus layout.")
	quit()
