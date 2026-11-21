using Godot;
using System;

// Fight and knock-down frames for the character sheets of tools/PixelChars.cs (players and enemies).
// Not part of the game; used only by the sprite generator.
//
// A pose starts from the character's standing pixel map. The arm that acts is taken off the body and
// drawn again from its shoulder to where the hand is in this frame, together with the weapon it holds.
// Hands and weapons are placed in 3D around the body (f = forward, s = toward the weapon side,
// u = up) and projected to the view of the row (side, front, back, 3/4), so one table of poses gives
// all 8 directions. A weapon in front of the body hides it; seen from the back the body hides the
// weapon. Arm and weapon get their own dark outline, as in hand-drawn pixel art, and smears, dust
// and sparks are added on top without an outline.
public static partial class PixelPoses
{
    // extra pixels around the body on every side of a sheet cell, so a weapon at full reach still fits
    public const int M = 8;
    // ground depth is drawn shorter than height (top-down 3/4 camera)
    const float K = 0.6f;

    public static readonly Color Ink = new("141019");
    public static readonly Color Shade = new(0, 0, 0, 0.32f);
    static readonly Color Clear = new(0, 0, 0, 0);
    static readonly Color Wood = new("7A5634"), WoodDark = new("4E3520"), WoodLit = new("A07850");
    static readonly Color Stone = new("8A8F98"), StoneLit = new("C9CED6");
    static readonly Color Steel = new("E4E8EC"), SteelDark = new("8A8F98");
    static readonly Color Iron = new("5B5F6B"), IronLit = new("A3A9B3");
    static readonly Color Gold = new("F2B544"), BowString = new("E8E3C8");
    static readonly Color Smear = new(0.91f, 0.96f, 1.0f, 0.6f);
    static readonly Color Dust = new(0.80f, 0.74f, 0.62f, 0.85f);
    static readonly Color Spark = new("FFF2A8");
    static readonly Color Spit = new("D8E03A"), SpitLit = new("F6F9A8"), SpitDark = new("8F9620");
    static readonly Color Crack = new(0.08f, 0.06f, 0.1f, 0.75f);

    // Where a body's arms are, per view (body pixels, before the margin M is added).
    public class Rig
    {
        public Vector2 Main, Off;         // shoulders: the weapon arm and the other one
        public Rect2I EraseMain, EraseOff; // the arms as drawn at rest (taken off when that arm acts)
        public Rect2I Patch;               // pixels repainted with PatchCol (the resting hand in the side view)
        public Color PatchCol;
        public Vector2 Mouth;              // the Spitter's mouth
        public int ArmW = 2, HandW = 2;
        public float Reach = 1.0f;         // hand distances are scaled by this (bigger bodies reach further)
        public Color Arm, Hand, Claw = Clear;
    }

    // The direction a view faces on screen (rows facing left are these, mirrored).
    public static float Facing(string View)
    {
        return Mathf.DegToRad(View switch { "down" => 90.0f, "up" => -90.0f, "down3q" => 45.0f, "up3q" => -45.0f, _ => 0.0f });
    }

    // 3D (forward, weapon side, up) to screen pixels for a body facing angle A.
    static Vector2 Proj(float A, Vector3 V)
    {
        var F = new Vector2(Mathf.Cos(A), Mathf.Sin(A));
        var Sd = new Vector2(-F.Y, F.X);
        var G = ((F * V.X) + (Sd * V.Y));
        return new Vector2(G.X, ((G.Y * K) - V.Z));
    }

    // a unit direction from pitch (up from level) and yaw (toward the weapon side), in degrees
    static Vector3 Dir(float Pitch, float Yaw)
    {
        float P = Mathf.DegToRad(Pitch), Y = Mathf.DegToRad(Yaw);
        return new Vector3((Mathf.Cos(P) * Mathf.Cos(Y)), (Mathf.Cos(P) * Mathf.Sin(Y)), Mathf.Sin(P));
    }

    // --- pixel helpers ------------------------------------------------------------------------------
    static void Set(Image Im, Vector2 P, Color C)
    {
        int X = Mathf.FloorToInt(P.X), Y = Mathf.FloorToInt(P.Y);
        if ((X >= 0) && (Y >= 0) && (X < Im.GetWidth()) && (Y < Im.GetHeight()))
        {
            Im.SetPixel(X, Y, C);
        }
    }

    static void Dot(Image Im, Vector2 P, int W, Color C)
    {
        float O = ((W - 1) / 2.0f);
        for (int Dy = 0; Dy < W; Dy++)
        {
            for (int Dx = 0; Dx < W; Dx++)
            {
                Set(Im, (P + new Vector2((Dx - O), (Dy - O))), C);
            }
        }
    }

    static void Seg(Image Im, Vector2 A, Vector2 B, int W, Color C)
    {
        int N = Math.Max(1, Mathf.CeilToInt((A.DistanceTo(B) * 2.0f)));
        for (int I = 0; I <= N; I++)
        {
            Dot(Im, A.Lerp(B, (I / (float)N)), W, C);
        }
    }

    static Vector2 Across(Vector2 Wd)
    {
        return ((Wd.Length() < 0.25f) ? Vector2.Right : Wd.Normalized().Orthogonal());
    }

    // a block across a weapon (axe or hammer head, sword guard, blade): from T0 to T1 along it, Half wide
    static void Block(Image Im, Vector2 Grip, Vector2 Wd, float T0, float T1, float Half, Color C, Color Lit)
    {
        var Pp = Across(Wd);
        for (float T = T0; T <= T1; T += 0.4f)
        {
            for (float U = -Half; U <= Half; U += 0.4f)
            {
                Set(Im, ((Grip + (Wd * T)) + (Pp * U)), ((U < ((-Half) + 0.7f)) ? Lit : C));
            }
        }
    }

    static Image Blank(int W, int H)
    {
        return Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
    }

    // the layer on top, with a dark line around it drawn over whatever is under that line
    static void Over(Image Dst, Image Layer, bool Outline)
    {
        int W = Dst.GetWidth(), H = Dst.GetHeight();
        if (Outline)
        {
            for (int Y = 0; Y < H; Y++)
            {
                for (int X = 0; X < W; X++)
                {
                    if (Layer.GetPixel(X, Y).A > 0)
                    {
                        continue;
                    }
                    if (((X > 0) && (Layer.GetPixel((X - 1), Y).A >= 1)) || ((X < (W - 1)) && (Layer.GetPixel((X + 1), Y).A >= 1)) || ((Y > 0) && (Layer.GetPixel(X, (Y - 1)).A >= 1)) || ((Y < (H - 1)) && (Layer.GetPixel(X, (Y + 1)).A >= 1)))
                    {
                        Dst.SetPixel(X, Y, Ink);
                    }
                }
            }
        }
        for (int Y = 0; Y < H; Y++)
        {
            for (int X = 0; X < W; X++)
            {
                Color C = Layer.GetPixel(X, Y);
                if (C.A > 0)
                {
                    Dst.SetPixel(X, Y, ((C.A >= 1) ? C : Dst.GetPixel(X, Y).Blend(C)));
                }
            }
        }
    }

    // the layer only where nothing solid is drawn yet
    static void Under(Image Dst, Image Layer)
    {
        for (int Y = 0; Y < Dst.GetHeight(); Y++)
        {
            for (int X = 0; X < Dst.GetWidth(); X++)
            {
                Color C = Layer.GetPixel(X, Y);
                Color D = Dst.GetPixel(X, Y);
                if ((C.A > 0) && (D.A < 1))
                {
                    Dst.SetPixel(X, Y, C.Blend(D));
                }
            }
        }
    }

    // --- whole-body moves ---------------------------------------------------------------------------
    // the body in the middle of a bigger cell
    public static Image Embed(Image Body, int Cell)
    {
        var Im = Blank(Cell, Cell);
        int O = ((Cell - Body.GetWidth()) / 2);
        Im.BlendRect(Body, new Rect2I(0, 0, Body.GetWidth(), Body.GetHeight()), new Vector2I(O, O));
        return Im;
    }

    // everything above the legs moves by D (the legs stay planted); a move up leaves no gap, so it is dropped
    public static Image Lean(Image Body, int Legs, Vector2I D)
    {
        D.Y = Math.Max(0, D.Y);
        int W = Body.GetWidth(), H = Body.GetHeight();
        var Im = Blank(W, H);
        Im.BlendRect(Body, new Rect2I(0, Legs, W, (H - Legs)), new Vector2I(0, Legs));
        Im.BlendRect(Body, new Rect2I(0, 0, W, Legs), D);
        return Im;
    }

    // the knees give: the upper body sinks Drop pixels and the legs get as much shorter
    public static Image Kneel(Image Body, int Legs, int Drop)
    {
        int W = Body.GetWidth(), H = Body.GetHeight();
        var Im = Blank(W, H);
        Im.BlendRect(Body, new Rect2I(0, (Legs + Drop), W, (H - (Legs + Drop))), new Vector2I(0, (Legs + Drop)));
        Im.BlendRect(Body, new Rect2I(0, 0, W, Legs), new Vector2I(0, Drop));
        return Im;
    }

    // Lying on its back: the standing body turned a quarter (head toward the left: it fell away from the
    // way it faced), centred on the cell, its lowest pixel Bottom.
    public static Image Lie(Image Body, int Cell, int Bottom)
    {
        int W = Body.GetWidth(), H = Body.GetHeight();
        var Turned = Blank(H, W);
        int X0 = int.MaxValue, X1 = -1, Y1 = -1;
        for (int Y = 0; Y < H; Y++)
        {
            for (int X = 0; X < W; X++)
            {
                Color C = Body.GetPixel(X, Y);
                if (C.A <= 0)
                {
                    continue;
                }
                // counter-clockwise: (x, y) -> (y, w-1-x)
                int Nx = Y, Ny = ((W - 1) - X);
                Turned.SetPixel(Nx, Ny, C);
                X0 = Math.Min(X0, Nx);
                X1 = Math.Max(X1, Nx);
                Y1 = Math.Max(Y1, Ny);
            }
        }
        var Im = Blank(Cell, Cell);
        if (X1 < 0)
        {
            return Im;
        }
        int Ox = ((Cell / 2) - ((X0 + X1 + 1) / 2));
        int Oy = (Bottom - Y1);
        Im.BlendRect(Turned, new Rect2I(0, 0, H, W), new Vector2I(Ox, Oy));
        return Im;
    }

    // Dark outline around the silhouette, then a soft ground shadow (an ellipse Rx wide) under the feet.
    public static Image Finish(Image Src, float FeetY, float Rx)
    {
        int W = Src.GetWidth(), H = Src.GetHeight();
        var Im = (Image)Src.Duplicate();
        for (int Y = 0; Y < H; Y++)
        {
            for (int X = 0; X < W; X++)
            {
                if (Src.GetPixel(X, Y).A > 0)
                {
                    continue;
                }
                if (((X > 0) && (Src.GetPixel((X - 1), Y).A > 0)) || ((X < (W - 1)) && (Src.GetPixel((X + 1), Y).A > 0)) || ((Y > 0) && (Src.GetPixel(X, (Y - 1)).A > 0)) || ((Y < (H - 1)) && (Src.GetPixel(X, (Y + 1)).A > 0)))
                {
                    Im.SetPixel(X, Y, Ink);
                }
            }
        }
        for (int Y = Math.Max(0, ((int)FeetY - 3)); Y < Math.Min(H, ((int)FeetY + 3)); Y++)
        {
            for (int X = 0; X < W; X++)
            {
                float Dx = (((X + 0.5f) - (W / 2.0f)) / Rx);
                float Dy = (((Y + 0.5f) - FeetY) / 2.2f);
                if ((((Dx * Dx) + (Dy * Dy)) <= 1.0f) && (Im.GetPixel(X, Y).A == 0))
                {
                    Im.SetPixel(X, Y, Shade);
                }
            }
        }
        return Im;
    }

    // --- knocked down ---------------------------------------------------------------------------------
    // 4 frames: hit (staggers back), knees give, falling, lying still with the eyes shut (Shut: the same
    // body with closed eyes). Returns the frames and the width of the shadow under each.
    public static (Image Im, float Rx)[] Fall(Image Body, Image Shut, string View, int Legs, int Feet, int Cell, float Rx)
    {
        float A = Facing(View);
        int Back = -Math.Sign(Mathf.Round(Mathf.Cos(A) * 10.0f));
        int Ground = (Feet + M);
        // lying, the shadow is as long as the body is tall
        int Top = Body.GetUsedRect().Position.Y;
        float Long = (((Feet - Top) / 2.0f) + 1.0f);
        return new[]
        {
            (Embed(Kneel(Lean(Body, Legs, new Vector2I(Back, 0)), Legs, 1), Cell), Rx),
            (Embed(Kneel(Lean(Body, Legs, new Vector2I(Back, 0)), Legs, 3), Cell), Rx),
            (Lie(Body, Cell, (Ground - 4)), (Rx + 2)),
            (Lie(Shut, Cell, Ground), Long),
        };
    }

    // --- attacks --------------------------------------------------------------------------------------
    class Ctx
    {
        public float A;
        public Rig R;
        public Vector2 Sm, So;
        public Image L, Fx;

        // the hand (weapon arm, or the other arm with the side mirrored) at V from its shoulder
        public Vector2 Hand(Vector3 V, bool Off = false)
        {
            var Vv = (Off ? new Vector3(V.X, -V.Y, V.Z) : V);
            return ((Off ? So : Sm) + Proj(A, (Vv * R.Reach)));
        }

        // one unit along a 3D direction, on screen
        public Vector2 W(Vector3 D)
        {
            return Proj(A, D);
        }

        public void ArmTo(Vector2 Hand, bool Off, int HandW = 0)
        {
            Seg(L, (Off ? So : Sm), Hand, R.ArmW, R.Arm);
            Dot(L, Hand, ((HandW > 0) ? HandW : R.HandW), R.Hand);
        }

        // three hooked claws out of a hand, pointing away from the shoulder
        public void Claws(Vector2 Hand, bool Off)
        {
            var D = (Hand - (Off ? So : Sm));
            D = ((D.Length() < 0.5f) ? Vector2.Down : D.Normalized());
            var Pp = D.Orthogonal();
            for (int K = -1; K <= 1; K++)
            {
                var Root = (Hand + (Pp * (K * 1.2f)));
                Seg(L, Root, ((Root + (D * 2.5f)) + (Pp * (K * 0.8f))), 1, R.Claw);
            }
        }

        // a pale crescent left by the tip of a swung weapon, between two hand positions and directions
        public void SmearArc(Vector2 H0, Vector3 D0, Vector2 H1, Vector3 D1, float R0, float R1, Color C)
        {
            for (int I = 0; I <= 14; I++)
            {
                float T = (I / 14.0f);
                var D = D0.Lerp(D1, T).Normalized();
                var H = H0.Lerp(H1, T);
                // thin at the start, full at the end
                float Inner = Mathf.Lerp((R1 - 0.5f), R0, T);
                for (float Rr = Inner; Rr <= R1; Rr += 0.5f)
                {
                    Set(Fx, (H + (W(D) * Rr)), C);
                }
            }
        }

        public void DustRing(Vector2 C, float Rx, float Alpha, bool Cracks)
        {
            var Col = new Color(Dust, (Dust.A * Alpha));
            for (int I = 0; I < 14; I++)
            {
                float Ang = ((I / 14.0f) * Mathf.Tau);
                Set(Fx, (C + new Vector2((Mathf.Cos(Ang) * Rx), (Mathf.Sin(Ang) * Rx * 0.45f))), Col);
            }
            // puffs at the sides, clear of what made the dust
            foreach (var P in new[] { new Vector2(-Rx * 0.9f, -1.5f), new Vector2(Rx * 0.85f, -2), new Vector2(-Rx * 0.6f, -3), new Vector2(Rx * 0.55f, -3.5f) })
            {
                Dot(Fx, (C + P), 2, Col);
            }
            if (Cracks)
            {
                foreach (var D in new[] { new Vector2(1, 0.35f), new Vector2(-1, 0.3f), new Vector2(0.2f, 0.5f) })
                {
                    Seg(Fx, C, (C + (D * 4.0f)), 1, Crack);
                }
            }
        }

        public void Star(Vector2 C, float R)
        {
            Set(Fx, C, Spark);
            foreach (var D in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
            {
                Seg(Fx, (C + (D * 1.0f)), (C + (D * R)), 1, new Color(Spark, 0.9f));
            }
        }
    }

    // How many frames each attack has, and how fast it plays (frames per second).
    public static readonly string[] PLAYER_ATTACKS = { "fists", "spear", "blade", "hammer", "bow" };
    public static readonly float[] PLAYER_ATTACK_FPS = { 18.0f, 16.0f, 16.0f, 12.0f, 12.0f };
    public const int ATTACK_FRAMES = 4;

    // An attack (Kind: fists, spear, blade, hammer, bow; claw, spit, smash for the NOX), 4 finished cells.
    public static Image[] Attack(string Kind, Image Body, string View, Rig R, int Legs, int Feet, int Cell, float ShadowRx)
    {
        float A = Facing(View);
        var Fwd = new Vector2(Mathf.Cos(A), (Mathf.Sin(A) * K));
        bool Behind = View.StartsWith("up");
        var Out = new Image[ATTACK_FRAMES];
        for (int I = 0; I < ATTACK_FRAMES; I++)
        {
            int LeanBy = Kind switch
            {
                "fists" => new[] { 0, 1, 1, 0 }[I],
                "hammer" => new[] { -1, 0, 2, 1 }[I],
                "smash" => new[] { -1, 1, 2, 1 }[I],
                "bow" => 0,
                _ => new[] { -1, 1, 1, 0 }[I],
            };
            bool UsesMain = (Kind != "spit");
            bool UsesOff = ((Kind == "spear") || (Kind == "hammer") || (Kind == "bow") || (Kind == "smash"));
            // take the acting arms off the resting body (seen from the side, the resting hand is painted over)
            var B = (Image)Body.Duplicate();
            if (UsesMain)
            {
                B.FillRect(R.EraseMain, Clear);
                if (R.Patch.Size != Vector2I.Zero)
                {
                    B.FillRect(R.Patch, R.PatchCol);
                }
            }
            if (UsesOff)
            {
                B.FillRect(R.EraseOff, Clear);
            }
            var D = new Vector2I(Mathf.RoundToInt((Fwd.X * LeanBy)), Mathf.RoundToInt((Fwd.Y * LeanBy)));
            var Im = Embed(Lean(B, Legs, D), Cell);
            int O = ((Cell - Body.GetWidth()) / 2);
            // the shoulders move with the upper body (Lean never moves it up)
            var Shift = new Vector2((O + D.X), (O + Math.Max(0, D.Y)));
            var C = new Ctx { A = A, R = R, Sm = (R.Main + Shift), So = (R.Off + Shift), L = Blank(Cell, Cell), Fx = Blank(Cell, Cell) };
            switch (Kind)
            {
                case "fists": Fists(C, I); break;
                case "spear": Spear(C, I); break;
                case "blade": Blade(C, I); break;
                case "hammer": Hammer(C, I); break;
                case "bow": Bow(C, I); break;
                case "claw": Claw(C, I); break;
                case "spit": SpitAt(C, I, (R.Mouth + Shift), Behind); break;
                case "smash": Smash(C, I); break;
            }
            Image Done;
            if (Behind)
            {
                // seen from the back: the body covers the arm and weapon
                Done = Blank(Cell, Cell);
                Over(Done, C.L, true);
                Over(Done, Im, false);
            }
            else
            {
                Done = Im;
                Over(Done, C.L, true);
            }
            Done = Finish(Done, (Feet + O + 1), ShadowRx);
            if (Behind)
            {
                // smears and dust are behind the body too
                Under(Done, C.Fx);
            }
            else
            {
                Over(Done, C.Fx, false);
            }
            Out[I] = Done;
        }
        return Out;
    }

    // jab: guard, the fist shoots out, impact, back to guard
    static void Fists(Ctx C, int I)
    {
        var Hands = new[] { new Vector3(2, -1.5f, -1.5f), new Vector3(7, -2, -0.5f), new Vector3(9, -2.5f, -0.5f), new Vector3(4, -1.5f, -1.5f) };
        var H = C.Hand(Hands[I]);
        C.ArmTo(H, false, 3);
        if (I == 2)
        {
            C.Star((H + (C.W(Dir(0, 0)) * 3.0f)), 3.0f);
        }
        if (I == 1)
        {
            Seg(C.Fx, (H - (C.W(Dir(0, 0)) * 5.0f)), (H - (C.W(Dir(0, 0)) * 2.5f)), 1, Smear);
        }
    }

    // stone spear, two hands: drawn back, thrust, full reach, recover
    static void Spear(Ctx C, int I)
    {
        var Hands = new[] { new Vector3(-2.5f, -1, -1.5f), new Vector3(3.5f, -1, -1.5f), new Vector3(6, -1, -1.5f), new Vector3(1.5f, -1, -2) };
        var Dirs = new[] { Dir(12, 8), Dir(2, 0), Dir(0, 0), Dir(8, 4) };
        var H = C.Hand(Hands[I]);
        var Wd = C.W(Dirs[I]);
        Seg(C.L, (H - (Wd * 6.0f)), (H + (Wd * 9.0f)), 1, Wood);
        Block(C.L, H, Wd, 9.0f, 10.6f, 1.0f, Stone, StoneLit);
        Seg(C.L, (H + (Wd * 10.6f)), (H + (Wd * 12.2f)), 1, StoneLit);
        C.ArmTo((H - (Wd * 4.0f)), true);
        C.ArmTo(H, false);
        if (I == 2)
        {
            var Pp = Across(Wd);
            Seg(C.Fx, ((H + (Wd * 2.0f)) + (Pp * 2.0f)), ((H + (Wd * 8.0f)) + (Pp * 2.0f)), 1, Smear);
            Seg(C.Fx, ((H + (Wd * 3.0f)) - (Pp * 2.0f)), ((H + (Wd * 9.0f)) - (Pp * 2.0f)), 1, Smear);
            Set(C.Fx, (H + (Wd * 13.5f)), Spark);
        }
    }

    // metal blade, one hand: raised, slash across with a crescent smear, follow-through, recover
    static void Blade(Ctx C, int I)
    {
        var Hands = new[] { new Vector3(0, 1, 3.5f), new Vector3(4, 0.5f, 0.5f), new Vector3(5, -2.5f, -1.5f), new Vector3(2.5f, -0.5f, -3) };
        var Dirs = new[] { Dir(75, 25), Dir(15, 40), Dir(-20, -45), Dir(-55, -10) };
        var H = C.Hand(Hands[I]);
        var Wd = C.W(Dirs[I]);
        if ((I == 1) || (I == 2))
        {
            C.SmearArc(C.Hand(Hands[(I - 1)]), Dirs[(I - 1)], H, Dirs[I], 4.0f, 9.0f, Smear);
        }
        Seg(C.L, (H - (Wd * 2.0f)), H, 1, WoodDark);
        Block(C.L, H, Wd, 0.0f, 0.4f, 1.6f, Gold, Gold);
        Block(C.L, H, Wd, 1.0f, 8.0f, 0.5f, Steel, SteelDark);
        Seg(C.L, (H + (Wd * 8.0f)), (H + (Wd * 9.2f)), 1, Steel);
        C.ArmTo(H, false);
    }

    // hammer, two hands: lifted over the head, coming down, smashed into the ground (dust, cracks), recover
    static void Hammer(Ctx C, int I)
    {
        var Hands = new[] { new Vector3(-0.5f, -1.5f, 5), new Vector3(3, -1.5f, 2.5f), new Vector3(5.5f, -1.5f, -2.5f), new Vector3(4.5f, -1.5f, -3) };
        var Dirs = new[] { Dir(115, 0), Dir(40, 0), Dir(-50, 0), Dir(-40, 0) };
        var H = C.Hand(Hands[I]);
        var Wd = C.W(Dirs[I]);
        if ((I == 1) || (I == 2))
        {
            C.SmearArc(C.Hand(Hands[(I - 1)]), Dirs[(I - 1)], H, Dirs[I], 6.0f, 10.5f, Smear);
        }
        Seg(C.L, (H - (Wd * 2.0f)), (H + (Wd * 8.0f)), 1, Wood);
        Block(C.L, H, Wd, 7.5f, 10.5f, 2.5f, Iron, IronLit);
        C.ArmTo((H - (Wd * 2.5f)), true);
        C.ArmTo(H, false);
        if (I >= 2)
        {
            var Hit = (H + (Wd * 9.0f) + new Vector2(0, 2.5f));
            C.DustRing(Hit, ((I == 2) ? 6.0f : 8.5f), ((I == 2) ? 1.0f : 0.55f), true);
        }
    }

    // bow, held out in the other hand: nock, full draw, release (the string snaps, a streak), recover
    static void Bow(Ctx C, int I)
    {
        var Draw = new[] { new Vector3(3, -4.5f, -1), new Vector3(-1, -4.5f, -1), new Vector3(-2.5f, -3.5f, 0), new Vector3(0.5f, -4, -2) };
        float[] Bend = { 1.1f, 1.7f, 1.0f, 1.0f };
        var Grip = C.Hand(new Vector3(6, -4.5f, -1), true);
        var H = C.Hand(Draw[I]);
        // the bow: limbs from the grip up and down, curving back toward the archer. Upright seen from the side;
        // canted over seen from the front or back, so its curve still shows (edge-on it would be a stick).
        float Cant = (Mathf.Abs(Mathf.Sin(C.A)) * Mathf.DegToRad(75.0f));
        Vector2 Limb(float U) => (Grip + C.W(new Vector3(((-(U * U) / 18.0f) * Bend[I]), (U * Mathf.Sin(Cant) * 1.2f), (U * Mathf.Cos(Cant)))));
        for (int K = -7; K < 7; K++)
        {
            Seg(C.L, Limb(K), Limb((K + 1)), 1, ((Math.Abs(K) > 4) ? WoodDark : WoodLit));
        }
        var Top = Limb(7);
        var Bottom = Limb(-7);
        var Fwd = C.W(Dir(0, 0));
        if (I <= 1)
        {
            // string to the drawing hand, and the arrow on it
            Seg(C.L, Top, H, 1, BowString);
            Seg(C.L, H, Bottom, 1, BowString);
            var Tip = (Grip + (Fwd * 3.0f));
            Seg(C.L, H, Tip, 1, WoodLit);
            Dot(C.L, Tip, 2, StoneLit);
        }
        else
        {
            Seg(C.L, Top, Bottom, 1, BowString);
        }
        C.ArmTo(Grip, true);
        C.ArmTo(H, false);
        if (I == 2)
        {
            Seg(C.Fx, (Grip + (Fwd * 3.0f)), (Grip + (Fwd * 11.0f)), 1, Smear);
            C.Star((Grip + (Fwd * 2.0f)), 2.0f);
        }
    }

    // NOX claw swipe: arm raised, the claws rake across (magenta smear), follow-through, recover
    static void Claw(Ctx C, int I)
    {
        var Hands = new[] { new Vector3(0, 1.5f, 4.5f), new Vector3(5, 0.5f, 0.5f), new Vector3(5, -3.5f, -2.5f), new Vector3(1.5f, 0, -4) };
        var H = C.Hand(Hands[I]);
        if ((I == 1) || (I == 2))
        {
            var H0 = C.Hand(Hands[(I - 1)]);
            for (int K = 0; K <= 10; K++)
            {
                var P = H0.Lerp(H, (K / 10.0f));
                Dot(C.Fx, (P + (C.W(Dir(0, 0)) * 2.0f)), 2, new Color(C.R.Claw, 0.5f));
            }
        }
        C.ArmTo(H, false);
        C.Claws(H, false);
    }

    // Spitter: rears back with the mouth open, then a glob of acid flies out
    static void SpitAt(Ctx C, int I, Vector2 Mouth, bool Behind)
    {
        var Fwd = C.W(Dir(5, 0));
        if ((I == 0) && !Behind)
        {
            Dot(C.L, Mouth, 2, new Color("120A16"));
        }
        if ((I == 1) || (I == 2))
        {
            var G = (Mouth + (Fwd * ((I == 1) ? 3.0f : 8.0f)));
            Dot(C.L, G, 3, Spit);
            Set(C.L, (G - new Vector2(1, 1)), SpitLit);
            Set(C.Fx, (G - (Fwd * 2.5f)), SpitDark);
            if (I == 2)
            {
                Set(C.Fx, (G - (Fwd * 4.5f)), SpitDark);
            }
        }
        if (I == 3)
        {
            Set(C.Fx, (Mouth + (Fwd * 10.0f)), new Color(Spit, 0.7f));
            Set(C.Fx, (Mouth + (Fwd * 12.0f) + new Vector2(0, 1)), new Color(Spit, 0.5f));
        }
    }

    // Wrecker: both fists up, down, into the ground (dust and cracks), recover
    static void Smash(Ctx C, int I)
    {
        var Hands = new[] { new Vector3(-0.5f, -0.5f, 6.5f), new Vector3(4, -1, 1), new Vector3(6.5f, -1.5f, -5.5f), new Vector3(5, -1, -4.5f) };
        var Hm = C.Hand(Hands[I]);
        var Ho = C.Hand(Hands[I], true);
        C.ArmTo(Ho, true, (C.R.HandW + 1));
        C.Claws(Ho, true);
        C.ArmTo(Hm, false, (C.R.HandW + 1));
        C.Claws(Hm, false);
        if (I >= 2)
        {
            var Hit = (Hm.Lerp(Ho, 0.5f) + new Vector2(0, 3));
            C.DustRing(Hit, ((I == 2) ? 8.0f : 11.0f), ((I == 2) ? 1.0f : 0.55f), true);
        }
    }
}
