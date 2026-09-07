extends SceneTree

const Lobby = preload("res://scripts/lobby_rules.gd")
const Sim = preload("res://scripts/simulation.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	_test_validation()
	_test_exact_teams()
	_test_seeded_draws()
	_test_lottery_coverage()
	_test_deep_copy()
	print("LOBBY_RULES_TEST_RESULT checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)

func check(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + description)

func players(count: int) -> Dictionary:
	var result: Dictionary = {}
	for index: int in range(count):
		result[100 + index * 7] = {"name": "Amigo %d" % index, "ready": true, "cosmetics": {"human": {"color": index % 6, "accessory": index % 3}, "mosquito": {"color": (index + 1) % 6, "accessory": (index + 1) % 3}}}
	return result

func seeded(seed_value: int) -> RandomNumberGenerator:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	return rng

func human_ids(drawn: Dictionary) -> Array[int]:
	var result: Array[int] = []
	for id: int in drawn:
		if drawn[id].role == "human":
			result.append(id)
	result.sort()
	return result

func _test_validation() -> void:
	check(Lobby.validate(players(2), {"human_count": 1}).is_empty(), "ready minimum 1v1 lobby accepted")
	check(Lobby.validate(players(6), {"human_count": 5}).is_empty(), "5v1 accepted without old ratio")
	check(Lobby.validate(players(16), {"human_count": 4}).is_empty(), "4v12 full room accepted")
	check(Lobby.validate(players(16), {"human_count": 5}).is_empty(), "5v11 full room accepted")
	check(not Lobby.validate(players(1), {"human_count": 1}).is_empty(), "one participant cannot start")
	check(not Lobby.validate({}, {"human_count": 1}).is_empty(), "empty lobby cannot start")
	check(not Lobby.validate(players(17), {"human_count": 5}).is_empty(), "transport room limit enforced")
	check(not Lobby.validate(players(14), {"human_count": 1}).is_empty(), "provisional 12-mosquito limit enforced")
	check(not Lobby.validate(players(2), {"human_count": 2}).is_empty(), "all-human lobby cannot start")
	check(not Lobby.validate(players(2), {"human_count": 5}).is_empty(), "human count never silently clamps to connected participants")
	for invalid: Variant in [0, -1, 6, 99, 1.0, "1", true, null, NAN]:
		check(not Lobby.validate(players(8), {"human_count": invalid}).is_empty(), "malformed or unsupported human count rejected: %s" % str(invalid))
		check(Lobby.draw(players(8), {"human_count": invalid}, seeded(1)).is_empty(), "invalid human count yields no drawn roster")
	var unready: Dictionary = players(2)
	unready[100].ready = false
	check(not Lobby.validate(unready, {"human_count": 1}).is_empty(), "readiness required at start gate")
	check(Lobby.validate(unready, {"human_count": 1}, false).is_empty(), "capacity validation can explicitly ignore readiness")
	check(not Lobby.draw(unready, {"human_count": 1}, seeded(4)).is_empty(), "pure draw can be tested independently of readiness")
	unready[100].ready = "yes"
	check(not Lobby.validate(unready, {"human_count": 1}).is_empty(), "truthy nonboolean readiness rejected")
	var invalid_players: Dictionary = players(2)
	invalid_players["fake"] = {"ready": true}
	check(not Lobby.validate(invalid_players, {"human_count": 1}).is_empty(), "nonnumeric peer id rejected")
	invalid_players = players(2)
	invalid_players[100] = "broken"
	check(not Lobby.validate(invalid_players, {"human_count": 1}).is_empty(), "invalid participant record rejected")

func _test_exact_teams() -> void:
	var rng: RandomNumberGenerator = seeded(321)
	for total: int in range(2, 17):
		for humans: int in range(1, 6):
			var mosquitoes: int = total - humans
			var feasible: bool = mosquitoes >= 1 and mosquitoes <= 12
			var config: Dictionary = {"human_count": humans}
			var roster: Dictionary = players(total)
			check(Lobby.validate(roster, config).is_empty() == feasible, "capacity evaluated exactly for %dv%d" % [humans, mosquitoes])
			var drawn: Dictionary = Lobby.draw(roster, config, rng)
			if not feasible:
				check(drawn.is_empty(), "impossible teams return no partial/clamped draw")
				continue
			check(drawn.size() == total and human_ids(drawn).size() == humans, "lottery creates exact requested human count for %dv%d" % [humans, mosquitoes])
			check(Sim.validate_roster(drawn).is_empty(), "drawn roster satisfies authoritative game rules")
			var mosquito_count := 0
			for entry: Dictionary in drawn.values():
				if entry.role == "mosquito":
					mosquito_count += 1
			check(mosquito_count == mosquitoes, "every remaining participant becomes mosquito")

func _test_seeded_draws() -> void:
	var roster: Dictionary = players(8)
	var one: Dictionary = Lobby.draw(roster, {"human_count": 3}, seeded(4567))
	var two: Dictionary = Lobby.draw(roster, {"human_count": 3}, seeded(4567))
	check(one == two, "injected RNG gives deterministic reproducible draw")
	var reversed: Dictionary = {}
	var ids: Array = roster.keys()
	ids.reverse()
	for id: int in ids:
		reversed[id] = roster[id]
	check(human_ids(Lobby.draw(reversed, {"human_count": 3}, seeded(4567))) == human_ids(one), "dictionary insertion order does not bias seeded lottery")
	for id: int in roster:
		roster[id].role = "human"
	check(human_ids(Lobby.draw(roster, {"human_count": 3}, seeded(4567))) == human_ids(one), "old/chosen roles never influence new draw")
	var rng: RandomNumberGenerator = seeded(97531)
	var repeated := false
	var last := -1
	for round_index: int in range(100):
		var chosen: int = human_ids(Lobby.draw(players(2), {"human_count": 1}, rng))[0]
		if chosen == last:
			repeated = true
		last = chosen
	check(repeated, "independent lottery permits repeated roles across consecutive rounds")

func _test_lottery_coverage() -> void:
	var roster: Dictionary = players(10)
	var counts: Dictionary = {}
	for id: int in roster:
		counts[id] = 0
	var rng: RandomNumberGenerator = seeded(982734)
	for round_index: int in range(500):
		var drawn: Dictionary = Lobby.draw(roster, {"human_count": 3}, rng)
		for id: int in human_ids(drawn):
			counts[id] = int(counts[id]) + 1
	for id: int in counts:
		# Broad deterministic coverage check, not a claim of measured online fairness.
		check(int(counts[id]) > 80 and int(counts[id]) < 220, "every player can receive either team in repeated independent draws")

func _test_deep_copy() -> void:
	var original: Dictionary = players(3)
	var before: Dictionary = original.duplicate(true)
	var drawn: Dictionary = Lobby.draw(original, {"human_count": 1}, seeded(8))
	check(original == before and not original[100].has("role"), "draw does not mutate persistent lobby roster")
	check(drawn[100].cosmetics == original[100].cosmetics, "draw preserves both saved role cosmetics")
	drawn[100].cosmetics.human.color = 99
	check(original[100].cosmetics.human.color == before[100].cosmetics.human.color, "draw owns a deep copy of cosmetics")
	check(drawn[100].name == original[100].name and drawn[100].ready == original[100].ready, "draw preserves player identity and readiness")
	check(not Lobby.draw(players(2), {"human_count": 1}).is_empty(), "default RNG can produce a valid random roster")
