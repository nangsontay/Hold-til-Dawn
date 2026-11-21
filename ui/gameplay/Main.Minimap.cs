using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Mai Thien Ngon. Minimap (top left) so players do not get lost on the island: it shows the part of the
// island around you, and M shows the whole island large. Each landmark is a dot where its structure stands
// (WorldGeometry.LmAt), named in black on the large map, the labels kept apart from each other.
// The ground (sea and shallows, beach, grass, safe zone, woods, hills, pond, swamp, concrete) is drawn once into a
// pixel image; the roads, landmark dots, towers, doors, pads, teammates, you and the camera view are drawn on top
// every frame. Walking into a landmark's area shows its name at the top of the screen.
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

    static readonly Color MAP_INK = new Color("141210"), MAP_HALO = new Color(0.96f, 0.93f, 0.84f, 0.75f);

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
                    if (Terrain.CrossingTerrain(P) == 1) Col = new Color("396772");
                }
                Im.SetPixel(X, Y, Col);
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
            // 480 HUD units = 720 window pixels = 2 per pixel of the ground image, so it stays crisp
            float S = Mathf.Min(4 * MAP_PX, (float)Vs.Y - 40);
            return new Rect2(((float)Vs.X - S) / 2, ((float)Vs.Y - S) / 2, S, S);
        }
        // top left, where the day and timer were (they moved to the top right)
        return new Rect2(10, 10, MAP_PX, MAP_PX);
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
        this.Panel(C, R.Grow(4));
        float Texel = MAP_TEX / (2 * Reach);
        var Src = new Rect2((Focus - Vector2.One * View + Vector2.One * Reach) * Texel, Vector2.One * (2 * View * Texel));
        C.DrawTextureRectRegion(MapTex, R, Src);
        // the roads out of the three doors (clipped to the map)
        WorldGeometry W = (WorldGeometry)Geometry;
        List<Vector2> Clip(Vector2[] Path)
        {
            var Out = new List<Vector2>();
            foreach (var P in Path) Out.Add(To(P).Clamp(Inside.Position, Inside.End));
            return Out;
        }
        for (int I = 0; I < 3 && I < W.Trails.Count; I++) Ci.DrawPolyline(Clip(W.Trails[I].Path).ToArray(), new Color("8C7F5B"), Big ? 3 : 2);
        if (Big)
        {
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
        // landmarks: a dot where each one's structure stands, named on the large map
        this.DrawLandmarkDots(Ci, Inside, To, Big);
        foreach (GArray S in SupplyRows())
        {
            if (Gd.Truth(S[2])) continue;
            Vector2 Pos = To(S[1]);
            if (!Inside.Grow(-10).HasPoint(Pos)) continue;
            Ci.DrawCircle(Pos, Big ? 10 : 4, new Color("192027"));
            Ci.DrawArc(Pos, Big ? 10 : 4, 0, Mathf.Tau, 16, SUPPLY_COL, 2);
            if (Big) DrawComponentIcon(Ci, Gd.Int(S[0]), Pos, 1);
        }
        foreach (var Crossing in W.Crossings)
        {
            Vector2 Pos = To(Crossing.At);
            if (Inside.HasPoint(Pos)) Ci.DrawLine(Pos - Crossing.Along * 3, Pos + Crossing.Along * 3, new Color("C9A572"), Big ? 3 : 2);
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
            C.DrawRect(new Rect2(Pp - Vector2.One * (Dot / 2 + 2), Vector2.One * (Dot + 4)), INK);
            C.DrawRect(new Rect2(Pp - Vector2.One * (Dot / 2 + 1), Vector2.One * (Dot + 2)), Pc);
            if (Big) this.Text(C, Pp + new Vector2(-40, -6), Gd.Str(Gd.At(P, 0)), 8, Pc.Lightened(0.3f), 1, 80);
        }
        if (Me != null)
        {
            Vector2 Mp = To(LocalPos);
            C.DrawCircle(Mp, Dot + 1.5f + Pulse * 1.5f, new Color(1, 1, 1, 0.25f));
            C.DrawRect(new Rect2(Mp - Vector2.One * (Dot / 2 + 2), Vector2.One * (Dot + 4)), Colors.White);
            C.DrawRect(new Rect2(Mp - Vector2.One * (Dot / 2 + 1), Vector2.One * (Dot + 2)), (Color)Gd.At(ROLE_COL, Gd.At(Me, 1)));
        }
        if (!Big)
        {
            this.Text(C, R.Position + new Vector2(3, 10), "M", 10, new Color(1, 1, 1, 0.6f), 0, -1.0f, true);
            this.DrawHereName(C, Vs);
        }
    }

    // where each landmark's label sits on the large map, worked out once per map size
    readonly List<(Vector2 Dot, Rect2 Box, string Name)> LabelPlan = new();
    Rect2 LabelPlanFor;

    void DrawLandmarkDots(CanvasItem Ci, Rect2 Inside, Func<dynamic, Vector2> To, bool Big)
    {
        var W = (WorldGeometry)Geometry;
        float Dr = Big ? 3.5f : 2.0f;
        foreach (var L in IslandMap.Landmarks)
        {
            if (L.Id == 1 || !W.LmAt.TryGetValue(L.Id, out Vector2 At)) continue;
            Vector2 P = To(At);
            if (!Inside.Grow(-2).HasPoint(P)) continue;
            Ci.DrawCircle(P, Dr + 1.5f, MAP_HALO);
            Ci.DrawCircle(P, Dr, MAP_INK);
        }
        if (!Big) return;
        Font F = (Font)Gd.Unwrap(FontUi);
        const int SIZE = 10;
        if (LabelPlanFor != Inside || LabelPlan.Count == 0)
        {
            LabelPlanFor = Inside;
            LabelPlan.Clear();
            var Dots = new List<Rect2>();
            var Order = new List<(Vector2 P, string Name)>();
            foreach (var L in IslandMap.Landmarks)
            {
                if (L.Id == 1 || !W.LmAt.TryGetValue(L.Id, out Vector2 At)) continue;
                Vector2 P = To(At);
                Order.Add((P, L.Name));
                Dots.Add(new Rect2(P - Vector2.One * 5, Vector2.One * 10));
            }
            // the base sits in the middle: keep labels off it
            Dots.Add(new Rect2(To(Vector2.Zero) - Vector2.One * 14, Vector2.One * 28));
            var Placed = new List<Rect2>();
            foreach (var (P, Name) in Order)
            {
                Vector2 Sz = F.GetStringSize(Name, HorizontalAlignment.Left, -1, SIZE) + new Vector2(4, 0);
                float H = Sz.Y;
                // right, left, above, below, then the corners
                var Tries = new[]
                {
                    new Vector2(P.X + 7, P.Y - H / 2), new Vector2(P.X - 7 - Sz.X, P.Y - H / 2),
                    new Vector2(P.X - Sz.X / 2, P.Y - 6 - H), new Vector2(P.X - Sz.X / 2, P.Y + 6),
                    new Vector2(P.X + 5, P.Y - 4 - H), new Vector2(P.X - 5 - Sz.X, P.Y - 4 - H),
                    new Vector2(P.X + 5, P.Y + 4), new Vector2(P.X - 5 - Sz.X, P.Y + 4),
                };
                Rect2 Best = new Rect2(Tries[0], Sz);
                float BestCost = float.MaxValue;
                foreach (var T0 in Tries)
                {
                    var Box = new Rect2(T0, Sz);
                    float Cost = 0;
                    foreach (var O in Placed) if (O.Intersects(Box)) Cost += O.Intersection(Box).Area + 50;
                    foreach (var O in Dots) if (O.Intersects(Box)) Cost += O.Intersection(Box).Area + 20;
                    if (!Inside.Grow(-2).Encloses(Box)) Cost += 10000;
                    if (Cost < BestCost)
                    {
                        BestCost = Cost;
                        Best = Box;
                    }
                    if (Cost == 0) break;
                }
                Placed.Add(Best);
                LabelPlan.Add((P, Best, Name));
            }
        }
        foreach (var (_, Box, Name) in LabelPlan)
        {
            Vector2 Base = new Vector2(Box.Position.X + 2, Box.Position.Y + F.GetAscent(SIZE));
            Ci.DrawStringOutline(F, Base, Name, HorizontalAlignment.Left, -1, SIZE, 3, MAP_HALO);
            Ci.DrawString(F, Base, Name, HorizontalAlignment.Left, -1, SIZE, MAP_INK);
        }
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
        // under the radio line, which sits lower at night (the night bar)
        float Y = (Gd.Get(Snap, "ph", "") == "night") ? 116 + NIGHT_BAR_H : 142;
        this.Text(C, new Vector2(0, Y), HereNow.Name.ToUpper(), 15, new Color(Lc, A), 1, Vs.X, true);
    }
}
