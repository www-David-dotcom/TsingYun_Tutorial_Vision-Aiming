extends Node
# Per-episode replay recorder. Writes a JSON event stream to user://
# replays/<episode_id>.json so the team can re-render or audit episodes
# offline. The MP4 side is handled by Godot's --write-movie command-line
# flag (see shared/godot_arena/README.md), not by this script.

var _file: FileAccess
var _episode_id: String = ""
var _seed: int = 0
var _open: bool = false


func start(episode_id: String, seed_value: int) -> void:
    _episode_id = episode_id
    _seed = seed_value
    var dir: String = "user://replays"
    DirAccess.make_dir_recursive_absolute(dir)
    var path: String = "%s/%s.json" % [dir, episode_id]
    _file = FileAccess.open(path, FileAccess.WRITE)
    if _file == null:
        push_error("replay_recorder: could not open %s" % path)
        _open = false
        return
    _open = true
    _write_line({
        "kind": "header",
        "episode_id": episode_id,
        "seed": seed_value,
        "version": "0.6.0",
    })


func record(event: Dictionary) -> void:
    if not _open:
        return
    _write_line(event)


func finish(stats: Dictionary) -> void:
    if not _open:
        return
    _write_line({"kind": "footer", "stats": stats})
    _file.close()
    _file = null
    _open = false


func _write_line(payload: Dictionary) -> void:
    _file.store_line(JSON.stringify(payload))
