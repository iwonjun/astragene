@tool
extends SceneTree

const SIDE := 128

func _initialize() -> void:
    var file := FileAccess.open("res://game/maps/duel.map", FileAccess.WRITE)
    if file == null:
        push_error("Cannot write map")
        quit(1)
        return
    file.big_endian = false
    file.store_32(0x504d4741)
    file.store_32(1)
    file.store_16(SIDE)
    file.store_16(SIDE)
    var nodes: Dictionary = {}
    var deposits := [Vector2i(12, 14), Vector2i(12, 18), Vector2i(12, 22), Vector2i(18, 12), Vector2i(22, 12), Vector2i(28, 16), Vector2i(34, 40), Vector2i(38, 40), Vector2i(42, 40), Vector2i(40, 34)]
    for i in range(deposits.size()):
        var point: Vector2i = deposits[i]
        nodes[point] = i
        nodes[Vector2i(127, 127) - point] = deposits.size() + i
    for y in range(SIDE):
        for x in range(SIDE):
            var walkable := x > 1 and y > 1 and x < 126 and y < 126
            var height := 0
            var sx := x
            var sy := y
            if x + y > 127:
                sx = 127 - x
                sy = 127 - y
            if sx >= 6 and sx <= 30 and sy >= 6 and sy <= 30:
                height = 2
            elif sx >= 5 and sx <= 47 and sy >= 5 and sy <= 47:
                height = 1
            # Cliff boundaries are blocked except a broad southeast ramp.
            if ((sx == 6 or sx == 30) and sy >= 6 and sy <= 30) or ((sy == 6 or sy == 30) and sx >= 6 and sx <= 30):
                walkable = sx >= 22 and sy >= 22
            if ((sx == 5 or sx == 47) and sy >= 5 and sy <= 47) or ((sy == 5 or sy == 47) and sx >= 5 and sx <= 47):
                walkable = sx >= 36 and sy >= 36
            var resource: int = nodes.get(Vector2i(x, y), -1)
            var buildable := walkable and resource < 0
            file.store_8((1 if walkable else 0) | (2 if buildable else 0))
            file.store_8(height)
            file.store_32(resource & 0xffffffff)
    file.flush()
    var error := file.get_error()
    file.close()
    print("Generated 128x128 symmetric duel map")
    quit(0 if error == OK else 1)
