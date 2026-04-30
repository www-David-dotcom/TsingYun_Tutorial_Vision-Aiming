extends Node
# Loads a deterministic MNIST sample matching the parent chassis's
# `chassis_number` and applies it as the sticker texture on every
# ArmorPlate child. Real RM uses identical printed stickers on all four
# plates of one robot — one MNIST pick per chassis per episode, four
# plates share it.
#
# Asset layout:
#   res://assets/mnist/{0..9}/000.png ... 049.png
# Populate via tools/scripts/extract_mnist_stickers.py and copy the
# output dir into shared/godot_arena/assets/mnist/.
#
# Determinism: the chassis seeds Godot's RNG at episode reset, so the
# same episode seed picks the same MNIST PNG on every replay.

@export var mnist_root: String = "res://assets/mnist"
@export var samples_per_digit: int = 50

var _chassis: Node = null
var _plates: Array[Node] = []


func _ready() -> void:
    _chassis = get_parent()
    for child in _chassis.get_children():
        if child.has_method("apply_sticker"):
            _plates.append(child)


func load_sticker_for_current_number() -> void:
    var n: int = _chassis.chassis_number
    if n < 0 or n > 9:
        push_warning("[sticker_loader] %s: chassis_number=%d out of MNIST range 0-9" % [_chassis.team, n])
        return
    var sample_idx: int = randi() % samples_per_digit
    var path: String = "%s/%d/%03d.png" % [mnist_root, n, sample_idx]
    var tex: Texture2D = load(path) as Texture2D
    if tex == null:
        push_warning("[sticker_loader] missing MNIST texture at %s; run tools/scripts/extract_mnist_stickers.py" % path)
        return
    for plate in _plates:
        plate.apply_sticker(tex)
