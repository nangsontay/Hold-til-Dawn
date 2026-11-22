# Hold 'til Dawn (prototype)

Godot 4.7.2. Rules from the GDD live in `game.gd` (server only); `main.gd` is network, input, drawing and HUD.

## Run
- **Play locally / host:** open the project in Godot, press Play, choose a role, click **Host**. Others click **Join by IP** with the host's IP (UDP 7777).
- **Dedicated server:** `godot --headless --path . -- --server`, then every player joins by IP.
- The lobby leader (first to join) picks the mode and presses **Start** (needs at least one Engineer).

## Controls
WASD move, mouse aim, LMB attack, E (hold) gather / repair door (Engineer) / revive, Q bandage, C workbench (inside the base by day, then number keys), RMB healing flask (Healer), F (hold 2 s, Engineer at night) enter or leave the base through a closed door, V skip-day vote, T (Engineer, 3-8 tiles in front of a door) build a tower, mouse wheel zoom (2, 3 or 4 screen pixels per art pixel), Esc leave.

For playtests, the day-skip vote is available immediately: press V at any time during the day. Once at least 60% of human players vote (one vote when playing solo or with bots), the 5-second dusk countdown begins, followed by night.

## What is in
- 5 day/night cycles on an 11 x 7 tile base (GDD: 15 x 10) with doors A (west), B (north), C (east), no south door; players enter only through doors, and at night only the Engineer (hold F) or anyone through a broken door.
- Players, enemies and bots share scenery/resource/building colliders from `world_geometry.gd`. Movement is swept in small steps; the server validates player movement and acknowledges predicted inputs. Grass, pebbles, bushes and herbs stay walk-through. Enemies and bots navigate around obstacles with cached A* paths.
- Bot teammates (`bots.gd`, not in the GDD, for demos and solo tests): the lobby leader adds or removes them (max 5 players); a bot takes the missing role (Engineer, then Healer, Scout). They run on the server with the same rules as people: dodge boss slams, revive downed teammates, Healer flasks, fight, Engineer door repair, gather by day and craft near dusk, hold the attacked door at night. Only people count for the day-skip vote.
- Night waves spawn all around the island, from 8 tiles outside the base to near the coast, including the south, and head for the nearest attacked door. First wave after 5 s, then every 30 s (15 s on night 5); all enemies, including daytime roamers and bosses, move at half their original speed. FML-04 budget x `SPAWN_MUL` = 3 remains enabled for playtests; boss on nights 2 and 4, night-5 helicopter extraction.
- Daytime roamers beyond 20 tiles from the base (chance grows with distance, x `ROAM_MUL` = 2 for playtests, pairs, max 8); at dusk they walk to an attacked door.
- Enemy drops (GDD: Grunt 10%, Spitter 15%, Wrecker 20%, boss always; x `DROP_MUL` = 3 for playtests; resource nodes x `NODE_MUL` = 2): walk over to pick up, gone after 30 s, not from inside the base.
- Defence towers (AGT-12, MEC-11, MEC-28, BAL-28): 300 HP, shoot the nearest enemy within 6 tiles at 8 DPS, light 6 tiles at night for everyone. One free tower (placed with T by an Engineer on day 1, or it appears in front of night 1's door at dusk); more cost 10 wood + 5 metal after the first boss; one per door. At night enemies walking into a tower's light attack it first (Wreckers x2, Spitters never); the Engineer repairs it like a door, a destroyed one only to 30% and only with no enemy in its light.

## 2.5D look (branch `proto-2.5d`)
- The rules are still flat 2D (`game.gd` unchanged); only `main.gd` draws differently: everything with height (trees, rocks, walls, enemies, players) is sorted by the y of its foot, walls have a top and a front face, and bodies stand above their shadows. Scenery and resource nodes stay opaque even when a character stands behind them; depleted nodes are smaller until dawn.
- Pixel-art world (GDD style guide): everything is drawn without smoothing, and the camera zoom keeps one art pixel on a whole number of screen pixels.
- Fonts in `fonts/` (SIL OFL 1.1, licence files next to them): **Pixelify Sans** (Stefie Justprince, weight 560) for all text, **Jersey 10** (Sarah Cadigan-Fried) for titles, the timer, big numbers and banners.
- Base props in `art/props/` (lighthouse = beacon, workbench, tower, wrecked tower, landing pad) are pixel art drawn by `tools/pixel_props.gd`; doors have lamp pillars that light up at dusk.
- Nature sprites in `art/nature/` are pixel art (32 px per tile, banded colours, 1 px outline) rendered from Quaternius 3D models (CC0) by `tools/bake_nature.gd`; see `art/nature/CREDITS.txt`.
- Players and enemies in `art/chars/` are 32x32 pixel art (Wrecker 40x40) of the GDD characters (CHR-01..03 Scout, Healer, Engineer; CHR-05..07 Grunt, Spitter, Wrecker), drawn by `tools/pixel_chars.gd` (pixel maps, GDD palette, outline, 4 directions, idle + walk); see `art/chars/CREDITS.txt`. The PNGs can be repainted by hand. A boss is a big tinted Wrecker for now; a coloured ring at a player's feet shows the role.

## Dev
- Playtest settings: open `config/playtest.tres` in Godot's Inspector to tune dusk, wave timing, enemy speed, multipliers and camera follow. Restart the game after changing these settings. Existing defaults retain immediate day skipping, 5-second dusk and half-speed enemies.
- Editable landmarks: open `maps/island.tscn` in Godot's 2D editor. Duplicate `ExampleRock`, move it, choose a nature PNG in Texture, set a uniform Scale, and adjust `Solid Radius` / `Blocks Attacks` in the Inspector. Position is the ground anchor; the script aligns nature textures automatically. Keep these Sprite2D objects as direct children with zero rotation. The procedural forest remains, and every peer reads the same landmark scene. Restart after map edits.
- Combat: solid scenery and intact base walls block melee, enemy sight, tower fire and Spitter projectiles. Projectiles test their complete movement segment and stop at the nearest obstacle/player. Destroyed towers remain physical wrecks. Harvested resource colliders disappear until dawn.
- Presentation: activity states are sent in snapshots; gathering/repair show a swinging tool, healing/reviving show a pulsing cross, and animation timing restarts when the state changes. Existing character PNGs are preserved. Camera follow uses a dead zone and exponential smoothing with whole-pixel positions.
- F3 toggles collision outlines for playtesting. Enemy separation and scenery queries use spatial buckets; the renderer culls against the actual camera view at every zoom level.
- Geometry checks: `godot --headless --path . -s tests/geometry_check.gd` (shared scenery, navigation, swept hits, blocked attacks and movement validation).
- `-- --autolobby` opens the crew lobby with 2 bots for UI previews (`--bots=N` to change). The menu and lobby also support `--shot` screenshots.
- `-- --autohost` hosts and starts at once with 2 bot teammates (`--bots=N` to change, 0 for none); add `--night` to jump to night 1. `-- --autojoin` joins 127.0.0.1 and starts.
- `--shot=<file.png>` saves a screenshot after `--shot-at=<seconds>` (default 4) and quits; `--at=<x>,<y>` puts you at that tile first.
- F9 (host, debug build) ends the current phase.
- Rules check: `godot --headless --path . -s tests/sim_check.gd`
