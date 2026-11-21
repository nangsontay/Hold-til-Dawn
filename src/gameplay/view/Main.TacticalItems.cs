using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Pham Minh Hieu. How the tactical items look in use:
//   flasks   - fly from the thrower in an arc, then splash and leave their pulsing area
//   vine trap - sprouts out of the ground, then waits with its vines curling
//   blast    - the explosive arrow's burst: a flash, a ring and flying sparks
//   haste    - streaks behind a hasted runner and sparks round their feet
//   long spear / stun maul - drawn moving over the spear / hammer frames: a thrust out and back, an overhead
//   blow that lands with a shock ring and stars
public partial class Main
{
    // when this client first saw each tactical area (by kind and spot), for its opening animation
    readonly Dictionary<(string, Vector2), float> AreaSeen = new();
    // seconds a thrown flask flies before it splashes
    const float FLASK_FLIGHT = 0.3f;

    public void DrawTacticalAreas()
    {
        float Now = (float)Clock;
        var Live = new HashSet<(string, Vector2)>();
        foreach (dynamic A in Gd.Iter(Gd.Get(Snap, "tactical", new GArray())))
        {
            string Kind = Gd.Str(Gd.At(A, 0)); Vector2 At = (Vector2)Gd.At(A, 1);
            var Key = (Kind, At);
            Live.Add(Key);
            if (!AreaSeen.TryGetValue(Key, out float Seen))
            {
                Seen = Now;
                AreaSeen[Key] = Seen;
            }
            float Age = Now - Seen;
            if (Kind == "trap")
            {
                DrawTrap(At, Age);
                continue;
            }
            Color Tint = new(Kind == "heal" ? "6CE4BA" : Kind == "poison" ? "A1C64A" : "FF9B45");
            if (Kind == "blast")
            {
                DrawBlast(At, Age, Tint);
                continue;
            }
            // a flask: in the air first (only for one thrown just now: an old area seen late has no flight)
            float Left = Gd.Float(Gd.At(A, 2));
            bool Fresh = Left > 5f - FLASK_FLIGHT - 0.1f;
            if (Fresh && Age < FLASK_FLIGHT)
            {
                Vector2 From = ThrowerPos(Gd.Size(A) > 3 ? Gd.At(A, 3) : null, At);
                DrawFlaskFlight(From, At, Age / FLASK_FLIGHT, Kind);
                continue;
            }
            float Open = Fresh ? Mathf.Clamp((Age - FLASK_FLIGHT) / 0.25f, 0f, 1f) : 1f;
            float R = 1.8f * T * Mathf.Ease(Open, 0.4f);
            DrawCircle(At, R, new Color(Tint, 0.13f));
            DrawArc(At, R, 0, Mathf.Tau, 32, new Color(Tint, 0.8f), 2f);
            if (Fresh && Open < 1f)
            {
                // the splash: glass and drops thrown out
                for (int I = 0; I < 8; I++)
                {
                    Vector2 D = Vector2.FromAngle(I * Mathf.Tau / 8 + 0.3f);
                    Vector2 P = At + D * (6 + 26 * Open) - new Vector2(0, 10 * Mathf.Sin(Open * Mathf.Pi));
                    DrawRect(new Rect2(P.Round(), new Vector2(2, 2)), new Color(Tint.Lightened(0.3f), 1 - Open));
                }
            }
            for (int I = 0; I < 5; I++)
            {
                Vector2 P = At + Vector2.FromAngle(I * Mathf.Tau / 5 + Now * 0.25f) * (R * 0.55f);
                P.Y -= 3f + 3f * MathF.Sin(Now * 3 + I);
                if (Kind == "heal") { DrawLine(P - Vector2.Right * 3, P + Vector2.Right * 3, Tint, 2f); DrawLine(P - Vector2.Down * 3, P + Vector2.Down * 3, Tint, 2f); }
                else DrawRect(new Rect2(P.Round(), new Vector2(3, 3)), Tint);
            }
        }
        // forget areas that are gone
        if (AreaSeen.Count > Live.Count)
        {
            var Gone = new List<(string, Vector2)>();
            foreach (var K in AreaSeen.Keys) if (!Live.Contains(K)) Gone.Add(K);
            foreach (var K in Gone) AreaSeen.Remove(K);
        }
    }

    Vector2 ThrowerPos(dynamic Owner, Vector2 Fallback)
    {
        if (Owner == null) return Fallback;
        if (Gd.Equal(Owner, MyId)) return (Vector2)LocalPos;
        dynamic P = Gd.Get(Snap.Ps, Owner, null);
        return P == null ? Fallback : (Vector2)Gd.Get(ShownP, Owner, Gd.At(P, 2));
    }

    // a flask tumbling along a throw arc
    void DrawFlaskFlight(Vector2 From, Vector2 To, float K, string Kind)
    {
        Vector2 Ground = From.Lerp(To, K);
        float Height = 18 + 34 * Mathf.Sin(K * Mathf.Pi);
        this.Shadow(Ground, 4);
        Vector2 At = Ground - new Vector2(0, Height);
        DrawSetTransform(At, K * 9f, Vector2.One);
        DrawItemIcon(this, Kind == "heal" ? "healing_flask" : "poison_flask", Vector2.Zero, 1f);
        DrawSetTransform(Vector2.Zero);
    }

    // a vine trap sprouting, then waiting
    void DrawTrap(Vector2 At, float Age)
    {
        float Grow = Mathf.Clamp(Age / 0.35f, 0f, 1f);
        float S = Mathf.Ease(Grow, 0.4f) * (1f + 0.25f * Mathf.Sin(Grow * Mathf.Pi));
        var Green = new Color("98BE56");
        DrawArc(At, 17f * S, 0, Mathf.Tau, 16, Green, 2f);
        // curling vines
        for (int I = 0; I < 4; I++)
        {
            float A = I * Mathf.Tau / 4 + (float)Clock * 0.6f;
            Vector2 Root = At + Vector2.FromAngle(A) * 12f * S;
            Vector2 Tip = Root + new Vector2(0, -6f * S) + Vector2.FromAngle(A + 1.2f) * 3f * Mathf.Sin((float)Clock * 3 + I);
            DrawLine(Root, Tip, new Color("5E8A34"), 2f);
        }
        if (S > 0.05f)
        {
            DrawSetTransform(At, 0, Vector2.One * S);
            DrawItemIcon(this, "vine_trap", Vector2.Zero, 1f);
            DrawSetTransform(Vector2.Zero);
        }
        if (Grow < 1f)
        {
            // soil kicked up as it bursts out
            for (int I = 0; I < 6; I++)
            {
                Vector2 P = At + Vector2.FromAngle(I * Mathf.Tau / 6) * (8 + 14 * Grow) - new Vector2(0, 8 * Mathf.Sin(Grow * Mathf.Pi));
                DrawRect(new Rect2(P.Round(), new Vector2(2, 2)), new Color("7A6046", 1 - Grow));
            }
        }
    }

    // the explosive arrow's burst over its short life
    void DrawBlast(Vector2 At, float Age, Color Tint)
    {
        float K = Mathf.Clamp(Age / 0.35f, 0f, 1f);
        float R = 1.5f * T;
        DrawCircle(At, R * (0.3f + 0.7f * K), new Color(2.2f, 1.5f, 0.6f, 0.5f * (1 - K)));
        DrawArc(At, R * (0.4f + 0.8f * K), 0, Mathf.Tau, 32, new Color(Tint, 1 - K), 3f);
        for (int I = 0; I < 10; I++)
        {
            Vector2 D = Vector2.FromAngle(I * Mathf.Tau / 10 + 0.4f);
            Vector2 P = At + D * R * (0.2f + 0.9f * K) - new Vector2(0, 14 * Mathf.Sin(K * Mathf.Pi));
            DrawRect(new Rect2(P.Round(), new Vector2(3, 3)), new Color(1.8f, 1.2f, 0.5f, 1 - K));
        }
    }

    // haste: streaks behind a runner and sparks at the feet
    void DrawHaste(dynamic Id, dynamic P, Vector2 Foot)
    {
        if (Gd.Size(P) <= 30 || !Gd.Truth(Gd.At(P, 30))) return;
        dynamic M = Gd.Get(Cmove, Id, null);
        float Now = (float)Clock;
        bool Moving = M != null && (Now - Gd.Float(Gd.At(M, 1))) < 0.15f;
        var Gold = new Color("F0C76B");
        if (Moving)
        {
            Vector2 Back = -Vector2.FromAngle(Gd.Float(Gd.At(M, 2)));
            for (int I = 0; I < 3; I++)
            {
                Vector2 S = Foot + new Vector2(0, -8 - I * 9) + Back * 10;
                DrawLine(S, S + Back * (14 + 6 * Mathf.Sin(Now * 20 + I)), new Color(Gold, 0.6f), 2f);
            }
        }
        for (int I = 0; I < 3; I++)
        {
            float K = (Now * 1.5f + I / 3f) % 1f;
            Vector2 S = Foot + new Vector2(-9 + I * 9, -K * 22);
            DrawRect(new Rect2(S.Round(), new Vector2(2, 2)), new Color(Gold, 1 - K));
        }
    }

    void DrawTacticalWeapon(dynamic Id, dynamic P, Vector2 Foot, float Aim)
    {
        DrawHaste(Id, P, Foot);
        string W = Gd.Str(Gd.At(P, 8));
        if (!Gd.Truth(Gd.At(P, 11)) || W is not ("long_spear" or "stun_maul")) return;
        float Start = this.SwingStart(Gd.Str(Id), true);
        float K = Mathf.Clamp(((float)Clock - Start) / 0.25f, 0f, 1f);
        Vector2 Dir = Vector2.FromAngle(Aim), Side = Dir.Orthogonal(), Hand = Foot - new Vector2(0, 18);
        var Ink = new Color(0.08f, 0.07f, 0.1f);
        if (W == "long_spear")
        {
            // drawn back, driven out to full reach, pulled back
            float Out = K < 0.25f ? -8f * (K / 0.25f) : K < 0.55f ? Mathf.Lerp(-8f, 22f, (K - 0.25f) / 0.3f) : Mathf.Lerp(22f, 4f, (K - 0.55f) / 0.45f);
            Vector2 Butt = Hand - Dir * 12 + Dir * Out;
            Vector2 Tip = Hand + Dir * 46 + Dir * Out;
            DrawLine(Butt, Tip, Ink, 5);
            DrawLine(Butt, Tip, new Color("C39058"), 3);
            DrawColoredPolygon(new[] { Tip + Dir * 13, Tip + Side * 5, Tip - Side * 5 }, new Color("DBE9D7"));
            DrawPolyline(new[] { Tip + Dir * 13, Tip + Side * 5, Tip - Side * 5, Tip + Dir * 13 }, Ink, 1.5f);
            if (K > 0.35f && K < 0.75f)
            {
                // the thrust streak
                float A = 1f - Mathf.Abs(K - 0.55f) / 0.2f;
                DrawLine(Tip + Dir * 15 - Side * 3, Tip + Dir * 34 - Side * 3, new Color(1, 1, 1, 0.6f * A), 2f);
                DrawLine(Tip + Dir * 15 + Side * 3, Tip + Dir * 30 + Side * 3, new Color(1, 1, 1, 0.6f * A), 2f);
            }
        }
        else
        {
            // raised high behind, brought down hard in front, a shock where it lands
            float Swing = K < 0.3f ? Mathf.Lerp(0f, -0.4f, K / 0.3f) : Mathf.Lerp(-0.4f, 1.0f, Mathf.Ease(Mathf.Clamp((K - 0.3f) / 0.3f, 0f, 1f), 2.4f));
            float Lift = (1f - Swing) * 22f;
            Vector2 Head = Hand + Dir * (12 + 18 * Swing) - new Vector2(0, Lift);
            DrawLine(Hand, Head, Ink, 6);
            DrawLine(Hand, Head, new Color("B48550"), 4);
            Vector2 Hd = (Head - Hand).Normalized();
            Vector2 Hs = Hd.Orthogonal();
            var Block = new[] { Head + Hd * 6 + Hs * 12, Head + Hd * 6 - Hs * 12, Head - Hd * 6 - Hs * 12, Head - Hd * 6 + Hs * 12 };
            DrawColoredPolygon(Block, new Color("A0CEE0"));
            DrawPolyline(new[] { Block[0], Block[1], Block[2], Block[3], Block[0] }, Ink, 1.5f);
            if (K >= 0.6f)
            {
                float I = (K - 0.6f) / 0.4f;
                Vector2 Ground = Foot + Dir * 30;
                DrawSetTransform(Ground, 0, new Vector2(1f, 0.45f));
                DrawArc(Vector2.Zero, 8 + 22 * I, 0, Mathf.Tau, 24, new Color("DDD681", 1 - I), 2.5f);
                DrawSetTransform(Vector2.Zero);
                for (int S = 0; S < 3; S++)
                    DrawCircle(Ground - new Vector2(0, 12 + 6 * I) + Vector2.FromAngle(S * Mathf.Tau / 3 + I * 4) * 9, 2f, new Color("FFF3B4", 1 - I));
            }
        }
    }
}
