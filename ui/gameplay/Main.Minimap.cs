using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Mai Thien Ngon. Minimap (top left) so players do not get lost on the island: it shows the part of the
// island around you, and M shows the whole island large, laid out like the level-design map
// (src/world/IslandMap.cs): numbered landmarks with their names, the main gathering routes, the exploration
// trails, the safe zone, the night spawn bands, the boss spawns and the names of the bays.
// The ground (sea and shallows, beach, grass, safe zone, woods, hills, pond, swamp, trees and rocks) is drawn once
// into a pixel image; paths, markers, nodes, towers, doors, pads, teammates, you and the camera view are drawn on
// top every frame. Walking into a landmark's area shows its name at the top of the screen.
// Enemies are not shown: the GDD only shows what players can see (section 6.1).
public partial class Main : Node2D
{
    public const int MAP_PX = 120;
    // the ground image has twice the pixels, so the large map stays sharp
    const int MAP_TEX = 360;
    public dynamic MapBig = false;
    // the terrain image, drawn with hard pixels on the linearly filtered HUD
    CanvasTexture MapTex = null;
    // the landmark you are in, and when you walked into it
    IslandMap.Landmark HereNow;
    float HereSince = -99.0f;

    static readonly Color MAP_ROUTE = new Color("FFD98C"), MAP_TRAIL = new Color("E6CA96"), MAP_BAND = new Color(0.95f, 0.64f, 0.72f);

    // world half-width covered by the map: the island plus a strip of sea
    public static float MapReach() => 82.0f * IslandMap.SCALE * (float)T;
    // world half-width the small map shows round you
    const float MINI_VIEW = 41.0f;

    public void ToggleMap()
    {
        MapBig = !Gd.Truth(MapBig);
        if (Gd.Truth(MapBig))
        {
            BagOpen = false;
            CraftOpen = false;
            this.CloseBuild();
        }
    }

    static Color KindColour(string Kind) => Kind switch
    {
        "base" or "titanium" => new Color("F2C27D"),
        "wood" => new Color("A8D18A"),
        "herb" => new Color("B3E0A3"),
        "stone" => new Color("E8D6AD"),
        "swamp" => new Color("DEADD6"),
        _ => new Color("9BD1D1"),
    };

    void BuildMapTexture()
    {
        var Im = Image.CreateEmpty(MAP_TEX, MAP_TEX, false, Image.Format.Rgba8);
        float Reach = MapReach();
        float Per = 2 * Reach / MAP_TEX;
        Vector2 Half = (Vector2)GameSim.HALF;
        float Safe = (float)GameSim.SAFE_D, Tile = (float)T, Sc = IslandMap.SCALE;
        Color Deep = new Color("163346"), Sand = new Color("CDB688");
        Color[] Shallow = { new Color("1E4256"), new Color("27546A"), new Color("33707F") };
        Color Outer = new Color("5A6F3A"), Inner = new Color("6E8548"), Woods = new Color("4C6230");
        Color Floor = new Color("4F4B42"), Wall = new Color("CFC5AC");
        WorldGeometry Terrain = (WorldGeometry)Geometry;
        var ShadeBox = IslandMap.Shade.ConvertAll(S => Bounds(S, 0));
        for (int Y = 0; Y < MAP_TEX; Y++)
            for (int X = 0; X < MAP_TEX; X++)
            {
                var P = new Vector2((X + 0.5f) * Per - Reach, (Y + 0.5f) * Per - Reach);
                float D = IslandMap.Depth(P);
                Color Col;
                if (D < 0)
                    Col = D > -2 * Sc * Tile ? Shallow[2] : D > -5 * Sc * Tile ? Shallow[1] : D > -9 * Sc * Tile ? Shallow[0] : Deep;
                else if (!IslandMap.OnGrass(P)) Col = Sand;
                else if (Mathf.Abs(P.X) < Half.X && Mathf.Abs(P.Y) < Half.Y)
                    Col = (Mathf.Abs(P.X) > Half.X - Per || Mathf.Abs(P.Y) > Half.Y - Per) ? Wall : Floor;
                else
                {
                    Col = (float)WorldGeometry.WallDist(P) < Safe ? Inner : Outer;
                    for (int I = 0; I < IslandMap.Shade.Count; I++)
                        if (ShadeBox[I].HasPoint(P) && IslandMap.InPolygon(IslandMap.Shade[I], P)) Col = Col.Lerp(Woods, 0.6f);
                    foreach (var H in IslandMap.Hills)
                        for (int K = 0; K < H.Rings.Count; K++)
                            if (P.DistanceTo(H.Top) < 14 * Sc * Tile && IslandMap.InPolygon(H.Rings[K], P))
                                Col = new Color("6E8048").Lerp(new Color("A49C7E"), (float)K / Math.Max(1, H.Rings.Count - 1));
                    if (P.DistanceTo(Terrain.SwampCentre) < Terrain.SwampR * 0.85f) Col = new Color("55583A");
                    foreach (var Pd in Terrain.Ponds)
                        if (IslandMap.InPolygon(Pd.Edge, P)) Col = new Color("3B6A86");
                }
                Im.SetPixel(X, Y, Col);
            }
        // forest and rock outcrops, so the map shows the same landmarks the player walks past
        foreach (dynamic D in Gd.Iter(Deco))
        {
            string Name = Gd.Str(Gd.At(D, 1));
            Color Col;
            if (Name.StartsWith("pine")) Col = new Color("2A4A3A");
            else if (Name.StartsWith("dead")) Col = new Color("4E4741");
            else if (Name.StartsWith("rock") || Name.StartsWith("crag")) Col = new Color("9AA2A8");
            else if (Name.StartsWith("lm_palm") || Name.StartsWith("lm_mangrove")) Col = new Color("2F5A34");
            else if (Name.StartsWith("lm_")) Col = new Color("D8CDB0");
            else continue;
            Vector2 P = (Vector2)Gd.At(D, 0);
            int Px = (int)((P.X + Reach) / Per), Py = (int)((P.Y + Reach) / Per);
            if (Px >= 0 && Py >= 0 && Px < MAP_TEX && Py < MAP_TEX) Im.SetPixel(Px, Py, Col);
        }
        // the landmarks' concrete and the pier (src/world/WorldLandmarks.cs)
        void Fill(Rect2 F, Color Col)
        {
            for (int Py = (int)((F.Position.Y + Reach) / Per); Py <= (int)((F.End.Y + Reach) / Per); Py++)
                for (int Px = (int)((F.Position.X + Reach) / Per); Px <= (int)((F.End.X + Reach) / Per); Px++)
                    if (Px >= 0 && Py >= 0 && Px < MAP_TEX && Py < MAP_TEX) Im.SetPixel(Px, Py, Col);
        }
        foreach (var (F, _) in Terrain.Floors) Fill(F, new Color("A9A79C"));
        foreach (var D in IslandMap.Docks) Fill(D, new Color("8C7150"));
        MapTex = new CanvasTexture { DiffuseTexture = ImageTexture.CreateFromImage(Im), TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
    }

    public Rect2 MapRect(dynamic Vs)
    {
        if (Gd.Truth(MapBig))
        {
            float S = Mathf.Floor(Mathf.Min((float)Vs.Y - 110, 3 * MAP_PX) / MAP_PX) * MAP_PX;
            return new Rect2(((float)Vs.X - S) / 2 - 70, ((float)Vs.Y - S) / 2 + 4, S, S);
        }
        // top left, where the day and timer were (they moved to the top right)
        return new Rect2(10, 10, MAP_PX, MAP_PX);
    }

    // a dashed polyline in screen space
    static void Dashed(CanvasItem C, IList<Vector2> Pts, Color Col, float Width, float Dash, float Gap)
    {
        float Left = Dash;
        bool On = true;
        for (int I = 0; I < Pts.Count - 1; I++)
        {
            Vector2 A = Pts[I], B = Pts[I + 1];
            float Len = A.DistanceTo(B);
            float At = 0;
            while (At < Len)
            {
                float Step = Mathf.Min(Left, Len - At);
                if (On) C.DrawLine(A.Lerp(B, At / Len), A.Lerp(B, (At + Step) / Len), Col, Width);
                At += Step;
                Left -= Step;
                if (Left <= 0.001f)
                {
                    On = !On;
                    Left = On ? Dash : Gap;
                }
            }
        }
    }

    public void DrawMinimap(dynamic C, dynamic Vs, dynamic Me)
    {
        if (MapTex == null)
        {
            if (Gd.Size(Deco) == 0) return;
            this.BuildMapTexture();
        }
        CanvasItem Ci = (CanvasItem)Gd.Unwrap(C);
        Rect2 R = this.MapRect(Vs);
        bool Big = Gd.Truth(MapBig);
        // the small map follows you, kept inside the island's square
        float Reach = MapReach();
        float View = Big ? Reach : MINI_VIEW * (float)T;
        Vector2 Focus = Big ? Vector2.Zero : ((Vector2)LocalPos).Clamp(-Vector2.One * (Reach - View), Vector2.One * (Reach - View));
        float Scale = R.Size.X / (2 * View);
        Vector2 Mid = R.GetCenter();
        Vector2 To(dynamic P) => (Mid + ((Vector2)P - Focus) * Scale).Round();
        Rect2 Inside = R.Grow(-1);
        float Dot = Big ? 3 : 2;
        float Tl = (float)T;
        this.Panel(C, R.Grow(4));
        float Texel = MAP_TEX / (2 * Reach);
        var Src = new Rect2((Focus - Vector2.One * View + Vector2.One * Reach) * Texel, Vector2.One * (2 * View * Texel));
        C.DrawTextureRectRegion(MapTex, R, Src);
        // paths: door roads, exploration trails, then the main gathering routes on top (clipped to the map)
        WorldGeometry W = (WorldGeometry)Geometry;
        List<Vector2> Clip(Vector2[] Path)
        {
            var Out = new List<Vector2>();
            foreach (var P in Path) Out.Add(To(P).Clamp(Inside.Position, Inside.End));
            return Out;
        }
        for (int I = 0; I < 3 && I < W.Trails.Count; I++) Ci.DrawPolyline(Clip(W.Trails[I].Path).ToArray(), new Color("8C7F5B"), Big ? 3 : 2);
        foreach (var Tr in IslandMap.Trails) Dashed(Ci, Clip(Tr), MAP_TRAIL, 1, Big ? 2 : 1.5f, Big ? 3 : 2);
        foreach (var Rt in IslandMap.Routes)
        {
            var Pts = Clip(Rt);
            Ci.DrawPolyline(Pts.ToArray(), new Color(0.13f, 0.18f, 0.13f, 0.8f), Big ? 3.5f : 2.5f);
            Dashed(Ci, Pts, MAP_ROUTE, Big ? 1.6f : 1.2f, Big ? 5 : 3, Big ? 4 : 2);
        }
        if (Big)
        {
            // the safe zone, the night spawn bands and the boss spawns with their way in
            var Ring = new List<Vector2>();
            foreach (dynamic P in Gd.Iter(this.RoundRect(GameSim.HALF, GameSim.SAFE_D))) Ring.Add(To(P));
            Ring.Add(Ring[0]);
            Dashed(Ci, Ring, new Color(0.88f, 0.91f, 0.65f, 0.85f), 1, 4, 4);
            for (int I = 0; I < 3; I++)
            {
                Vector2 Door = (Vector2)Gd.At(GameSim.DOOR_POS, I), Dir = (Vector2)Gd.At(GameSim.DOOR_DIR, I);
                Vector2 Side = new Vector2(-Dir.Y, Dir.X);
                Vector2 Band = Door + Dir * 30 * Tl;
                var Corners = new[] { To(Band - Dir * 5 * Tl - Side * 5 * Tl), To(Band + Dir * 5 * Tl - Side * 5 * Tl), To(Band + Dir * 5 * Tl + Side * 5 * Tl), To(Band - Dir * 5 * Tl + Side * 5 * Tl) };
                Ci.DrawColoredPolygon(Corners, new Color(MAP_BAND, 0.28f));
                Ci.DrawPolyline(new[] { Corners[0], Corners[1], Corners[2], Corners[3], Corners[0] }, new Color(MAP_BAND, 0.8f), 1);
                Vector2 Boss = IslandMap.PullInland(Door + Dir * 60 * Tl, 2 * Tl);
                Dashed(Ci, new List<Vector2> { To(Boss), To(Door + Dir * 13 * Tl) }, new Color(MAP_BAND, 0.85f), 1.2f, 3, 3);
                Vector2 Bp = To(Boss);
                Ci.DrawColoredPolygon(new[] { Bp + new Vector2(0, -6), Bp + new Vector2(6, 0), Bp + new Vector2(0, 6), Bp + new Vector2(-6, 0) }, new Color("4A2B45"));
                Ci.DrawPolyline(new[] { Bp + new Vector2(0, -6), Bp + new Vector2(6, 0), Bp + new Vector2(0, 6), Bp + new Vector2(-6, 0), Bp + new Vector2(0, -6) }, new Color("FFC4D4"), 1.2f);
                this.Text(C, Bp + new Vector2(-30, 18), "BOSS " + Gd.At(GameSim.DOOR_NAMES, I), 9, new Color("FFC4D4"), 1, 60);
            }
            foreach (var (Name, Pos) in IslandMap.Bays)
                if (Inside.HasPoint(To(Pos))) this.Text(C, To(Pos) + new Vector2(-50, 4), Name.ToUpper(), 9, new Color(0.75f, 0.85f, 0.88f, 0.8f), 1, 100);
        }
        // camera view
        dynamic Ct = Gd.Native(this, "get_viewport").GetCanvasTransform();
        Vector2 Screen = (Vector2)Gd.Native(this, "get_viewport_rect").Size;
        Transform2D Inv = ((Transform2D)Ct).AffineInverse();
        Vector2 A = To(Inv * Vector2.Zero), B = To(Inv * Screen);
        var Cam = new Rect2(A, B - A).Intersection(R);
        if (Cam.HasArea()) C.DrawRect(Cam, new Color(1, 1, 1, 0.35f), false, 1);
        // resource nodes still holding resources
        foreach (dynamic Nd in Gd.Iter(Gd.Get(Snap, "nodes", new GArray())))
        {
            if (Gd.At(Nd, 2) <= 0 || !Inside.HasPoint(To(Gd.At(Nd, 1)))) continue;
            C.DrawRect(new Rect2(To(Gd.At(Nd, 1)) - Vector2.One * (Dot / 2), Vector2.One * Dot), Gd.At(RES_COL, Gd.At(Nd, 0)));
        }
        // landmarks: numbered rings (numbers on the large map)
        foreach (var L in IslandMap.Landmarks)
        {
            if (L.Id == 1) continue;
            Vector2 Lp = To(L.Pos);
            if (!Inside.Grow(-3).HasPoint(Lp)) continue;
            Color Lc = KindColour(L.Kind);
            if (Big)
            {
                Ci.DrawCircle(Lp, 8, new Color("142832"));
                Ci.DrawArc(Lp, 8, 0, Mathf.Tau, 20, Lc, 1.4f);
                this.Text(C, Lp + new Vector2(-10, 3.5f), L.Id.ToString("00"), 9, Lc, 1, 20);
            }
            else
            {
                Ci.DrawCircle(Lp, 3, new Color("142832"));
                Ci.DrawArc(Lp, 3, 0, Mathf.Tau, 10, Lc, 1);
            }
        }
        // towers
        foreach (dynamic Tw in Gd.Iter(Gd.Get(Snap, "tw", new GArray())))
        {
            if (!Inside.HasPoint(To(Gd.At(Tw, 1)))) continue;
            Color Tc = (Gd.At(Tw, 2) > 0 && !Gd.Truth(Gd.At(Tw, 3))) ? new Color("F2B544") : new Color(0.45f, 0.45f, 0.5f);
            C.DrawRect(new Rect2(To(Gd.At(Tw, 1)) - Vector2.One * Dot / 2, Vector2.One * Dot), Tc);
        }
        // landing pads: always on the large map, and during the night 5 extension (FML-13)
        bool Extension = (Snap.K == 5 && Snap.Ph == "night");
        for (int I = 0; I < 3; I++)
        {
            Vector2 Pp = To(GameSim.PadPos(I));
            if (!Inside.HasPoint(Pp)) continue;
            if (Big) Ci.DrawArc(Pp, (float)GameSim.PAD_R * Scale, 0, Mathf.Tau, 20, new Color("9FD3DB"), 1);
            if (Extension) C.DrawArc(Pp, Big ? 6 : 3, 0, Mathf.Tau, 16, new Color(0.5f, 1.2f, 2.6f), 1);
        }
        // doors: steel, amber when damaged, red when broken; the attacked door blinks
        float Pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 150.0f);
        for (int I = 0; I < 3; I++)
        {
            float Dur = Gd.Float(Gd.At(Snap.Doors, I));
            Color Dc = Dur > 250 ? new Color("8FA3B8") : Dur > 0 ? new Color("F2B544") : new Color("E0362C");
            if (Gd.Contains(Snap.Active, I)) Dc = Dc.Lerp(new Color("E0362C"), Pulse);
            Vector2 Dp = To(Gd.At(GameSim.DOOR_POS, I));
            if (!Inside.HasPoint(Dp)) continue;
            C.DrawRect(new Rect2(Dp - Vector2.One * (Dot / 2 + 1), Vector2.One * (Dot + 2)), Dc);
        }
        // teammates, then you on top with a pulsing ring
        foreach (dynamic Id in Gd.Iter(Snap.Ps))
        {
            if (Id == MyId) continue;
            dynamic P = Gd.At(Snap.Ps, Id);
            if (Gd.At(P, 5) == 2) continue;
            Color Pc = (Color)Gd.At(ROLE_COL, Gd.At(P, 1));
            if (Gd.At(P, 5) != 0) Pc = Pc.Darkened(0.5f);
            Vector2 Pp = To(Gd.Get(ShownP, Id, Gd.At(P, 2)));
            if (!Inside.HasPoint(Pp)) continue;
            C.DrawRect(new Rect2(Pp - Vector2.One * (Dot / 2 + 1), Vector2.One * (Dot + 2)), INK);
            C.DrawRect(new Rect2(Pp - Vector2.One * (Dot / 2), Vector2.One * Dot), Pc);
        }
        if (Me != null)
        {
            Vector2 Mp = To(LocalPos);
            C.DrawCircle(Mp, Dot + 1.5f + Pulse * 1.5f, new Color(1, 1, 1, 0.25f));
            C.DrawRect(new Rect2(Mp - Vector2.One * (Dot / 2 + 2), Vector2.One * (Dot + 4)), Colors.White);
            C.DrawRect(new Rect2(Mp - Vector2.One * (Dot / 2 + 1), Vector2.One * (Dot + 2)), (Color)Gd.At(ROLE_COL, Gd.At(Me, 1)));
        }
        if (Big)
        {
            this.Text(C, R.Position + new Vector2(0, -10), "ISLAND MAP  (M to close)", 12, GOLD, 1, R.Size.X, true);
            this.DrawMapKey(C, R);
        }
        else
        {
            this.Text(C, R.Position + new Vector2(3, 10), "M", 10, new Color(1, 1, 1, 0.6f), 0, -1.0f, true);
            this.DrawHereName(C, Vs);
        }
    }

    // beside the large map: the landmark list and what the lines mean
    void DrawMapKey(dynamic C, Rect2 R)
    {
        CanvasItem Ci = (CanvasItem)Gd.Unwrap(C);
        var Box = new Rect2(R.End.X + 14, R.Position.Y - 4, 236, R.Size.Y + 8);
        this.Panel(C, Box);
        float X = Box.Position.X + 10, Y = Box.Position.Y + 16;
        this.Text(C, new Vector2(X, Y), "LANDMARKS", 11, GOLD, 0, -1.0f, true);
        Y += 15;
        foreach (var L in IslandMap.Landmarks)
        {
            Color Lc = KindColour(L.Kind);
            Ci.DrawArc(new Vector2(X + 6, Y - 4), 6, 0, Mathf.Tau, 14, Lc, 1);
            this.Text(C, new Vector2(X - 4, Y - 1), L.Id.ToString("00"), 7, Lc, 1, 20);
            this.Text(C, new Vector2(X + 16, Y), L.Name, 9, Colors.White);
            this.Text(C, new Vector2(X + 16, Y), L.What, 8, new Color(1, 1, 1, 0.55f), 2, Box.Size.X - 36);
            Y += 13.2f;
        }
        Y += 4;
        Ci.DrawLine(new Vector2(X, Y - 8), new Vector2(Box.End.X - 10, Y - 8), new Color(1, 1, 1, 0.15f), 1);
        void Key(string Label, Action<Vector2> Swatch)
        {
            Swatch(new Vector2(X, Y - 4));
            this.Text(C, new Vector2(X + 30, Y), Label, 8, new Color(1, 1, 1, 0.8f));
            Y += 12;
        }
        Key("Main gathering route", P => Dashed(Ci, new List<Vector2> { P, P + new Vector2(22, 0) }, MAP_ROUTE, 1.6f, 5, 4));
        Key("Exploration trail", P => Dashed(Ci, new List<Vector2> { P, P + new Vector2(22, 0) }, MAP_TRAIL, 1, 2, 3));
        Key("Safe zone (20 tiles)", P => Dashed(Ci, new List<Vector2> { P, P + new Vector2(22, 0) }, new Color(0.88f, 0.91f, 0.65f), 1, 4, 4));
        Key("Night enemy spawn band", P => Ci.DrawRect(new Rect2(P + new Vector2(2, -4), new Vector2(18, 8)), new Color(MAP_BAND, 0.5f)));
        Key("Boss spawn and way in", P => Ci.DrawColoredPolygon(new[] { P + new Vector2(11, -5), P + new Vector2(16, 0), P + new Vector2(11, 5), P + new Vector2(6, 0) }, new Color("FFC4D4")));
    }

    // the name of the landmark you just walked into, for a few seconds under the radio line
    void DrawHereName(dynamic C, dynamic Vs)
    {
        var Here = IslandMap.Near((Vector2)LocalPos, 8.0f * IslandMap.SCALE * (float)T);
        if (Here != null && Here.Id == 1) Here = null;
        float Now = (float)Clock;
        if (Here != HereNow)
        {
            HereNow = Here;
            if (Here != null) HereSince = Now;
        }
        if (HereNow == null) return;
        float Age = Now - HereSince;
        if (Age > 4.0f) return;
        float A = Mathf.Clamp(Age / 0.3f, 0, 1) * Mathf.Clamp((4.0f - Age) / 0.8f, 0, 1);
        Color Lc = KindColour(HereNow.Kind);
        this.Text(C, new Vector2(0, 142), HereNow.Id.ToString("00") + "   " + HereNow.Name.ToUpper(), 15, new Color(Lc, A), 1, Vs.X, true);
        this.Text(C, new Vector2(0, 158), HereNow.What, 11, new Color(1, 1, 1, 0.75f * A), 1, Vs.X);
    }
}
