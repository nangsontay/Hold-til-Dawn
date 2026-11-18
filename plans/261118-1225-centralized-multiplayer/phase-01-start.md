---
phase: 1
title: "Prove lobby and ENet compatibility"
status: pending
priority: P1
dependencies: []
---
# Prove the two connections before changing the menu

**Outcome.** Establish a reproducible working pair of desktop client and server builds and a reference run of the current game. Keep the public entry point available for comparison throughout this phase.

- [ ] Verify worker and clients include the same imported map, textures and collision geometry. Clean-import first. Record the Godot binary version, repository revision and baseline results. From the repository root run the commands below. Exercise an actual dedicated host and two clients on a local network; note leader, roles, Engineer start gate, bots, movement, actions, one night transition, disconnect and result-to-crew behavior. Record gaps against MEC-24/27/29: immediate player removal, absent Ready state and solo start in the prototype.
- [ ] Pin the native Godot plugin release, matching Colyseus server dependencies, Node runtime and platform binaries. Use the [official native SDK release link in the Godot guide](https://docs.colyseus.io/getting-started/godot). Keep production dependencies separate from the [benchmark package](../261118-1143-multiplayer-benchmark/colyseus/package.json). Check redistribution notices and packaging for both architectures actually shipped.
- [ ] In a development entry point, connect an exported Windows client and an exported macOS client to a real Colyseus room over WSS. Exchange a typed request/reply, then connect a Godot ENet peer to a dedicated test worker. Exchange an action and receive a snapshot. Check clean disconnect, invalid lobby certificate rejection, and that losing only WSS leaves the existing ENet match playable.
- [ ] Record the tested version pair, export presets, OS/architecture and smoke invocation. Keep the verified handshake and package pins as the basis for the real feature. Remove any throwaway test entry point before shipping.

```sh
godot --version
godot --headless --editor --path . --import
godot --headless --path . -s tests/sim_check.gd
godot --headless --path . -s tests/geometry_check.gd
godot --headless --path . -- --server
```

The last command runs a server until stopped. Stop the owned process after the local comparison. Track the same game build across subsequent phases.

**Files.** Read [project.godot](../../project.godot), [main.gd](../../main.gd), [README](../../README.md) and both checks above. Planned additions include the pinned SDK under `addons/colyseus/`, a production Node package under `multiplayer-server/` and export presets if needed. Keep export credentials out of source control.

**Gate and rollback.** Simulation and geometry checks pass and both exported clients complete both connections. If a target cannot load the native SDK or validate WSS, record the build and failure and resolve the version or packaging issue here before changing the shipping path.

**Handoff to the worker.** Record the lobby DNS/WSS URL, private loopback control endpoint, fixed match ports 7778–7780, launch binary and protected credential source as configuration names and development defaults. Never include credential values. Confirm whether the native SDK must load in the worker export; a worker without lobby SDK code must run as an exported artifact on a clean target-architecture Linux machine before the menu cutover.

**Baseline verified during planning.** A clean temporary copy imported with Godot 4.7.2; both existing checks exited 0. Godot logged sandbox warnings when saving editor preferences and accessing the macOS certificate store. The WSS, export and hosted-network gates still require implementation-stage tests.
