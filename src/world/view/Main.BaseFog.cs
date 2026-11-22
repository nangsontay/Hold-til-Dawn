using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. From outside the base you cannot see into it: a roof covers the floor inside the walls
// and nothing standing inside is drawn (players, enemies, drops, the workbench, hits, boss markers).
// The walls, doors, door towers and the lighthouse (the beacon, rising through the roof) still show.
// Step inside and everything shows again; the roof fades so walking through a door does not flash.
public partial class Main : Node2D
{
    // 0: inside is visible, 1: hidden
    float BaseFog;
    float BaseFogClock;
    // how fast the fog comes and goes (full change in 1 / this seconds)
    const float BASE_FOG_SPEED = 4.0f;
    // what is drawn over the floor inside the walls
    static readonly Color BASE_FOG_COL = new Color(0.07f, 0.08f, 0.1f);

    // call once per frame before drawing the world
    public void TickBaseFog()
    {
        float Now = (float)Clock;
        float Dt = Mathf.Clamp((Now - BaseFogClock), 0.0f, 0.1f);
        BaseFogClock = Now;
        bool Out = !Gd.Truth(GameSim.InsideBase(LocalPos));
        BaseFog = Mathf.MoveToward(BaseFog, (Out ? 1.0f : 0.0f), (Dt * BASE_FOG_SPEED));
    }

    // whether something at this spot is hidden from you
    public bool HiddenInBase(Vector2 Pos)
    {
        return ((BaseFog > 0.5f) && Gd.Truth(GameSim.InsideBase(Pos)));
    }

    // From outside, the base has a roof: flat riveted slabs in the walls' concrete laid between the wall tops (lifted by the wall height,
    // like the walls' top faces), rusting at the seams, with vent grates. The lighthouse rises through the
    // middle of it and is drawn on top (a depth item). Under it, the floor goes dark, which is what shows through an
    // open door. Drawn on the ground layer, so the walls and everything outside draw over it.
    float RoofAlpha;

    // the roof is about 1500 rects a frame from outside: its colours are parsed once, not on every rect
    static readonly System.Collections.Generic.Dictionary<string, Color> RoofHex = new();
    static readonly Color ROOF_SLAB = new Color("B0A58B"), ROOF_SLAB_LIT = new Color("C6BBA1");
    static readonly Vector2[] RIVETS = { new Vector2(2, 2), new Vector2(32 - 4, 2), new Vector2(2, 25 - 4), new Vector2(32 - 4, 25 - 4), new Vector2((32 / 2) - 1, 2), new Vector2((32 / 2) - 1, 25 - 4) };

    Color Rf(string Hex, float A = 1.0f)
    {
        if (!RoofHex.TryGetValue(Hex, out var C))
        {
            C = new Color(Hex);
            RoofHex[Hex] = C;
        }
        C.A = (A * RoofAlpha);
        return C;
    }

    Color Rf(Color C)
    {
        C.A *= RoofAlpha;
        return C;
    }

    public static Rect2 RoofRect()
    {
        Vector2 H = (Vector2)GameSim.HALF;
        float In = (float)GameSim.WALL_IN;
        float Lift = (float)WALL_H;
        return new Rect2((-H.X + In), ((-H.Y + In) - Lift), (2 * (H.X - In)), (2 * (H.Y - In)));
    }

    public void DrawBaseFog()
    {
        if (BaseFog <= 0.0f)
        {
            return;
        }
        RoofAlpha = Mathf.SmoothStep(0.0f, 1.0f, BaseFog);
        Vector2 H = (Vector2)GameSim.HALF;
        // the dark inside, seen through the doorways
        var Dark = BASE_FOG_COL;
        Dark.A = RoofAlpha;
        DrawRect(new Rect2(-H, (H * 2)), Dark);
        Rect2 R = RoofRect();
        float X0 = R.Position.X;
        float Y0 = R.Position.Y;
        // riveted concrete slabs in the walls' cream, each a shade apart, rust bleeding from some seams (the rim around them is the walls'
        // concrete coping, src/world/view/Main.BaseLook.cs)
        DrawRect(R, Rf("8A8069"));
        const int Pw = 32;
        const int Ph = 25;
        for (float Y = Y0; Y < R.End.Y; Y += Ph)
        {
            for (float X = X0; X < R.End.X; X += Pw)
            {
                var P = new Rect2((X + 1), (Y + 1), (Pw - 1), (Ph - 1)).Intersection(R);
                if (!P.HasArea())
                {
                    continue;
                }
                int Hs = Hash(Mathf.RoundToInt(X), Mathf.RoundToInt(Y) + 999);
                DrawRect(P, Rf(ROOF_SLAB.Lerp(ROOF_SLAB_LIT, ((Hs % 100) / 100.0f))));
                DrawRect(new Rect2(P.Position, new Vector2(P.Size.X, 1)), Rf("DDD3BB", 0.8f));
                DrawRect(new Rect2(P.Position, new Vector2(1, P.Size.Y)), Rf("DDD3BB", 0.5f));
                DrawRect(new Rect2(P.Position.X, (P.End.Y - 1), P.Size.X, 1), Rf("857B66", 0.6f));
                // rivets at the corners and halfway along the long edges
                foreach (var Rv in RIVETS)
                {
                    Vector2 At = (P.Position + Rv);
                    if (P.HasPoint(At))
                    {
                        DrawRect(new Rect2(At, Vector2.One), Rf("E8E0CB"));
                        DrawRect(new Rect2((At + new Vector2(0, 1)), Vector2.One), Rf("7A705C", 0.7f));
                    }
                }
                // weathering: rust blooms and streaks, a few pale patches
                if ((Hs % 3) == 0)
                {
                    int N = (4 + ((Hs >> 3) % 6));
                    Vector2 C = (P.Position + new Vector2((4 + ((Hs >> 5) % (Pw - 10))), (4 + ((Hs >> 9) % (Ph - 10)))));
                    for (int K = 0; K < N; K++)
                    {
                        int Hk = Hash(Hs, K);
                        Vector2 D = new Vector2(((Hk % 7) - 3), (((Hk >> 4) % 5) - 2));
                        var Blot = new Rect2((C + D), new Vector2((1 + (Hk >> 8) % 3), (1 + (Hk >> 11) % 2))).Intersection(P);
                        if (Blot.HasArea())
                        {
                            DrawRect(Blot, Rf((((Hk >> 13) % 3) == 0) ? "74401F" : "9C5B33", 0.75f));
                        }
                    }
                    // a run toward the south edge
                    var Run = new Rect2(C, new Vector2(1, (3 + (Hs >> 14) % 8))).Intersection(P);
                    if (Run.HasArea())
                    {
                        DrawRect(Run, Rf("9C5B33", 0.5f));
                    }
                }
                else if ((Hs % 7) == 1)
                {
                    DrawRect(new Rect2((P.Position + new Vector2(5, 6)), new Vector2(10, 5)).Intersection(P), Rf("E2D9C3", 0.4f));
                }
            }
        }
        // vent grates in concrete frames
        foreach (var V in new[] { new Vector2(-118, 28), new Vector2(84, -22), new Vector2(-104, -96), new Vector2(102, 34) })
        {
            this.RoofGrate(new Rect2(V, new Vector2(26, 12)));
        }
        // the lighthouse's shadow across the roof by day (the sun is low in the south-east); it comes up through
        // the roof, which is lifted by the wall height
        Vector2 Foot = ((Vector2)LIGHTHOUSE - new Vector2(0, (float)WALL_H));
        if (Gd.Equal(Gd.Get(Snap, "ph", ""), "day"))
        {
            var Sh = new[] { (Foot + new Vector2(-16, 4)), (Foot + new Vector2(16, 0)), (Foot + new Vector2(-104, -98)), (Foot + new Vector2(-128, -84)) };
            for (int K = 0; K < Sh.Length; K++)
            {
                Sh[K] = Sh[K].Clamp(R.Position, R.End);
            }
            DrawColoredPolygon(Sh, Rf("000000", 0.28f));
        }
        // where the lighthouse comes up through the roof: a rusty steel collar
        this.RoofEllipse(Foot + new Vector2(4, 2), 34, 9, Rf("000000", 0.3f));
        this.RoofEllipse(Foot, 30, 8, Rf("6B5444"));
        this.RoofEllipse(Foot, 30, 8, Rf("2E353D"), false);
        this.RoofEllipse(Foot, 26, 6, Rf("8A6A4E"), false);
    }

    // a vent grate: concrete frame, dark slats
    void RoofGrate(Rect2 G)
    {
        DrawRect(new Rect2((G.Position + new Vector2(2, 3)), G.Size), Rf("000000", 0.3f));
        DrawRect(G.Grow(2), Rf("2E353D"));
        DrawRect(G.Grow(1), Rf("C8BDA3"));
        DrawRect(G, Rf("23282E"));
        for (float X = (G.Position.X + 2); X < (G.End.X - 1); X += 3.0f)
        {
            DrawRect(new Rect2(X, (G.Position.Y + 1), 1, (G.Size.Y - 2)), Rf("8A8069"));
        }
        DrawRect(new Rect2(G.Position.X, G.Position.Y, G.Size.X, 1), Rf("000000", 0.4f));
    }

    void RoofEllipse(Vector2 C, float Rx, float Ry, Color Col, bool Filled = true)
    {
        var Pts = new Vector2[24];
        for (int K = 0; K < 24; K++)
        {
            float A = ((K * Mathf.Tau) / 24);
            Pts[K] = (C + new Vector2((Mathf.Cos(A) * Rx), (Mathf.Sin(A) * Ry)));
        }
        if (Filled)
        {
            DrawColoredPolygon(Pts, Col);
        }
        else
        {
            var Ring = new Vector2[25];
            Pts.CopyTo(Ring, 0);
            Ring[24] = Pts[0];
            DrawPolyline(Ring, Col, 1.0f);
        }
    }

    // From outside only the part of the lighthouse above the roof shows: the sprite is cut where it comes
    // up through the roof's collar (DrawBaseFog). Inside, or while the roof is fading out, it is drawn whole.
    public void DrawLighthouse(Vector2 Pos, Color Mod)
    {
        dynamic P = Gd.Get(Prop, "lighthouse");
        if ((P == null))
        {
            return;
        }
        if (BaseFog <= 0.5f)
        {
            this.DrawProp("lighthouse", Pos, Mod);
            return;
        }
        var Tex = (Texture2D)Gd.Unwrap(Gd.At(P, 0));
        float Sc = (float)LIGHTHOUSE_SCALE;
        Vector2 Anchor = (Vector2)Gd.At(P, 1);
        Vector2 Size = Tex.GetSize();
        // texture rows above the roof line (the roof sits WALL_H above the ground)
        float Keep = Mathf.Clamp((Anchor.Y - ((float)WALL_H / Sc)), 0.0f, Size.Y);
        var Src = new Rect2(0, 0, Size.X, Keep);
        var Dst = new Rect2((Pos - (Anchor * Sc)).Round(), new Vector2(Size.X, Keep) * Sc);
        DrawTextureRectRegion(Tex, Dst, Src, Mod);
    }

    // whether a depth item (see Main._Draw) stands inside the base and is hidden from you
    public bool HiddenItem(dynamic It)
    {
        if (BaseFog <= 0.5f)
        {
            return false;
        }
        int Kind = Gd.Int(Gd.At(It, 1));
        Vector2 Pos;
        switch (Kind)
        {
            // scatter and nodes, enemies, props
            case 0:
            case 2:
                Pos = (Vector2)Gd.At(It, 3);
                break;
            case 6:
                // the lighthouse is seen over the walls
                if (Gd.Str(Gd.At(It, 2)) == "lighthouse")
                {
                    return false;
                }
                Pos = (Vector2)Gd.At(It, 3);
                break;
            // players
            case 3:
                Pos = (Vector2)Gd.At(It, 4);
                break;
            // projectiles, drops, arrows
            case 4:
            case 5:
            case 11:
                Pos = (Vector2)Gd.At(Gd.At(It, 2), 0);
                break;
            case 12:
                Pos = Corpses[Gd.Int(Gd.At(It, 2))].Foot;
                break;
            // walls, broken doors, pillars, towers and built walls are never hidden
            default:
                return false;
        }
        return this.HiddenInBase(Pos);
    }
}
