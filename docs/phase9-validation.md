# Phase 9 rendering validation

Measured on 2026-09-23 with Godot 4.6.3 .NET, Forward+ / D3D12, NVIDIA RTX 4060 Ti, 1600×900, VSync off. The fixture uses two opposing 200-unit armies, all ten normal unit definitions, normal combat and player-0 visibility. No invulnerability, extra vision or health overrides. Assets warm for at least 120 frames and one second before simulation begins.

- 400 simultaneously visible living units: 421 measured frames, mean 0.9503 ms/frame.
- Entire six-second battle: 8,913 samples, mean 0.6731 ms, p95 0.838 ms.
- Maximum draw calls: 31 (Godot RenderingServer frame counter).
- Casualties reduce the population later in the run; the 400-unit sample is reported separately. These are short local measurements, not a minimum-spec or sustained-load guarantee. Phase 13 retains the 500-entity sustained profiling requirement.
- Simulation tests: 42 passed. GdUnit integration: 3 passed. No shader/runtime errors in GPU capture logs.

Reproduce from the repository root after `tools/use_local_tools.ps1`:

```powershell
dotnet build RtsGame.sln
godot --headless --script res://tools/gen_meshes.gd
godot --headless --script res://tools/build_scenes.gd
godot res://game/scenes/match.tscn -- --render-benchmark
godot res://game/scenes/match.tscn -- --art-capture
```

`render-benchmark.json` stores raw summary values. `phase9-1.png` is the actual battle; `phase9-2.png` is a paused role/faction gallery; `phase9-3.png` is the starting base. Gallery capture does not contribute to performance results.

All meshes/materials are locally generated, original placeholders. Production dissolve is integrated in `toon.gdshader`; fog is integrated in `terrain.gdshader`. The shared pooled effect shader draws impact rings, melee arcs, and layered explosion cores. The brief hit stop affects position interpolation only. Units are instanced, including health bars and selection rings. Local short-distance navigation now bypasses a large field build only if the whole bounding rectangle is walkable; obstacle regression tests cover the fallback.

Godot 4.6 API references checked: [MultiMesh](https://docs.godotengine.org/en/4.6/classes/class_multimesh.html), [SurfaceTool](https://docs.godotengine.org/en/4.6/classes/class_surfacetool.html), [ArrayMesh](https://docs.godotengine.org/en/4.6/classes/class_arraymesh.html), [ResourceSaver](https://docs.godotengine.org/en/4.6/classes/class_resourcesaver.html), [Environment](https://docs.godotengine.org/en/4.6/classes/class_environment.html). Quads are converted to ArrayMesh before saving `.mesh`; all saves are checked.
