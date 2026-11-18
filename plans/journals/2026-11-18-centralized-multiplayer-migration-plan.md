---
title: Centralized multiplayer migration plan
date: 2026-11-18
summary: Confirmed Colyseus discovery and Godot ENet match ownership against the game design.
---

# Centralized multiplayer migration plan

The migration keeps gameplay in developer-hosted Godot workers and uses Colyseus for discovery and admission. The GDD rules out in-progress rejoin and defines timeout votes, empty-room lifetime and lobby restart recovery. The user chose three rooms for the initial test and Server full with refresh at capacity.

[Migration plan](../261118-1225-centralized-multiplayer/plan.md)

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
