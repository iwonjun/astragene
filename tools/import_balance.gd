@tool
extends SceneTree

var failed := false

func _initialize() -> void:
    var output := "// Generated from balance/*.csv by tools/import_balance.gd.\nusing System;\nnamespace RtsGame.Sim.Data;\n\npublic static class DefDatabase\n{\n"
    output += _table("units", "Unit", "id,name,faction,health,armor,damage,range,speedMilli,supply,ore,plasma,trainTicks,vision,attack,defense,airborne,worker,cooldownTicks,windupTicks,trainer,requiredTech")
    output += _table("buildings", "Building", "id,name,faction,health,armor,ore,plasma,buildTicks,supply,width,vision,requiredTech")
    output += _table("upgrades", "Upgrade", "id,name,level,ore,plasma,trainTicks,amount")
    output += "}\n"
    if failed:
        quit(1)
        return
    var file := FileAccess.open("res://src/Sim/Data/DefDatabase.g.cs", FileAccess.WRITE)
    if file == null:
        push_error("Cannot write definitions")
        quit(1)
        return
    file.store_string(output)
    file.flush()
    var error := file.get_error()
    file.close()
    quit(0 if error == OK else 1)

func _table(csv_name: String, type_name: String, schema: String) -> String:
    var file := FileAccess.open("res://balance/%s.csv" % csv_name, FileAccess.READ)
    if file == null:
        _fail("Missing " + csv_name)
        return ""
    var header := file.get_csv_line()
    if ",".join(header).trim_prefix("\ufeff") != schema:
        _fail("Unexpected header: " + csv_name)
        return ""
    var result := "    private static readonly %sDef[] %sData =\n    {\n" % [type_name, type_name]
    var id := 0
    while not file.eof_reached():
        var row := file.get_csv_line()
        if row.size() == 1 and row[0].is_empty():
            continue
        if row.size() != header.size() or not row[0].is_valid_int() or int(row[0]) != id:
            _fail("Invalid column count or ID in " + csv_name)
            return ""
        var args: PackedStringArray = []
        for i in range(row.size()):
            var key: String = header[i].trim_prefix("\ufeff")
            var value: String = row[i]
            match key:
                "name":
                    if value.is_empty() or value.contains('"') or value.contains("\\") or value.contains("\n"):
                        _fail("Invalid definition name")
                        return ""
                    args.append('"%s"' % value)
                "faction", "attack", "defense":
                    var enum_type := "Faction" if key == "faction" else ("AttackType" if key == "attack" else "ArmorType")
                    var allowed := ["Lumina", "Verge"] if key == "faction" else (["Normal", "Piercing", "Explosive"] if key == "attack" else ["Light", "Medium", "Heavy"])
                    if value not in allowed:
                        _fail("Unknown enum value")
                        return ""
                    args.append(enum_type + "." + value)
                "airborne", "worker":
                    if value != "true" and value != "false":
                        _fail("Invalid boolean")
                        return ""
                    args.append(value)
                _:
                    if not value.is_valid_int() or int(value) < (-1 if key == "requiredTech" else 0) or int(value) > 2147483647:
                        _fail("Invalid integer in " + csv_name + ":" + key)
                        return ""
                    args.append(value)
        result += "        new %sDef(%s),\n" % [type_name, ", ".join(args)]
        id += 1
    if id == 0:
        _fail("Empty table " + csv_name)
    result += "    };\n    public static ReadOnlySpan<%sDef> %ss => %sData;\n" % [type_name, type_name, type_name]
    return result

func _fail(message: String) -> void:
    push_error(message)
    failed = true
