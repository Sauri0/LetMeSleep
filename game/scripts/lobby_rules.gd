class_name LobbyRules
extends RefCounted

## Lobby participants have no chosen team. A fresh draw supplies the authoritative
## per-round roster; old roles never influence this lottery.
const SimulationData = preload("res://scripts/simulation.gd")

static func validate(players: Dictionary, config: Dictionary, require_ready: bool = true) -> String:
	var total: int = players.size()
	if total < 2:
		return "Hacen falta al menos 2 jugadores: un humano y un mosquito."
	if total > SimulationData.MAX_PLAYERS:
		return "La sala admite hasta 16 jugadores en total."
	var requested: Variant = config.get("human_count", 1)
	if not requested is int or int(requested) < 1 or int(requested) > SimulationData.MAX_HUMANS:
		return "La cantidad de humanos debe ser un entero entre 1 y 5."
	var humans: int = int(requested)
	var mosquitoes: int = total - humans
	if mosquitoes < 1:
		return "Con %d humanos hacen falta al menos %d jugadores para incluir un mosquito." % [humans, humans + 1]
	if mosquitoes > SimulationData.MAX_MOSQUITOES:
		return "Quedarían %d mosquitos; este prototipo admite hasta 12. Aumentá la cantidad de humanos o reducí la sala." % mosquitoes
	for id: Variant in players:
		if not id is int or int(id) <= 0 or not players[id] is Dictionary:
			return "La lista de jugadores no es válida."
		if require_ready:
			var ready: Variant = players[id].get("ready", false)
			if not ready is bool or not bool(ready):
				return "Falta que todos pulsen Estoy listo."
	return ""

static func draw(players: Dictionary, config: Dictionary, rng: RandomNumberGenerator = null) -> Dictionary:
	# Empty means invalid capacity/configuration. Call validate to get its readable
	# explanation. Readiness belongs to the start gate, not this pure lottery.
	if not validate(players, config, false).is_empty():
		return {}
	var generator: RandomNumberGenerator = rng
	if generator == null:
		generator = RandomNumberGenerator.new()
		generator.randomize()
	var ids: Array = players.keys()
	ids.sort() # Stable input ordering makes an injected seed reproducible.
	for index: int in range(ids.size() - 1, 0, -1):
		var other: int = generator.randi_range(0, index)
		var old: int = int(ids[index])
		ids[index] = ids[other]
		ids[other] = old
	var result: Dictionary = players.duplicate(true)
	var humans: int = int(config.get("human_count", 1))
	for index: int in range(ids.size()):
		result[int(ids[index])].role = "human" if index < humans else "mosquito"
	return result
