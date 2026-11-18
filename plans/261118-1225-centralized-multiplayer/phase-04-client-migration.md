---
phase: 4
title: "Move clients to the hosted lobby and matches"
status: pending
priority: P1
dependencies: [3]
---
# Move clients to the hosted lobby and matches

**Outcome.** A desktop player presses Play, chooses a room, enters the crew, and plays without entering an address.

- [ ] In [main.gd](../../main.gd) replace public Host and Join by IP with Play. Isolate WSS directory/session handling in a small lobby client. Configure a developer DNS endpoint and a LAN launch override. Keep the existing HUD, controls, snapshot decoder and movement reconciliation.
- [ ] Show Connecting, then every room’s current leader name (up to 16 characters), population, mode and state. Disable Join for full or running rooms. At three rooms disable Create and show **Server full** with Refresh. Display **Server offline** with Retry after 5 s. Distinguish a reachable lobby with a blocked UDP connection.
- [ ] Separate reservation, connecting, admitted, crew and match states. Use the assigned endpoint, build and worker generation. Clear stale replies, reservations and peers on cancellation, timeout, Leave or a newer request. Enter the crew only after worker admission. Preserve identical RPC node paths on both ends; server peer 1 and the human leader have different roles.
- [ ] Show each human’s Ready state and selected role, the leader’s mode and the reason Start is unavailable. All 3–5 humans, including the leader, must be ready with at least one Engineer. Changing a role or mode clears every Ready flag. Keep manual demo bots and solo start in development routes.
- [ ] Build the [GDD timeout vote panel](../../GDD_Assignment1.pdf) using worker state. Freeze prediction while game time is frozen. Show the leaver(s), vote choices/counts, time left, missing-Engineer warning and resume countdown. Allow remaining players to change votes while the stage is open. Discard pre-pause input on resume. Show a replacement as BOT, or “Match ended by vote” when the match ends without a winner.
- [ ] After results return to the same crew; Leave room returns to the directory. Losing only WSS leaves an active ENet match running. Use a monotonic liveness deadline, including during freezes: loss of worker traffic returns the client to the menu with a message within 5 s of its last valid packet. Track acknowledgements to detect a one-way outage. Keep offline developer shortcuts gated out of release builds and enable headless test clients explicitly.

**Verification.** Use exported Windows and macOS clients against a real directory and worker. Cover duplicate clicks, cancellation after ticket issuance, a stale room selection, full room, invalid credential, denied mid-match join, role changes, leader succession, both vote stages and one complete match cycle. Compare teleport reset and movement corrections with phase 1.

**Rollback.** Keep the previous client package as a development comparison. Couple public client and worker protocol versions and reject incompatible builds before admission.
