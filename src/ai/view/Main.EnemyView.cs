using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. How enemies and bosses look on the client: sprite choice, tint, attack and death
// frames, health bars, boss bar.
// Sprite sheets in art/chars/ belong to Mai Thien Ngon; this file only maps enemy state to them.
public partial class Main : Node2D
{
    public static readonly dynamic ENEMY_SKIN = new GArray("grunt", "spitter", "wrecker", "wrecker");
    public static readonly dynamic ENEMY_TINT = new GArray(Colors.White, Colors.White, Colors.White, Gd.MakeColor(1.2f, 0.85f, 1.25f));
    // sprite sheet per boss archetype, in GameSim.BOSS_KINDS order (Juggernaut .. Ironhide)
    public static readonly GArray BOSS_SKIN = new GArray("boss_juggernaut", "boss_broodmother", "boss_bio_cannon", "boss_burrower", "boss_gatebreaker", "boss_phantom", "boss_ironhide");
    // boss moves with a wind-up: it holds the first attack frame (arms up) until the move lands
    static readonly HashSet<string> BOSS_WIND = new() { "glow", "charge_wind", "blow", "slam", "ambush" };
    // a dead enemy plays its "down" frames where it fell, lies there, then fades (seconds)
    const float CORPSE_TIME = 2.4f, CORPSE_FADE = 0.6f;
    // bosses: the 48 px sheets drawn at 2x (about 3 tiles tall, three times a player), a whole number so the
    // pixels stay even
    const float BOSS_DRAW_SCALE = 2.0f;

    struct Fallen
    {
        public string Key, Skin;
        public float S, At;
        public Color Mod;
        public Vector2 Foot;
    }

    // what each enemy on screen was last drawn as (by id), so it can fall over where it died
    readonly Dictionary<string, (dynamic Id, Fallen Look)> EnSeen = new();
    readonly List<Fallen> Corpses = new();
    // bosses holding a wind-up, and when their move landed
    readonly HashSet<string> Winding = new();
    readonly Dictionary<string, float> StrikeAt = new();

    // Enemies drawn last frame that are gone from the snapshot died (or the night ended under them): they fall.
    public void NoteFallen(dynamic Shown)
    {
        foreach (var Key in new List<string>(EnSeen.Keys))
        {
            var (Id, Look) = EnSeen[Key];
            if (!Gd.Truth(Gd.Has(Shown, Id)))
            {
                Look.At = (float)Clock;
                Corpses.Add(Look);
                EnSeen.Remove(Key);
            }
        }
    }

    // out of a match: nothing carries over to the next one
    public void ForgetFallen()
    {
        EnSeen.Clear();
        Corpses.Clear();
        Winding.Clear();
        StrikeAt.Clear();
    }

    // depth items [foot y, 12, index] for the corpses still showing
    public void CorpseItems(dynamic Items)
    {
        Corpses.RemoveAll((C) => (((float)Clock - C.At) > CORPSE_TIME));
        for (int I = 0; I < Corpses.Count; I++)
        {
            // under anyone standing on the same spot
            Gd.Append(Items, new GArray((Corpses[I].Foot.Y - 1.0f), 12, I));
        }
    }

    public void DrawCorpse(int I)
    {
        var C = Corpses[I];
        float T = ((float)Clock - C.At);
        Color Mod = C.Mod;
        Mod.A *= Mathf.Clamp(((CORPSE_TIME - T) / CORPSE_FADE), 0.0f, 1.0f);
        this.Char(C.Key, C.Skin, C.Foot, null, C.S, Mod, false, "idle", "down", T);
    }

    // Ground markers of boss moves (BAL-43): circle (slam, surfacing), line (charge) and arc (ambush).
    // The inner fill grows as the hit comes closer.
    public void DrawTelegraphs()
    {
        foreach (dynamic Tl in Gd.Iter(Gd.Get(Snap, "tele", new GArray())))
        {
            Vector2 Pos = (Vector2)(Gd.At(Tl, 0));
            float R = Gd.Float(Gd.At(Tl, 1));
            float Left = Gd.Float(Gd.At(Tl, 2));
            string Kind = ((Gd.Size(Tl) > 3) ? Gd.Str(Gd.At(Tl, 3)) : "circle");
            if ((Kind == "line"))
            {
                Vector2 End = (Vector2)(Gd.At(Tl, 4));
                Vector2 Side = ((End - Pos).Normalized().Orthogonal() * R);
                dynamic Quad = new Vector2[] { (Pos + Side), (End + Side), (End - Side), (Pos - Side) };
                Gd.Native(this, "draw_colored_polygon", Quad, Gd.MakeColor(1.0f, 0.1f, 0.3f, 0.18f));
                Vector2 Mid = Pos.Lerp(End, (1.0f - Left));
                Gd.Native(this, "draw_colored_polygon", new Vector2[] { (Pos + Side), (Mid + Side), (Mid - Side), (Pos - Side) }, Gd.MakeColor(1.0f, 0.15f, 0.3f, 0.3f));
                Gd.Native(this, "draw_polyline", new Vector2[] { (Pos + Side), (End + Side), (End - Side), (Pos - Side), (Pos + Side) }, NEON_RED, 2);
                continue;
            }
            if ((Kind == "arc"))
            {
                float Ang = Gd.Float(Gd.At(Tl, 5));
                float Half = (Gd.Float(Gd.At(Tl, 6)) / 2);
                Gd.Native(this, "draw_colored_polygon", Main.ArcFan(Pos, R, Ang, Half), Gd.MakeColor(1.0f, 0.1f, 0.3f, 0.18f));
                Gd.Native(this, "draw_colored_polygon", Main.ArcFan(Pos, (R * (1.0f - Left)), Ang, Half), Gd.MakeColor(1.0f, 0.15f, 0.3f, 0.35f));
                Gd.Native(this, "draw_arc", Pos, R, (Ang - Half), (Ang + Half), 24, NEON_RED, 3);
                continue;
            }
            Gd.Native(this, "draw_circle", Pos, R, Gd.MakeColor(1.0f, 0.1f, 0.3f, 0.16f));
            Gd.Native(this, "draw_circle", Pos, (R * (1.0f - Left)), Gd.MakeColor(1.0f, 0.15f, 0.3f, 0.35f));
            Gd.Native(this, "draw_arc", Pos, R, 0, Mathf.Tau, 48, NEON_RED, 3);
        }
    }

    public static Vector2[] ArcFan(Vector2 C, float R, float Ang, float Half)
    {
        var Pts = new Vector2[18];
        Pts[0] = C;
        for (int I = 0; I < 17; I++)
        {
            Pts[(I + 1)] = (C + (Vector2.FromAngle(((Ang - Half) + (((2 * Half) * I) / 16.0f))) * Mathf.Max(R, 1.0f)));
        }
        return Pts;
    }

    public void DrawEnemy(dynamic E, dynamic Foot)
    {
        int Ty = Gd.Int(Gd.At(E, 1));
        string Look = ((Gd.Size(E) > 7) ? Gd.Str(Gd.At(E, 7)) : "");
        // each boss archetype has its own sheet (art/chars/boss_*.png from tools/PixelBosses.cs)
        string Skin = ((Ty == GameSim.BOSS) ? (string)(Gd.At(BOSS_SKIN, Gd.Int(Gd.At(E, 6)))) : (string)(Gd.At(ENEMY_SKIN, Ty)));
        string Key = Gd.Format("e%d", Gd.At(E, 0));
        if ((Look == "cloak"))
        {
            EnSeen.Remove(Key);
            // the Phantom is invisible while cloaked; only a faint shimmer gives it away up close
            if ((LocalPos.DistanceTo(Foot) < (2.5f * T)))
            {
                Gd.Native(this, "draw_arc", (Foot - Gd.MakeVector2(0, 16)), (Gd.At(E, 5) * 0.8f), 0, Mathf.Tau, 24, Gd.MakeColor(0.8f, 0.7f, 1.0f, 0.18f), 2);
            }
            return;
        }
        if (((Look == "burrow") || (Look == "surface")))
        {
            // the Burrower underground: a moving dirt mound
            EnSeen.Remove(Key);
            this.Shadow(Foot, (Gd.At(E, 5) * 0.9f));
            Gd.Native(this, "draw_circle", (Foot - Gd.MakeVector2(0, 3)), (Gd.At(E, 5) * 0.6f), Gd.MakeColor("5A4632"));
            Gd.Native(this, "draw_circle", (Foot - Gd.MakeVector2(0, 6)), (Gd.At(E, 5) * 0.35f), Gd.MakeColor("7A6046"));
            return;
        }
        // the character's own size (the sheet cell has room around it for its attack)
        float Size = Gd.Float(Gd.Get(Gd.At(Cinfo.Chars, Skin), "body", Gd.At(Cinfo.Chars, Skin).Size));
        // GDD size in tiles over the sprite's own size: the 40 px Wrecker is drawn 1:1. A boss is drawn much bigger
        // than its body (BOSS_DRAW_SCALE), so it towers over the players but still fits through a door.
        float S = ((Ty == GameSim.BOSS) ? BOSS_DRAW_SCALE : Gd.Float((((Gd.At(E, 5) / (0.5f * T)) * 32.0f) / Size)));
        Color Mod = (Color)(Gd.At(ENEMY_TINT, Ty));
        if ((Ty == GameSim.BOSS))
        {
            Mod = Colors.White;
            // wind-ups glow; recovery, stagger and climb-out (the weak windows) look dim
            if (((Look == "glow") || (Look == "charge_wind") || (Look == "blow") || (Look == "slam") || (Look == "ambush")))
            {
                Mod = Mod.Lerp(Gd.MakeColor(2.2f, 1.6f, 0.6f), (0.5f + (0.5f * Gd.Sin((Clock * 18.0f)))));
            }
            else if (((Look == "recover") || (Look == "stagger") || (Look == "climb")))
            {
                Mod = Mod.Darkened(0.35f);
            }
            else if ((Look == "shown"))
            {
                Mod = Mod.Lerp(Gd.MakeColor(0.8f, 0.7f, 1.0f), 0.4f);
            }
        }
        EnSeen[Key] = (Gd.At(E, 0), new Fallen { Key = Key, Skin = Skin, S = S, Mod = (Color)(Gd.At(ENEMY_TINT, Ty)), Foot = (Vector2)Foot });
        if (Gd.Truth(Gd.Has(Flash, Gd.At(E, 0))))
        {
            Mod = Gd.MakeColor(2.0f, 2.0f, 2.0f);
        }
        // attack frames: on each hit or spit (snapshot field 9); a boss holds the wind-up frame through its
        // wind-up and strikes when the move lands
        string Play = null;
        float PlayT = 0.0f;
        float Now = (float)Clock;
        float Start = this.SwingStart(Key, ((Gd.Size(E) > 9) && Gd.Truth(Gd.At(E, 9))));
        if (((Ty == GameSim.BOSS) && BOSS_WIND.Contains(Look)))
        {
            Winding.Add(Key);
            Play = "attack";
        }
        else
        {
            if (Winding.Remove(Key))
            {
                // straight to the slam (frame 1 of 4 at 10 frames/s)
                StrikeAt[Key] = (Now - 0.1f);
            }
            float From = Mathf.Max(Start, (StrikeAt.TryGetValue(Key, out float Sa) ? Sa : -1.0f));
            if (((From >= 0) && ((Now - From) < this.AnimLength(Skin, "attack"))))
            {
                Play = "attack";
                PlayT = (Now - From);
            }
        }
        this.Char(Key, Skin, Foot, null, S, Mod, false, "idle", Play, PlayT);
        if (((Gd.At(E, 3) < Gd.At(E, 4)) && (Ty != GameSim.BOSS)))
        {
            this.Bar((Foot - Gd.MakeVector2(0, (((Size * 0.7f) * S) + 4))), (Gd.At(E, 5) * 2.2f), (Gd.At(E, 3) / Gd.At(E, 4)), NEON_RED);
        }
    }

    // --- HUD drawing (screen space) -----------------------------------------
    public void DrawBossBar(dynamic C, dynamic Cx)
    {
        foreach (dynamic E in Gd.Iter(Snap.En))
        {
            if ((Gd.At(E, 1) == GameSim.BOSS))
            {
                dynamic Br = Gd.MakeRect2((Cx - 230), 66, 460, 10);
                this.Text(C, Gd.MakeVector2((Cx - 230), 62), GameSim.BossTitle(Gd.At(E, 6)).ToUpper(), 12, NEON_PINK, 1, 460, true);
                C.DrawRect(Br.Grow(2), INK);
                C.DrawRect(Gd.MakeRect2(Br.Position, Gd.MakeVector2(((Br.Size.X * Gd.At(E, 3)) / Gd.At(E, 4)), Br.Size.Y)), NEON_PINK);
                foreach (dynamic X in Gd.Iter(new GArray((Br.Position.X - 6), (Br.End.X + 6))))
                {
                    this.Diamond(C, Gd.MakeVector2(X, Br.GetCenter().Y), 8, NEON_PINK);
                }
            }
        }
    }
}
