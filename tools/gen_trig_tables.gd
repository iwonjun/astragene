@tool
extends SceneTree

func _initialize() -> void:
    var text := "// Generated offline by tools/gen_trig_tables.gd. Do not edit.\nnamespace RtsGame.Sim.Core;\n\ninternal static class TrigTables\n{\n"
    for kind: String in ["Sin", "Atan"]:
        text += "    internal static readonly long[] %s = new long[]\n    {\n" % kind
        for i in range(1024):
            var value: float = sin(TAU * i / 1024.0) if kind == "Sin" else atan(i / 1023.0)
            text += "        %dL,%s" % [roundi(value * 4294967296.0), "\n"]
        text += "    };\n"
    text += "}\n"
    var file := FileAccess.open("res://src/Sim/Core/TrigTables.g.cs", FileAccess.WRITE)
    if file == null:
        push_error("Cannot write trig tables")
        quit(1)
        return
    file.store_string(text)
    file.flush()
    var error := file.get_error()
    file.close()
    quit(0 if error == OK else 1)
