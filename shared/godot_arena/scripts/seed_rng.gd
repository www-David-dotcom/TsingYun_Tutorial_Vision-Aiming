extends Node
# Autoloaded as `SeedRng`. Single source of randomness for the arena so
# that the same EnvReset.seed gives byte-identical episodes.
#
# Subsystems that need randomness call SeedRng.next_*() instead of
# randf() / randi(). EnvReset on arena_main.gd reseeds this on every
# new episode.

var _rng: RandomNumberGenerator = RandomNumberGenerator.new()
var _current_seed: int = 0


func reseed(seed_value: int) -> void:
    _current_seed = seed_value
    _rng.seed = seed_value
    _rng.state = seed_value


func current_seed() -> int:
    return _current_seed


func next_float() -> float:
    return _rng.randf()


func next_range(lo: float, hi: float) -> float:
    return _rng.randf_range(lo, hi)


func next_int(lo: int, hi: int) -> int:
    return _rng.randi_range(lo, hi)
