# Hold 'til Dawn (prototype)

Godot **4.7.2 .NET** with the **.NET 8 SDK** (or a compatible newer SDK). All game scripts, tests and art tools are C#. Rules from the GDD live in the `GameSim` class (server only); the `Main` class is network, input, drawing and HUD. Both are split into files per team member: see [docs/code-ownership.md](docs/code-ownership.md).

## Run
- Install the [Godot .NET editor and .NET SDK](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html), then run `dotnet build`. The regular Godot editor cannot run C# projects. Commands below use the .NET Godot executable as `godot`.
- **Play locally / host:** open the project in Godot, press Play, choose a role, click **Host**. Others click **Join by IP** with the host's IP (UDP 7777).
- **Dedicated server:** `godot --headless --path . -- --server`, then every player joins by IP.
- The lobby leader (first to join) picks the mode and presses **Start** (needs at least one Engineer).

## Controls
WASD move, mouse aim, LMB attack, E (hold) gather / repair door (Engineer) / revive, Q bandage, C workbench (inside the base by day, then number keys), RMB healing flask (Healer), F (hold 2 s, Engineer at night) enter or leave the base through a closed door, V skip-day vote, T (Engineer, 3-8 tiles in front of a door) build a tower, mouse wheel zoom (2, 3 or 4 screen pixels per art pixel), Esc leave.

Day-skip vote (FML-14): press V by day to vote or withdraw. Votes count once 180 s of the day have passed; the day then ends when at least 60% of the people vote (bots do not vote; one vote when playing solo). A day lasts at most 360 s. The 15-second dusk warning (BAL-05) follows, then night. For a quick test, the debug-only F9 key (host) ends the current phase.

## What is in
- 5 day/night cycles on an 11 x 7 tile base (GDD: 15 x 10) with doors A (west), B (north), C (east), no south door; players enter only through doors, and at night only the Engineer (hold F) or anyone through a broken door.
- Players, enemies and bots share scenery/resource/building colliders from `src/world/WorldGeometry.cs`. Movement is swept in small steps; the server validates player movement and acknowledges predicted inputs. Grass, pebbles, bushes and herbs stay walk-through. Enemies and bots navigate around obstacles with cached A* paths.
- Bot teammates (`src/ai/Bots.cs`, not in the GDD, for demos and solo tests): the lobby leader adds or removes them (max 5 players); a bot takes the missing role (Engineer, then Healer, Scout). They run on the server with the same rules as people: dodge boss slams, revive downed teammates, Healer flasks, fight, Engineer door repair, gather by day and craft near dusk, hold the attacked door at night. Only people count for the day-skip vote.
- Night waves spawn out of sight 25-35 tiles out in a band in front of each attacked door (GDD 4.3, BAL-34). First wave after 30 s, then every 90 s (45 s on night 5) (BAL-04); budget from FML-04. Enemy speeds follow BAL-29..32 (Grunt and Spitter 2 tiles/s, Wrecker 1.6, x1.08 per night, cap 3); a boss moves like a Grunt of its night. Boss on nights 2 and 4, night-5 helicopter extraction.
- Daytime roamers beyond 20 tiles from the base (chance grows with distance per FML-16, pairs, max 8); at dusk they walk to an attacked door.
- Enemy drops (BAL-41: Grunt 10%, Spitter 15%, Wrecker 20%, boss always; resource nodes 4 + 3n per type, BAL-17): walk over to pick up, gone after 30 s, not from inside the base.
- Defence towers (AGT-12, MEC-11, MEC-28, BAL-28): 300 HP, shoot the nearest enemy within 6 tiles at 8 DPS, light 6 tiles at night for everyone. One free tower (placed with T by an Engineer on day 1, or it appears in front of night 1's door at dusk); more cost 10 wood + 5 metal after the first boss; one per door. At night enemies walking into a tower's light attack it first (Wreckers x2, Spitters never); the Engineer repairs it like a door, a destroyed one only to 30% and only with no enemy in its light.

## 2.5D look (branch `proto-2.5d`)
- The rules are still flat 2D (`GameSim` unchanged); only `Main` draws differently: everything with height (trees, rocks, walls, enemies, players) is sorted by the y of its foot, walls have a top and a front face, and bodies stand above their shadows. Scenery and resource nodes stay opaque even when a character stands behind them; depleted nodes are smaller until dawn.
- Pixel-art world (GDD style guide): everything is drawn without smoothing, and the camera zoom keeps one art pixel on a whole number of screen pixels.
- Fonts in `fonts/` (SIL OFL 1.1, licence files next to them): **Pixelify Sans** (Stefie Justprince, weight 560) for all text, **Jersey 10** (Sarah Cadigan-Fried) for titles, the timer, big numbers and banners.
- Base props in `art/props/` (lighthouse = beacon, workbench, tower, wrecked tower, landing pad) are pixel art drawn by `tools/PixelProps.cs`; doors have lamp pillars that light up at dusk.
- Nature sprites in `art/nature/` are pixel art (32 px per tile, banded colours, 1 px outline) rendered from Quaternius 3D models (CC0) by `tools/BakeNature.cs`; see `art/nature/CREDITS.txt`.
- Players and enemies in `art/chars/` are 32x32 pixel art (Wrecker 40x40) of the GDD characters (CHR-01..03 Scout, Healer, Engineer; CHR-05..07 Grunt, Spitter, Wrecker), drawn by `tools/PixelChars.cs` (pixel maps, GDD palette, outline, 4 directions, idle + walk); see `art/chars/CREDITS.txt`. The PNGs can be repainted by hand. A boss is a big tinted Wrecker for now; a coloured ring at a player's feet shows the role.

## Dev
- C# solution: `HoldTilDawn.sln`; build with `dotnet build`. Run all regression checks with `powershell -File tools/Test.ps1 -Godot <path-to-Godot-.NET-console.exe>` (includes a local multiplayer check on UDP 17777). The full simulation suite advances many minutes of game time and can take several minutes.
- Migration details and file mapping: [docs/csharp-migration.md](docs/csharp-migration.md).
- Settings (one file per owner, edit in Godot's Inspector, restart the game after a change): `config/gameplay/gameplay.tres` (vote delay, dusk), `config/ai/ai.tres` (enemy speed, wave timing, spawn/roamer/drop multipliers), `config/world/world.tres` (resource nodes), `config/display/display.tres` (camera follow). Defaults follow the GDD (180 s minimum before the skip vote, 15 s dusk, waves at 30 s then every 90 s / 45 s, speed and spawn/roamer/drop/node multipliers at 1); raise the multipliers or lower `DayMin` only for local playtests.
- Editable landmarks: open `maps/island.tscn` in Godot's 2D editor. Duplicate `ExampleRock`, move it, choose a nature PNG in Texture, set a uniform Scale, and adjust `Solid Radius` / `Blocks Attacks` in the Inspector. Position is the ground anchor; the script aligns nature textures automatically. Keep these Sprite2D objects as direct children with zero rotation. The procedural forest remains, and every peer reads the same landmark scene. Restart after map edits.
- Combat: solid scenery and intact base walls block melee, enemy sight, tower fire and Spitter projectiles. Projectiles test their complete movement segment and stop at the nearest obstacle/player. Destroyed towers remain physical wrecks. Harvested resource colliders disappear until dawn.
- Presentation: activity states are sent in snapshots; gathering/repair show a swinging tool, healing/reviving show a pulsing cross, and animation timing restarts when the state changes. Existing character PNGs are preserved. Camera follow uses a dead zone and exponential smoothing with whole-pixel positions.
- F3 toggles collision outlines for playtesting. Enemy separation and scenery queries use spatial buckets; the renderer culls against the actual camera view at every zoom level.
- Geometry checks: `godot --headless --path . tests/GeometryCheck.tscn` (shared scenery, navigation, swept hits, blocked attacks and movement validation).
- `-- --autolobby` opens the crew lobby with 2 bots for UI previews (`--bots=N` to change). The menu and lobby also support `--shot` screenshots.
- `-- --autohost` hosts and starts at once with 2 bot teammates (`--bots=N` to change, 0 for none); add `--night` to jump to night 1. `-- --autojoin` joins 127.0.0.1 and starts.
- `--shot=<file.png>` saves a screenshot after `--shot-at=<seconds>` (default 4) and quits; `--at=<x>,<y>` puts you at that tile first.
- F9 (host, debug build) ends the current phase.
- Rules check: `godot --headless --path . tests/SimCheck.tscn`

## Art tools (C#)
- Props: `godot --headless --path . tools/PixelProps.tscn`
- Characters: `godot --headless --path . tools/PixelChars.tscn`
- Both generators accept `-- --out=<directory>` to preview output without replacing the shipped art.
- Nature baking: place the source glTF models in `models/`, then run `godot --path . tools/BakeNature.tscn -- --out=<directory>`. This needs a rendering device; see the script for model credits and rendering settings.
