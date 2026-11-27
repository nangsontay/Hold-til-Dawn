extends SceneTree

const Worker = preload("res://match-worker.gd")
const Protocol = preload("res://worker-control.gd")
const WALL := 1_700_000_000_000

class LocalWorker extends Worker:
	var events := []

	func _send(kind: String, data: Dictionary, _now: int) -> bool:
		events.append({"kind": kind, "data": data})
		return true

var failures := 0


func _initialize() -> void:
	_check_admission()
	_check_demo_bots()
	_check_sim_action_guards()
	_check_match()
	_check_chest_snapshots()
	_check_timeout_paths()
	_check_stalled_input()
	_check_resume_interactions()
	_check_replacement()
	_check_five_night_transitions()
	_check_control()
	_check_empty()
	print("match-worker-check: %d failures" % failures)
	quit(int(failures > 0))


func check(ok: bool, label: String) -> void:
	if not ok:
		failures += 1
		push_error(label)


func _worker() -> Worker:
	var worker := LocalWorker.new(GameSim.new(), {"roomId": "crew", "generation": "new", "udp": 7778, "tcp": 2569,
		"key": Crypto.new().generate_random_bytes(32), "name": "Created room"}, 0)
	worker.control.status = "online"
	worker.started = true
	return worker


func _check_sim_action_guards() -> void:
	var sim := GameSim.new()
	sim.add_player(1, "Lead", 2)
	sim.add_player(2, "Engineer", 2)
	for value in [0, 2]:
		sim.action(1, "role", value)
		sim.action(1, "mode", value)
		check(sim.players[1].role == value and sim.mode == value, "integer role and mode are accepted")
		for arg in ["2", true, null, 1.5, [], {}]:
			sim.action(1, "role", arg)
			sim.action(1, "mode", arg)
			var type := type_string(typeof(arg))
			check(sim.players[1].role == value, "role rejects " + type)
			check(sim.mode == value, "mode rejects " + type)
	sim.action(1, "start", 0)
	sim.mark_vacant(2)
	sim.resolve_vacancy(2, true)
	var bot: Dictionary = sim.players[2]
	sim.set_count(bot, "wood", 10)
	bot.pos = GameSim.WORKBENCH_POS
	sim.action(2, "craft", 0)
	check(bot.craft_idx == -1, "GameSim rejects replacement crafting")
	sim.nodes.clear()
	sim.world.rebuild(sim.nodes, sim.towers)
	var tower_spot: Vector2 = sim.free_tower_spot(0)
	bot.pos = tower_spot
	sim.action(2, "tower", 0)
	check(sim.construction.is_empty(), "GameSim rejects replacement tower construction")
	var wall_spot := GameSim.snap_to_tile(GameSim.DOOR_POS[0] + GameSim.DOOR_DIR[0] * 6 * GameSim.T)
	bot.pos = wall_spot - GameSim.DOOR_DIR[0] * 2 * GameSim.T
	sim.action(2, "build", ["wood", wall_spot, true])
	check(sim.built_walls.is_empty() and sim.count(bot, "wood") == 10, "GameSim rejects replacement wall construction")
	bot.replacement = false
	sim.action(2, "build", ["wood", wall_spot, true])
	check(sim.built_walls.size() == 1, "the wall request is valid for an ordinary Engineer")
	bot.pos = tower_spot
	sim.action(2, "tower", 0)
	check(sim.construction.size() == 1, "the tower request is valid for an ordinary Engineer")
	bot.pos = GameSim.WORKBENCH_POS
	sim.action(2, "craft", 0)
	check(bot.craft_idx == 0, "the craft request is valid for an ordinary Engineer")


func _check_chest_snapshots() -> void:
	var w := _worker()
	for peer in [2, 3, 4]:
		_join(w, peer)
	_ready_all(w)
	w.action(2, "start", 0)
	for peer in [2, 3]:
		var player: Dictionary = w.sim.players[peer]
		var chest = w.sim.exploration_chests[peer - 2]
		player.pos = chest.pos
		chest.opened = true
		w.sim.action(peer, "open_chest", chest.id)
	var data := w.snapshot(0)
	for peer in [2, 3]:
		var chest = w.sim.exploration_chests[peer - 2]
		var scoped := GameSim.snapshot_for(data, peer)
		check(scoped.chests == {peer: [chest.id, chest.slots]}, "chest viewer receives only their own open chest")
		check(scoped.room == data.room and scoped.sup == data.sup, "chest scoping preserves public room and supply state")
	check(GameSim.snapshot_for(data, 4).chests.is_empty(), "a distant peer receives no chest contents")
	check(GameSim.snapshot_for(data, 0).chests.is_empty(), "the shared packet contains no chest contents")
	check(data.chests.size() == 2, "scoping a snapshot preserves the original views for other recipients")


func _request(id: int, creator := false, role := 2, ttl := 10000) -> Dictionary:
	return {"requestId": "request-%d" % id, "identity": "human-%d" % id, "name": "Player %d with a long name" % id,
		"role": role, "creator": creator, "expiresAt": WALL + ttl}


func _challenge(worker: Worker, peer: int, ticket: String, nonce: String, now: int) -> Dictionary:
	worker.connected(peer, now)
	return worker.challenge(peer, Worker.PROTOCOL, Worker.CLIENT_BUILD, ticket, nonce, now)


func _proof(worker: Worker, peer: int, ticket: Dictionary, nonce: String, challenge: String, generation := "new") -> String:
	return Worker.ticket_proof(ticket.secret, worker.control.room_id, generation, ticket.ticketId, peer, nonce, challenge).hex_encode()


func _join(worker: Worker, peer: int, role := 2, now := 0) -> void:
	var ticket := worker.reserve(_request(peer, not worker.creator_admitted, role), now, WALL + now)
	if ticket.code != "reserved":
		check(false, "human reservation accepted")
		return
	var nonce := Protocol.random_hex(32)
	var challenge := _challenge(worker, peer, ticket.ticketId, nonce, now)
	check(worker.admit(peer, _proof(worker, peer, ticket, nonce, challenge.challenge), now) == "admitted", "human admitted")


func _check_admission() -> void:
	var w := _worker()
	check(w.reserve(_request(3), 0, WALL).code == "creator_pending", "creator first")
	var creator := _request(2, true)
	var ticket := w.reserve(creator, 0, WALL)
	check(ticket.code == "reserved" and w.reserve(creator, 0, WALL) == ticket, "reservation replay returns same ticket")
	check(w.reserve(_request(4, true), 0, WALL).code == "creator_pending", "one creator reservation")
	var nonce := Protocol.random_hex(32)
	w.connected(3, 0)
	check(w.challenge(3, Worker.PROTOCOL, Worker.CONTENT, ticket.ticketId, nonce, 0).code == "incompatible_content", "pre-acknowledgement client build rejected")
	for peer in [4, 5, 6, 7, 8]:
		var challenge := _challenge(w, peer, ticket.ticketId, nonce, 0)
		var proof := _proof(w, peer, ticket, nonce, challenge.challenge)
		match peer:
			4:
				proof = _proof(w, peer, ticket, nonce, challenge.challenge, "obsolete")
			5:
				proof = _proof(w, 4, ticket, nonce, challenge.challenge)
			6:
				proof = Worker.ticket_proof(Protocol.random_hex(32), "crew", "new", ticket.ticketId, peer, nonce, challenge.challenge).hex_encode()
		var now := Worker.CHALLENGE_MS if peer == 7 else 0
		var expected := "admitted" if peer == 8 else "invalid_proof"
		check(w.admit(peer, proof, now) == expected, "bound proof outcome for peer %d" % peer)
		check(w.admit(peer, proof, now) == "invalid_proof", "proof reuse rejected")
		check(w._report().available == 4, "proof attempts preserve one occupied seat")
	check(w.sim.leader() == 8 and w._report().name.length() == 16, "creator identity determines the first leader")
	check(w.reserve(creator, 0, WALL).code == "admitted", "consumed reserve replay reports its outcome")
	check(w.update_game(0, 5000).size() == 5, "failed and abandoned ENet handshakes time out")
	_join(w, 9)
	for id in [11, 12, 13]:
		check(w.reserve(_request(id, false, 2, 5100), 5000, WALL + 5000).code == "reserved", "slot reserved")
	check(w._report().available == 0 and w.reserve(_request(14), 5000, WALL + 5000).code == "full", "sixth seat rejected")
	var cancelled: String = w.requests["request-11"].ticket
	var cancelled_challenge := _challenge(w, 11, cancelled, nonce, 5000)
	var cancelled_proof := Worker.ticket_proof(w.reservations[cancelled].secret, "crew", "new", cancelled, 11, nonce, cancelled_challenge.challenge).hex_encode()
	check(w._release(cancelled, "cancelled", 5000) and not w._release(cancelled, "cancelled", 5000), "cancel idempotent")
	check(w.admit(11, cancelled_proof, 5001) == "invalid_ticket", "cancelled proof rejected")
	var expiring: String = w.requests["request-12"].ticket
	var expiring_challenge := _challenge(w, 12, expiring, nonce, 5000)
	var expiring_proof := Worker.ticket_proof(w.reservations[expiring].secret, "crew", "new", expiring, 12, nonce, expiring_challenge.challenge).hex_encode()
	check(w.admit(12, expiring_proof, 5100) == "invalid_ticket" and w._report().available == 3, "expiry frees seats and rejects unused proof")
	check(w.reserve(_request(12, false, 2, 5100), 5100, WALL + 5100).code == "expired", "expired replay stays expired")
	w.disconnected(8, 6000)
	check(w.sim.leader() == 9 and w._report().name.begins_with("Player 9"), "waiting leader succession")
	w.control.status = "offline"
	check(w.reserve(_request(10), 6000, WALL + 6000).code == "recovering", "no reserves during recovery")


func _ready_all(w: Worker) -> void:
	for peer in w.admitted:
		w.action(peer, "ready", true)


func _check_demo_bots() -> void:
	var w := _worker()
	w.development = true
	_join(w, 2, 0)
	w.action(2, "add_bot", 0)
	check(w.sim.players.size() == 2 and w.snapshot(0).room.bots == [-1] and w.sim.players[-1].role == 2, "leader adds Engineer bot to snapshot")
	check(w._report().bots == 1 and w._report().available == 3, "bot occupies directory seat")
	_join(w, 3, 0)
	w.action(3, "remove_bot", 0)
	w.action(3, "add_bot", 0)
	check(w.sim.players.size() == 3, "non-leader cannot change bots")
	w.action(2, "remove_bot", 0)
	check(w.snapshot(0).room.bots.is_empty() and w._report().available == 3, "leader removes bot and frees seat")
	var pending := w.reserve(_request(4), 0, WALL)
	for i in 4:
		w.action(2, "add_bot", 0)
	check(pending.code == "reserved" and w._report().available == 0 and w.sim.players.size() == 4, "bots respect reserved capacity")
	w.action(2, "start", 0)
	check(w.state() == "waiting", "development start requires ready human")
	w.action(2, "ready", true)
	w.action(2, "start", 0)
	check(w.state() == "playing", "ready human and Engineer bot can development start")
	var production := _worker()
	_join(production, 2, 2)
	production.action(2, "add_bot", 0)
	production.action(2, "ready", true)
	production.action(2, "start", 0)
	check(production.sim.players.size() == 1, "production rejects bots")
	check(production.state() == "playing", "a single ready Engineer can start without a minimum crew size")
	var succession := _worker()
	succession.development = true
	_join(succession, 2)
	succession.action(2, "add_bot", 0)
	_join(succession, 3)
	succession.disconnected(2, 0)
	check(succession.sim.leader() == 3, "bot never becomes leader")
	succession.disconnected(3, 0)
	check(succession.sim.players.is_empty(), "bots leave with last human")


func _check_match() -> void:
	var w := _worker()
	_join(w, 2, 0)
	_join(w, 3, 1)
	_ready_all(w)
	w.action(2, "start", 0)
	check(w.state() == "waiting", "two humans cannot start")
	_join(w, 4, 0)
	_ready_all(w)
	w.action(2, "start", 0)
	check(w.state() == "waiting", "Engineer required")
	w.action(4, "role", 2)
	check(w.ready.is_empty(), "role clears Ready")
	_ready_all(w)
	w.action(3, "mode", 0)
	check(w.sim.mode == 1 and w.ready.size() == 3, "non-leader cannot change mode")
	w.action(2, "mode", 0)
	check(w.ready.is_empty() and w.sim.mode == 0, "leader's mode change clears Ready")
	w.action(3, "ready", true)
	w.action(3, "ready", false)
	w.action(999, "ready", true)
	w.action(2, "add_bot", 0)
	check(w.ready.is_empty() and w.sim.players.size() == 3, "Ready toggles and unauthorized work is ignored")
	_ready_all(w)
	w.action(3, "start", 0)
	check(w.state() == "waiting", "non-leader cannot start")
	var pending := _request(5)
	var assigned := w.reserve(pending, 0, WALL)
	var nonce := Protocol.random_hex(32)
	var challenge := _challenge(w, 5, assigned.ticketId, nonce, 0)
	w.action(2, "start", 0)
	check(w.state() == "playing" and w.snapshot(0).seed == w.sim.seed_at_start, "three Ready humans start seeded match")
	check(w.reservations.is_empty() and w.reserve(pending, 1, WALL + 1).code == "room_started", "start cancels reserves")
	check(w.admit(5, _proof(w, 5, assigned, nonce, challenge.challenge), 1) == "room_started"
		and _challenge(w, 6, assigned.ticketId, nonce, 1).code == "room_started", "assigned ticket reports Start before or after challenge")
	check(_challenge(w, 7, assigned.ticketId, nonce, 10000).code == "room_unavailable", "Start outcome expires with the original ticket")
	var hero: Dictionary = w.sim.players[2]
	var start: Vector2 = hero.pos
	w.sim.input(2, start + Vector2.RIGHT * 900, 0, false, false, false, hero.tp, 1)
	check(hero.pos.distance_to(start) <= GameSim.SPEED * 0.1 + 0.01 and hero.input_sequence == 1, "movement is server-limited")
	w.sim.input(2, start, 0, true, true, true, hero.tp, 1)
	check(not hero.atk, "replayed input ignored")
	hero.hp = 17
	w.sim.set_count(hero, "wood", 11)
	hero.weapon = "stone_spear"
	hero.armour = 1
	var position: Vector2 = hero.pos
	w.sim.players[3].channel = 1.0
	w.sim._spawn(0, w.sim.active[0], 10.0)
	w.sim.proj.append({"pos": Vector2.ONE})
	var t: float = w.sim.t
	var enemies := w.sim.enemies.duplicate(true)
	w.disconnected(2, 100)
	w.action(2, "timeout_vote", ["continue", false])
	w.action(3, "timeout_vote", ["continue", true])
	w.action(3, "timeout_vote", ["continue", false])
	check(w.votes.get(3) == false, "player can change a vote while its stage is open")
	w.action(3, "timeout_vote", ["continue", true])
	w.action(3, "timeout_vote", ["continue", false], w.deadline)
	check(w.votes.get(3) == true, "vote changes at the deadline are ignored")
	var old_tp: int = w.sim.players[3].tp
	check(w.receive_input(3, 1, 1000) and not w.receive_input(3, 1, 1001) and w.snapshot(1001).room.inputAcks[3] == 1,
		"frozen input is acknowledged once for liveness")
	w.sim.input(3, Vector2.ZERO, 0, true, true, true, w.sim.players[3].tp, 1)
	w.action(3, "bandage", 0)
	w.update_game(1.0, 1100)
	check(w.state() == "timeout" and w.sim.t == t and w.sim.players[3].channel == 1.0 and not w.sim.players[3].atk
		and enemies == w.sim.enemies and w.sim.proj[0].pos == Vector2.ONE, "world, channels and input freeze")
	w.action(4, "timeout_vote", ["continue", false])
	w.update_game(0, 1100)
	check(w.timeout == "bot", "tie continues")
	w.action(3, "timeout_vote", ["bot", true])
	w.update_game(0, 1101)
	w.disconnected(4, 1102)
	w.update_game(0, 1102)
	check(w.timeout == "countdown", "all remaining humans voted for bots")
	w.update_game(0, 4101)
	check(w.sim.frozen, "countdown lasts three seconds")
	w.update_game(0, 4102)
	check(w.state() == "playing" and w.sim.players[2].bot and w.sim.players[4].bot, "one replacement per vacancy")
	w.sim.input(3, Vector2.ZERO, 0, true, true, true, old_tp, 2)
	check(w.sim.players[3].tp > old_tp and not w.sim.players[3].atk, "resume ignores pre-pause input")
	check(hero.pos == position and hero.hp == 17 and w.sim.count(hero, "wood") == 11 and hero.weapon == "stone_spear" and hero.armour == 1
		and hero.role == 0 and hero.maxhp == GameSim.ROLE_HP[0], "replacement preserves state and equipment")
	check(w.snapshot(4102).room.name.length() == 16 and w.snapshot(4102).room.name == w.sim.players[3].name,
		"room name follows the successor with a 16-character cap")
	check(w.sim.players.size() == 3 and w._report().bots == 2 and w.sim.leader() == 3, "replacement counts in population and cannot lead")
	w.sim.proj.clear()
	w.sim.boss_plan = [0, 1]
	w.sim._spawn_boss()
	var hp: float = w.sim.enemies.back().maxhp
	check(is_equal_approx(hp, 3900.0 * 2.6 / 3.4 * GameSim.MODE_MUL[w.sim.mode][1]), "boss scaling includes replacements")
	w.sim.enemies.clear()
	w.sim.wave = 1
	var with_bots := _drain_wave(w.sim, true)
	w.sim.enemies.clear()
	w.sim.carry = 0
	w.sim._remove_character(2)
	w.sim._remove_character(4)
	w.sim.wave = 1
	var without_bots := _drain_wave(w.sim, false)
	check(with_bots > without_bots and without_bots > 0, "wave scaling includes replacements (%d > %d)" % [with_bots, without_bots])
	w.disconnected(3, 4200)
	check(w.state() == "result" and w.sim.result == "no_winner" and not w.sim.frozen, "last human leaves, match ends immediately")
	w.update_game(10, 14200)
	check(w.state() == "waiting" and w.ready.is_empty() and w.sim.players.is_empty(), "lobby reset clears Ready and bots")


func _check_timeout_paths() -> void:
	var w := _worker()
	for i in range(2, 7):
		_join(w, i)
	_ready_all(w)
	w.action(2, "start", 0)
	check(w.state() == "playing", "five humans can start")
	w.sim.set_count(w.sim.players[2], "herb", 9)
	w.sim.set_count(w.sim.players[6], "wood", 4)
	var bow_slot: int = w.sim.players[6].inv.find(null)
	w.sim.players[6].inv[bow_slot] = GameSim.bow_slot(3)
	var drop_position: Vector2 = w.sim.players[2].pos
	var second_position: Vector2 = w.sim.players[6].pos
	w.disconnected(2, 100)
	w.disconnected(6, 150)
	w.update_game(0, 30099)
	check(w.timeout == "continue", "step one before deadline")
	w.update_game(0, 30100)
	w.update_game(0, 60099)
	check(w.timeout == "bot", "step two before deadline")
	w.update_game(0, 60100)
	w.update_game(0, 63100)
	check(w.sim.drops.any(func(d): return d.item == "herb" and d.n == 9 and d.pos == drop_position and d.get("from_leaver", false))
		and w.sim.drops.any(func(d): return d.item == "wood" and d.n == 4 and d.pos == second_position and d.get("from_leaver", false))
		and w.sim.drops.any(func(d): return d.item == "arrow" and d.n == 3 and d.pos == second_position and d.get("from_leaver", false))
		and not w.sim.players.has(2) and not w.sim.players.has(6),
		"no votes resumes, dropping both inventories and loaded arrows at their last positions")
	w.disconnected(3, 64000)
	for peer in w.admitted:
		w.action(peer, "timeout_vote", ["continue", false])
	w.update_game(0, 64000)
	check(w.sim.result == "no_winner" and w.state() == "result" and w.sim.players.size() == 2, "End majority yields neutral result")
	check(w.reserve(_request(20, false, 2, 70000), 64000, WALL + 64000).code == "room_started", "result has no admission")
	w.sim.built_walls.append({"pos": Vector2(320, 320), "vert": true})
	w.update_game(10, 74000)
	check(w.ready.is_empty() and w.state() == "waiting" and w.sim.players.size() == 2 and w.sim.built_walls.is_empty(), "humans return to same lobby with a cleared world")


func _check_stalled_input() -> void:
	var w := _worker()
	for id in [2, 3, 4]:
		_join(w, id)
	_ready_all(w)
	w.action(2, "start", 0, 100)
	w.last_input.erase(4)
	w.receive_input(3, 1, 1000)
	check(w.update_game(0, 1000).is_empty() and w.sim.frozen and w.snapshot(1000).room.timeout == "connection",
		"silent peer pauses the match without disconnecting")
	for tick in range(1050, 12051, 50):
		w.receive_input(3, 2 + (tick - 1050) / 50, tick)
		check(w.update_game(0, tick).is_empty(), "healthy peer stays connected throughout the pause")
	w.receive_input(3, 223, 12099)
	check(w.update_game(0, 12099).is_empty(), "silence below the cap preserves all peers")
	w.receive_input(3, 224, 12100)
	var removed := w.update_game(0, 12100)
	check(removed == [2], "only the peer silent for the cap is disconnected")
	check(w.timeout == "continue" and w.vacancies.has(2) and w.admitted.has(3) and w.admitted.has(4),
		"stalled peer departure opens the continue vote and preserves healthy and untracked peers")
	w.disconnected(2, 12101)
	check(w.vacancies.size() == 1 and w.deadline == 42100, "duplicate disconnect does not restart the vote")

	var recovered := _worker()
	for id in [2, 3]:
		_join(recovered, id)
	_ready_all(recovered)
	recovered.action(2, "start", 0, 100)
	recovered.receive_input(3, 1, 1000)
	recovered.update_game(0, 1000)
	recovered.receive_input(2, 1, 2000)
	recovered.receive_input(3, 2, 2000)
	check(recovered.update_game(0, 2000).is_empty() and not recovered.sim.frozen and recovered.timeout.is_empty(),
		"input returning before the cap resumes without disconnecting")

	var multiple := _worker()
	for id in [2, 3, 4]:
		_join(multiple, id)
	_ready_all(multiple)
	multiple.action(2, "start", 0, 100)
	multiple.receive_input(4, 1, 12100)
	check(multiple.update_game(0, 12100) == [2, 3] and multiple.vacancies.size() == 2 and multiple.timeout == "continue",
		"all simultaneously stalled peers enter the same departure vote")


func _check_replacement() -> void:
	var w := _worker()
	for id in [2, 3, 4]:
		_join(w, id)
	_ready_all(w)
	w.action(2, "start", 0, 100)
	w.last_input[3] = 500
	w.last_input[4] = 500
	w.update_game(0, 999)
	check(not w.sim.frozen, "active liveness allows the whole grace period")
	w.update_game(0, 1000)
	check(w.sim.frozen and w.snapshot(1000).room.timeout == "connection" and w.admitted.size() == 3
		and w.vacancies.is_empty(), "missing input freezes at 900 ms and retains every seat")
	w.receive_input(2, 1, 1100)
	w.update_game(0, 1100)
	check(not w.sim.frozen and w.timeout.is_empty() and w.vacancies.is_empty(), "traffic recovery resumes with no vote or vacancy")
	w.disconnected(2, 1200)
	check(w.timeout == "continue" and w.vacancies.has(2), "transport-confirmed disconnect opens the vote")
	var sim := w.sim
	sim.resolve_vacancy(2, true)
	w.vacancies.clear()
	w._resume()
	var bot: Dictionary = sim.players[2]
	bot.pos = GameSim.Bots.WORKBENCH
	sim.set_count(bot, "wood", 140)
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(GameSim.count_in(sim.storage, "wood") == 140 and sim.count(bot, "wood") == 0, "full replacement stores resources at the bench")
	var outside := Vector2(GameSim.HALF.x + GameSim.SAFE_D + GameSim.T, 0)
	var safe := Vector2(GameSim.HALF.x + 4 * GameSim.T, 0)
	bot.pos = safe
	sim.nodes = [{"type": 1, "pos": outside, "amount": 5}, {"type": 3, "pos": safe, "amount": 5},
		{"type": 0, "pos": safe, "amount": 5}]
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(bot.bot_node == 2 and bot.inter, "daytime replacement gathers safe resources only")
	sim.nodes.append({"type": 2, "pos": safe + Vector2.RIGHT * GameSim.T, "amount": 5})
	sim.set_count(bot, "wood", 120)
	sim.set_count(bot, "herb", 1)
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(bot.bot_node == 3, "replacement switches from a full wood stack to a resource it can carry")
	sim.phase = "night"
	sim.active = [0, 1]
	var guard_spot: Vector2 = GameSim.DOOR_POS[1] + GameSim.DOOR_DIR[1] * 2.0 * GameSim.T
	sim.players[3].pos = GameSim.DOOR_POS[0]
	sim.players[4].pos = GameSim.DOOR_POS[0]
	bot.pos = guard_spot
	sim.enemies = [{"pos": guard_spot + Vector2.UP * GameSim.T, "r": 8.0}]
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(bot.atk and is_equal_approx(bot.aim, -PI / 2), "replacement guards the least defended attacked door")
	sim.enemies[0].pos = GameSim.DOOR_POS[1] + Vector2.UP * 6.25 * GameSim.T
	check(GameSim.Bots._foe(sim, bot, GameSim.DOOR_POS[1]) == null, "replacement ignores targets outside six tiles")
	sim.set_count(bot, "wood", 1)
	bot.atk = false
	bot.pos = GameSim.DOOR_POS[0] + GameSim.DOOR_DIR[0] * GameSim.T
	sim.doors = [100.0, 300.0, 500.0]
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(bot.inter, "Engineer replacement prioritizes the most damaged door")
	bot.role = 1
	bot.pos = guard_spot
	sim.players[3].state = 1
	sim.players[3].pos = guard_spot + Vector2.UP * GameSim.T * 0.5
	bot.inter = false
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(bot.inter, "Healer replacement prioritizes a nearby downed teammate")
	sim.players[3].state = 0
	bot.hp = 20
	sim.set_count(bot, "bandage", 1)
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(bot.channel == 2.0, "replacement bandages below 40 percent HP")
	bot.role = 2
	bot.pos = Vector2.ZERO
	bot.inside = true
	sim.set_count(bot, "wood", 10)
	var towers := sim.towers.duplicate()
	w.action(2, "craft", 0)
	w.action(2, "tower", 0)
	check(bot.armour == 0 and sim.count(bot, "wood") == 10 and sim.towers == towers, "replacement cannot use craft or build RPCs")
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(not bot.inside and not GameSim.inside_base(bot.pos), "Engineer replacement stays outside at night")
	bot.pos = Vector2(GameSim.HALF.x + GameSim.SAFE_D + 3 * GameSim.T, 0)
	var distance: float = GameSim.wall_dist(bot.pos)
	GameSim.Bots._think_replacement(sim, bot, 0.1)
	check(GameSim.wall_dist(bot.pos) < distance, "replacement moves back toward the twenty-tile safe zone")
	w.disconnected(4, 1200)
	w.action(2, "timeout_vote", ["continue", false], 1201)
	check(w.votes.is_empty(), "replacement cannot vote")


# Each night wave is paced one enemy per director tick; run it out and count what arrived.
func _drain_wave(sim: GameSim, night_phase: bool) -> int:
	if night_phase:
		sim.phase = "night"
		sim.reset_night_spawns()
		sim.plan_night_quota()
	sim.plan_night_wave()
	for step in 4000:
		sim.elapsed += 0.05
		sim.tick_pending_night_spawn()
		if sim.pending_wave_budget <= 0 and sim.enemies.size() > 0:
			break
	return sim.enemies.size()


func _check_five_night_transitions() -> void:
	# Exercise the real transitions with controlled timers and boss outcomes. Full-duration play uses player combat.
	var w := _worker()
	for id in [2, 3, 4]:
		_join(w, id)
	_ready_all(w)
	w.action(2, "start", 0)
	for night in range(1, 6):
		check(w.sim.phase == "day" and w.sim.k == night, "dawn before night %d" % night)
		# the skip vote only counts once DAY_MIN of daylight has passed (FML-14)
		w.sim.t = GameSim.DAY_MAX - GameSim.DAY_MIN
		w.action(2, "vote", 0)
		w.action(3, "vote", 0)
		w.update_game(0.02, night * 100)
		check(w.sim.phase == "dusk", "crew vote advances to dusk")
		# night 5 refuses to start without the assembled locator radio (FML-13)
		if night == 5:
			w.sim.radio_built = true
		w.sim.t = 0
		w.update_game(0.02, night * 100 + 1)
		check(w.sim.phase == "night" and w.snapshot(0).k == night, "worker publishes night %d" % night)
		if night in [2, 4]:
			# the boss is planned at dawn, so pin the archetype before it spawns
			w.sim.boss_plan = [0, 1]
			w.sim.elapsed = GameSim.BOSS_AT
			w.update_game(0.02, night * 100 + 2)
			check(w.sim._boss() != null, "specimen spawns on night %d" % night)
			w.sim.boss_dead = true
		w.sim.t = 0
		w.sim.enemies.clear()
		w.update_game(0.02, night * 100 + 3)
	check(w.state() == "result" and w.snapshot(0).result == "won", "night five reaches extraction")


func _check_control() -> void:
	var w := _worker()
	w.control.key = Crypto.new().generate_random_bytes(32)
	var body := Marshalls.raw_to_base64("{}".to_utf8_buffer())
	var good := w.control._fields("manager", 1, "close", body)
	var signed := good.slice(1)
	signed.append(Protocol.mac(w.control.key, good).hex_encode())
	check(w.control._verify(signed).type == "close", "valid authenticated control frame")
	for field in [3, 5, 8]:
		var bad := signed.duplicate()
		bad[field] = "0".repeat(64) if field == 8 else "other"
		check(w.control._verify(bad).is_empty(), "wrong generation, counter or MAC rejected")
	w.control.received = 1
	check(w.control._verify(signed).is_empty(), "replayed counter rejected")


func _check_empty() -> void:
	var w := _worker()
	w.update_game(0, 59999)
	check(w.exit_code == -1, "not empty for sixty seconds yet")
	var request := _request(2, true, 2, 70000)
	var ticket := w.reserve(request, 59999, WALL + 59999)
	w.update_game(0, 65000)
	check(w.exit_code == -1 and w.reserve(request, 65000, WALL + 65000).ttlMs == 5000,
		"late reservation pauses empty closure and retry reports remaining monotonic TTL")
	var nonce := Protocol.random_hex(32)
	var ch := _challenge(w, 2, ticket.ticketId, nonce, 65000)
	check(w.admit(2, _proof(w, 2, ticket, nonce, ch.challenge), 65000) == "admitted", "admission resets empty timer")
	w.disconnected(2, 66000)
	w.update_game(0, 125999)
	check(w.exit_code == -1, "second empty period is continuous")
	w.update_game(0, 126000)
	check(w.exit_code == 0, "empty worker exits at sixty seconds")
	var expired := _worker()
	expired.reserve(request, 59999, WALL + 59999)
	expired.update_game(0, 70000)
	check(expired.exit_code == -1, "empty countdown still has one millisecond after reservation expiry")
	expired.update_game(0, 70001)
	check(expired.exit_code == 0, "empty countdown resumes after reservation expiry")


func _check_resume_interactions() -> void:
	var w := _worker()
	for id in [2, 3, 4]:
		_join(w, id)
	_ready_all(w)
	w.action(2, "start", 0, 100)
	var sim := w.sim
	var p: Dictionary = sim.players[2]
	var target: Dictionary = sim.players[3]
	for channel in ["gather_acc", "revive_p", "entry"]:
		for held in [true, false]:
			sim.phase = "night" if channel == "entry" else "day"
			p.pos = GameSim.DOOR_POS[0] + GameSim.DOOR_DIR[0] * 24.0 if channel == "entry" else Vector2(300, 0)
			sim.nodes = [{"type": 0, "pos": p.pos, "amount": 5}]
			target.pos = p.pos
			target.state = 1 if channel == "revive_p" else 0
			target.bleed = 20.0
			p.gather_acc = 0.0
			p.revive_p = 0.0
			p.entry = 0.0
			p[channel] = 0.5
			p.gather_pos = p.pos
			p.entry_pos = p.pos
			sim.frozen = true
			var old_tp: int = p.tp
			var sequence: int = p.input_sequence + 1
			sim.tick(0.1)
			check(p[channel] == 0.5, "%s progress survives freeze" % channel)
			w._resume()
			sim.input(2, p.pos, 0, false, true, true, old_tp, sequence)
			sim._tick_players(0.1)
			check(p[channel] == 0.5, "%s waits for the first fresh input" % channel)
			sim.input(2, p.pos, 0, false, held and channel != "entry", held and channel == "entry", p.tp, sequence)
			sim._tick_players(0.1)
			check(p[channel] > 0.5 if held else p[channel] == 0.0, "%s honors the post-resume %s" % [channel, "hold" if held else "release"])
