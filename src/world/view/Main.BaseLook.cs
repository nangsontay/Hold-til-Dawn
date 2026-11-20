using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. How the base looks: weathered concrete walls (cream concrete with pilasters, rust runs and moss
// at the foot), the paved stone floor inside, and the warm lamps on the door pillars and the north wall that light it
// at night. The roof seen from outside (src/world/view/Main.BaseFog.cs) uses the same concrete for its rim, so walls and
// roof read as one building. All drawn in code, pixel by pixel, with a fixed hash so nothing flickers.
public partial class Main : Node2D
{
    // the base walls are passed to Wall() with this top colour (Main.WallItems); Wall() hands them to ConcreteWall
    public static readonly Color BASE_WALL_TOP = new Color("8A8F98");

    // concrete
    static readonly Color CON_TOP = new Color("CFC5AC");
    static readonly Color CON_TOP_D = new Color("B4AA90");
    static readonly Color CON_TOP_L = new Color("E2D9C3");
    static readonly Color CON_FACE = new Color("B5AA90");
    static readonly Color CON_FACE_L = new Color("C3B89E");
    static readonly Color CON_FACE_D = new Color("968B74");
    static readonly Color CON_PIL = new Color("C8BDA3");
    static readonly Color CON_GRIME = new Color("7A705C");
    static readonly Color RUST = new Color("9C5B33");
    static readonly Color RUST_D = new Color("74401F");
    static readonly Color MOSS = new Color("6E7B38");
    static readonly Color MOSS_D = new Color("55602A");
    // paving inside
    static readonly Color PAVE_A = new Color("6F6A5E");
    static readonly Color PAVE_B = new Color("847E70");
    static readonly Color PAVE_SEAM = new Color("4F4B42");
    static readonly Color PAVE_LIT = new Color("958F80");
    // lamps: wall sconces on the north wall (x positions) and one on top of every door pillar
    static readonly float[] SCONCE_X = { -150.0f, 150.0f };
    static readonly Color LAMP_LIGHT = new Color("FF9A3C");
    static readonly Color LAMP_HOT = new Color(2.4f, 1.6f, 0.6f);
    const int PILASTER_EVERY = 96;

    public System.Collections.Generic.List<PointLight2D> BaseLamps = new System.Collections.Generic.List<PointLight2D>();

    static int Hash(int X, int Y)
    {
        unchecked
        {
            int H = (X * 374761393) + (Y * 668265263);
            H = (H ^ (H >> 13)) * 1274126177;
            return (H ^ (H >> 16)) & 0x7fffffff;
        }
    }

    static int Mod(int A, int M)
    {
        return (((A % M) + M) % M);
    }

    void Px(float X, float Y, float W, float H, Color C)
    {
        DrawRect(new Rect2(X, Y, W, H), C);
    }

    static Color A(Color C, float Alpha)
    {
        C.A = Alpha;
        return C;
    }

    // A horizontal wall whose front face looks into the base (the north wall, door B's gate and pillars): under the
    // roof, seen from outside, that face is hidden.
    bool FaceUnderRoof(Rect2 R)
    {
        Vector2 H = (Vector2)GameSim.HALF;
        Vector2 C = R.GetCenter();
        return ((BaseFog > 0.5f) && (R.Size.X >= R.Size.Y) && (C.Y < 0) && (Mathf.Abs(C.X) < H.X));
    }

    // Same shape as Main.Wall: the top face lifted by Height, the front face below it, ink edges. Hide is how much of the
    // front face, from the bottom, is not drawn (-1: all of it when it faces into a roofed base).
    public void ConcreteWall(Rect2 R, float Height, bool Cap, float Hide = -1.0f)
    {
        var Top = new Rect2((R.Position - new Vector2(0, Height)), R.Size);
        var F = new Rect2(R.Position.X, (R.End.Y - Height), R.Size.X, Height);
        if (Hide < 0)
        {
            Hide = (this.FaceUnderRoof(R) ? Height : 0.0f);
        }
        float FaceEnd = (F.End.Y - Hide);
        bool Face = (FaceEnd > (F.Position.Y + 0.5f));
        if (Face)
        {
            this.ConcreteFace(new Rect2(F.Position, new Vector2(F.Size.X, (FaceEnd - F.Position.Y))), (Hide <= 0.0f));
        }
        this.ConcreteTop(Top, (R.Size.X >= R.Size.Y));
        float Bottom = (Face ? FaceEnd : Top.End.Y);
        // seen from outside, the wall tops run straight on into the roof (src/world/view/Main.BaseFog.cs): no ink
        // line along an edge the roof touches
        Rect2 Roof = RoofRect();
        var Seam = new Color(INK, (INK.A * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, BaseFog))));
        Color Ink(bool OnRoof) => (OnRoof ? Seam : INK);
        DrawLine(new Vector2(Top.Position.X, Top.Position.Y), new Vector2(Top.Position.X, Bottom), Ink(Mathf.Abs(Top.Position.X - Roof.End.X) < 1.0f), 2);
        DrawLine(new Vector2(Top.End.X, Top.Position.Y), new Vector2(Top.End.X, Bottom), Ink(Mathf.Abs(Top.End.X - Roof.Position.X) < 1.0f), 2);
        DrawLine(new Vector2(Top.Position.X, Bottom), new Vector2(Top.End.X, Bottom), Ink(Mathf.Abs(Bottom - Roof.Position.Y) < 1.0f), 2);
        if (Cap)
        {
            DrawLine(Top.Position, new Vector2(Top.End.X, Top.Position.Y), Ink(Mathf.Abs(Top.Position.Y - Roof.End.Y) < 1.0f), 2);
        }
        // the lamps on the inside of the north wall
        if (Face && (Hide <= 0.0f) && (R.Size.X >= R.Size.Y) && (R.GetCenter().Y < 0) && (Mathf.Abs(R.GetCenter().X) < ((Vector2)GameSim.HALF).X))
        {
            foreach (float Sx in SCONCE_X)
            {
                if ((Sx > (F.Position.X + 6)) && (Sx < (F.End.X - 6)))
                {
                    this.Sconce(new Vector2(Sx, (F.Position.Y + (Height * 0.45f))));
                }
            }
        }
    }

    void ConcreteFace(Rect2 F, bool Foot)
    {
        Px(F.Position.X, F.Position.Y, F.Size.X, F.Size.Y, CON_FACE);
        // formwork lines
        for (float Y = (F.Position.Y + 6); Y < (F.End.Y - 2); Y += 8.0f)
        {
            Px(F.Position.X, Y, F.Size.X, 1, A(CON_FACE_L, 0.7f));
        }
        int X0 = Mathf.CeilToInt(F.Position.X);
        int X1 = Mathf.FloorToInt(F.End.X);
        int Yt = Mathf.RoundToInt(F.Position.Y);
        int Hh = Mathf.Max(1, Mathf.RoundToInt(F.Size.Y));
        for (int X = X0; X < X1; X++)
        {
            if (Mod(X, PILASTER_EVERY) == 0)
            {
                // a pilaster: a lighter column standing a little proud of the wall
                Px((X - 3), F.Position.Y, 7, F.Size.Y, CON_PIL);
                Px((X - 3), F.Position.Y, 1, F.Size.Y, CON_TOP_L);
                Px((X + 3), F.Position.Y, 1, F.Size.Y, CON_FACE_D);
            }
            else if (Mod(X, 32) == 0)
            {
                Px(X, F.Position.Y, 1, F.Size.Y, A(CON_FACE_D, 0.8f));
            }
        }
        for (int X = (X0 - Mod(X0, 2)); X < X1; X += 2)
        {
            int H = Hash(X, 7);
            float Xs = Mathf.Max(X, F.Position.X);
            float W = (Mathf.Min((X + 2), F.End.X) - Xs);
            if (W <= 0)
            {
                continue;
            }
            if ((H % 9) == 0)
            {
                // a rust run down from the coping
                int Len = Mathf.Min(Hh, (3 + ((H >> 4) % 9)));
                Px(Xs, F.Position.Y, W, Len, A(RUST, 0.55f));
                Px(Xs, (F.Position.Y + Len - 1), W, 1, A(RUST_D, 0.6f));
            }
            if (Foot && ((H % 7) == 1))
            {
                // moss and damp at the foot
                int Len = Mathf.Min(Hh, (2 + ((H >> 6) % 6)));
                Px(Xs, (F.End.Y - Len), W, Len, A(MOSS, 0.75f));
                Px(Xs, (F.End.Y - Len), W, 1, A(MOSS_D, 0.6f));
            }
            if ((H % 11) == 3)
            {
                Px(Xs, (Yt + ((H >> 8) % Hh)), 1, 1, A(CON_FACE_D, 0.9f));
            }
            if ((H % 23) == 5)
            {
                // a lighter, sun-bleached patch
                Px(Xs, (Yt + ((H >> 9) % Hh)), W, 3, A(CON_TOP_L, 0.35f));
            }
        }
        // shadow under the coping and grime along the foot
        Px(F.Position.X, F.Position.Y, F.Size.X, 2, new Color(0, 0, 0, 0.18f));
        if (Foot)
        {
            Px(F.Position.X, (F.End.Y - 2), F.Size.X, 2, A(CON_GRIME, 0.6f));
        }
    }

    void ConcreteTop(Rect2 Top, bool Horizontal)
    {
        Px(Top.Position.X, Top.Position.Y, Top.Size.X, Top.Size.Y, CON_TOP);
        int X0 = Mathf.CeilToInt(Top.Position.X);
        int Y0 = Mathf.CeilToInt(Top.Position.Y);
        for (int X = (X0 - Mod(X0, 2)); X < Top.End.X; X += 2)
        {
            for (int Y = (Y0 - Mod(Y0, 2)); Y < Top.End.Y; Y += 2)
            {
                if (!Top.HasPoint(new Vector2(X, Y)))
                {
                    continue;
                }
                int H = Hash(X, Y);
                if ((H % 13) == 0)
                {
                    Px(X, Y, 1, 1, CON_TOP_D);
                }
                else if ((H % 61) == 1)
                {
                    Px(X, Y, 2, 1, A(MOSS, 0.8f));
                }
                else if ((H % 89) == 2)
                {
                    Px(X, Y, 1, 1, A(RUST, 0.8f));
                }
            }
        }
        // the edge toward the camera catches the light, the far edge is worn darker
        Px(Top.Position.X, (Top.End.Y - 1), Top.Size.X, 1, CON_TOP_L);
        Px(Top.Position.X, Top.Position.Y, Top.Size.X, 1, CON_TOP_D);
        // pilaster caps, a step proud of the coping
        if (Horizontal)
        {
            for (int X = X0; X < Top.End.X; X++)
            {
                if ((Mod(X, PILASTER_EVERY) == 0) && (X > (Top.Position.X + 5)) && (X < (Top.End.X - 5)))
                {
                    Px((X - 4), (Top.Position.Y - 1), 9, (Top.Size.Y + 1), CON_TOP_L);
                    Px((X - 4), (Top.Position.Y - 1), 9, 1, CON_TOP_D);
                    Px((X + 4), Top.Position.Y, 1, Top.Size.Y, CON_FACE_D);
                }
            }
        }
        else
        {
            for (int Y = Y0; Y < Top.End.Y; Y++)
            {
                if ((Mod(Y, PILASTER_EVERY) == 0) && (Y > (Top.Position.Y + 5)) && (Y < (Top.End.Y - 5)))
                {
                    Px((Top.Position.X - 1), (Y - 4), (Top.Size.X + 2), 9, CON_TOP_L);
                    Px((Top.Position.X - 1), (Y + 4), (Top.Size.X + 2), 1, CON_FACE_D);
                }
            }
        }
    }

    // A door pillar in the same concrete, taller than the wall, a lamp on top that glows from dusk.
    public void ConcretePillar(Rect2 R, float Height)
    {
        // under the roof only the part above the wall shows
        float Hide = (this.FaceUnderRoof(R) ? (float)WALL_H : 0.0f);
        this.ConcreteWall(R, Height, true, Hide);
        var Lamp = new Rect2((R.GetCenter() - new Vector2(2, (Height + 1))).Round(), new Vector2(4, 3));
        DrawRect(Lamp.Grow(1), INK);
        DrawRect(Lamp, (this.LampsLit() ? LAMP_HOT : (Color)Gd.MakeColor("F2B544")));
    }

    void Sconce(Vector2 P)
    {
        P = P.Round();
        Px((P.X - 2), (P.Y + 2), 5, 2, INK);
        Px((P.X - 2), (P.Y - 3), 5, 5, INK);
        Px((P.X - 1), (P.Y - 2), 3, 3, (this.LampsLit() ? LAMP_HOT : new Color("F2B544")));
        if (this.LampsLit())
        {
            // warm light washing down the wall
            Px((P.X - 5), (P.Y + 4), 11, 6, A(LAMP_LIGHT, 0.12f));
        }
    }

    bool LampsLit()
    {
        return Gd.Contains(new GArray("dusk", "night"), Gd.Get(Snap, "ph", ""));
    }

    // The paved floor inside the walls: big stone slabs in staggered rows, each a slightly different shade, with dark
    // joints, a lit top edge and the odd crack.
    public void DrawBaseFloor()
    {
        Vector2 H = (Vector2)GameSim.HALF;
        float In = (float)GameSim.WALL_IN;
        var Floor = new Rect2((-H.X + In), (-H.Y + In), (2 * (H.X - In)), (2 * (H.Y - In)));
        Px(Floor.Position.X, Floor.Position.Y, Floor.Size.X, Floor.Size.Y, PAVE_SEAM);
        const int S = 32;
        const int Sh = 24;
        int Row = 0;
        for (float Y = Floor.Position.Y; Y < Floor.End.Y; Y += Sh)
        {
            float Off = (((Row % 2) == 0) ? 0.0f : (S / 2));
            for (float X = (Floor.Position.X - Off); X < Floor.End.X; X += S)
            {
                var Slab = new Rect2((X + 1), (Y + 1), (S - 1), (Sh - 1)).Intersection(Floor);
                if (!Slab.HasArea())
                {
                    continue;
                }
                int Hs = Hash(Mathf.RoundToInt(X), Mathf.RoundToInt(Y));
                Color C = PAVE_A.Lerp(PAVE_B, ((Hs % 100) / 100.0f));
                DrawRect(Slab, C);
                Px(Slab.Position.X, Slab.Position.Y, Slab.Size.X, 1, A(PAVE_LIT, 0.8f));
                Px(Slab.Position.X, Slab.Position.Y, 1, Slab.Size.Y, A(PAVE_LIT, 0.5f));
                // speckle
                for (int K = 0; K < 6; K++)
                {
                    int Hk = Hash((Hs + K), K);
                    Px((Slab.Position.X + (Hk % Mathf.Max(1, (int)Slab.Size.X))), (Slab.Position.Y + ((Hk >> 8) % Mathf.Max(1, (int)Slab.Size.Y))), 1, 1, A(PAVE_SEAM, 0.6f));
                }
                if ((Hs % 5) == 0)
                {
                    // a crack across the slab
                    Vector2 P = (Slab.Position + new Vector2((4 + ((Hs >> 4) % 10)), 3));
                    for (int K = 0; K < 8; K++)
                    {
                        if (!Slab.HasPoint(P))
                        {
                            break;
                        }
                        Px(P.X, P.Y, 1, 1, A(PAVE_SEAM, 0.9f));
                        P += new Vector2((((Hash(Hs, K) % 3) == 0) ? 1 : 0), 1) + new Vector2((((Hash(K, Hs) % 2) == 0) ? 1 : 0), 0);
                    }
                }
            }
            Row += 1;
        }
        // damp, darker corners and a soft shade along the north wall
        Px(Floor.Position.X, Floor.Position.Y, Floor.Size.X, 8, new Color(0, 0, 0, 0.12f));
    }

    // --- lamps ---------------------------------------------------------------------
    public void SetupBaseLamps(dynamic Tex)
    {
        var Spots = new System.Collections.Generic.List<Vector2>();
        for (int I = 0; I < 3; I++)
        {
            foreach (dynamic Fp in Gd.Iter(this.DoorPillars(I)))
            {
                Spots.Add(((Rect2)Fp).GetCenter());
            }
        }
        float North = (((float)-((Vector2)GameSim.HALF).Y) + (float)GameSim.WALL_IN + 6.0f);
        foreach (float Sx in SCONCE_X)
        {
            Spots.Add(new Vector2(Sx, North));
        }
        foreach (Vector2 P in Spots)
        {
            var L = new PointLight2D();
            L.Texture = (Texture2D)Gd.Unwrap(Tex);
            L.Color = LAMP_LIGHT;
            L.Energy = 0.9f;
            L.TextureScale = ((2.2f * (float)T * 2.0f) / 256.0f);
            L.Position = P;
            L.Enabled = false;
            L.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            L.TextureRepeat = CanvasItem.TextureRepeatEnum.Disabled;
            AddChild(L);
            BaseLamps.Add(L);
        }
    }

    public void TickBaseLamps(float Night)
    {
        foreach (PointLight2D Pl in BaseLamps)
        {
            Pl.Enabled = (Night > 0.05f);
            Pl.Energy = (0.75f * Night);
        }
    }
}
