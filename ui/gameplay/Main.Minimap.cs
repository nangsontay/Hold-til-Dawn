using Godot;
using System;
using HoldTilDawn;

// Owner: Mai Thien Ngon. Minimap (bottom right) so players do not get lost on the island: it shows the half of the
// island around you, and M shows the whole island large.
// The terrain (sea, outer zone, safe zone, base, streams, plateaus, trails, forest and rocks) is drawn once into a small pixel image;
// nodes, towers, doors, pads, teammates, you and the camera view are drawn on top every frame.
// Enemies are not shown: the GDD only shows what players can see (section 6.1).
public partial class Main : Node2D
{
    public const int MAP_PX = 120;
    public dynamic MapBig = false;
    // the terrain image, drawn with hard pixels on the linearly filtered HUD
    CanvasTexture MapTex = null;

    // world half-width covered by the map: the island plus a strip of sea
    public static float MapReach() => (float)GameSim.ISLAND + 3.0f * (float)T;

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

    void BuildMapTexture()
    {
        var Im = Image.CreateEmpty(MAP_PX, MAP_PX, false, Image.Format.Rgba8);
        float Reach = MapReach();
        float Per = 2 * Reach / MAP_PX;
        Vector2 Half = (Vector2)GameSim.HALF;
        float Island = (float)GameSim.ISLAND, Safe = (float)GameSim.SAFE_D, Tile = (float)T;
        Color Sea = new Color("14283A"), Coast = new Color("1E3A4C");
        Color Outer = new Color("4E5E3C"), Inner = new Color("67784C");
        Color Plaza = new Color("8A8F98"), Floor = new Color("5F6470"), Wall = new Color("3A3E48");
        WorldGeometry Terrain = (WorldGeometry)Geometry;
        for (int Y = 0; Y < MAP_PX; Y++)
            for (int X = 0; X < MAP_PX; X++)
            {
                var P = new Vector2((X + 0.5f) * Per - Reach, (Y + 0.5f) * Per - Reach);
                float D = P.Length();
                Color Col;
                if (D > Island) Col = D < Island + Per ? Coast : Sea;
                else if (Mathf.Abs(P.X) < Half.X && Mathf.Abs(P.Y) < Half.Y)
                    Col = (Mathf.Abs(P.X) > Half.X - Per || Mathf.Abs(P.Y) > Half.Y - Per) ? Wall : Floor;
                else if (Mathf.Abs(P.X) < Half.X + 2 * Tile && Mathf.Abs(P.Y) < Half.Y + 2 * Tile) Col = Plaza;
                else
                {
                    Col = (float)WorldGeometry.WallDist(P) < Safe ? Inner : Outer;
                    // streams, bridges, cliffs, plateau tops, trails and ramps (src/world/WorldTerrain.cs)
                    switch (Terrain.TerrainAt(P))
                    {
                        case 1: Col = new Color("3B6A86"); break;
                        case 2: Col = new Color("8A6440"); break;
                        case 3: Col = new Color("4F4A42"); break;
                        case 4: Col = new Color("7C8D58"); break;
                        case 5: Col = new Color("8C7F5B"); break;
                        case 6: Col = new Color("958060"); break;
                    }
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
            else continue;
            Vector2 P = (Vector2)Gd.At(D, 0);
            int Px = (int)((P.X + Reach) / Per), Py = (int)((P.Y + Reach) / Per);
            if (Px >= 0 && Py >= 0 && Px < MAP_PX && Py < MAP_PX) Im.SetPixel(Px, Py, Col);
        }
        MapTex = new CanvasTexture { DiffuseTexture = ImageTexture.CreateFromImage(Im), TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
    }

    public Rect2 MapRect(dynamic Vs)
    {
        if (Gd.Truth(MapBig))
        {
            float S = Mathf.Floor(Mathf.Min((float)Vs.Y - 120, 3 * MAP_PX) / MAP_PX) * MAP_PX;
            return new Rect2(((float)Vs.X - S) / 2, ((float)Vs.Y - S) / 2 - 10, S, S);
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
        Rect2 R = this.MapRect(Vs);
        bool Big = Gd.Truth(MapBig);
        // the small map follows you at twice the zoom, kept inside the island's square
        float Reach = MapReach();
        float View = Big ? Reach : Reach / 2;
        Vector2 Focus = Big ? Vector2.Zero : ((Vector2)LocalPos).Clamp(-Vector2.One * (Reach - View), Vector2.One * (Reach - View));
        float Scale = R.Size.X / (2 * View);
        Vector2 Mid = R.GetCenter();
        Vector2 To(dynamic P) => (Mid + ((Vector2)P - Focus) * Scale).Round();
        Rect2 Inside = R.Grow(-1);
        float Dot = Big ? 3 : 2;
        this.Panel(C, R.Grow(4));
        float Texel = MAP_PX / (2 * Reach);
        var Src = new Rect2((Focus - Vector2.One * View + Vector2.One * Reach) * Texel, Vector2.One * (2 * View * Texel));
        C.DrawTextureRectRegion(MapTex, R, Src);
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
        // towers
        foreach (dynamic Tw in Gd.Iter(Gd.Get(Snap, "tw", new GArray())))
        {
            if (!Inside.HasPoint(To(Gd.At(Tw, 1)))) continue;
            Color Tc = (Gd.At(Tw, 2) > 0 && !Gd.Truth(Gd.At(Tw, 3))) ? new Color("F2B544") : new Color(0.45f, 0.45f, 0.5f);
            C.DrawRect(new Rect2(To(Gd.At(Tw, 1)) - Vector2.One * Dot / 2, Vector2.One * Dot), Tc);
        }
        // landing pads during the night 5 extension (FML-13)
        if (Snap.K == 5 && Snap.Ph == "night")
        {
            for (int I = 0; I < 3; I++)
                if (Inside.HasPoint(To(GameSim.PadPos(I))))
                    C.DrawArc(To(GameSim.PadPos(I)), Big ? 6 : 3, 0, Mathf.Tau, 16, new Color(0.5f, 1.2f, 2.6f), 1);
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
            if (Big)
            {
                Vector2 Out = ((Vector2)Gd.At(GameSim.DOOR_DIR, I)) * 12;
                this.Text(C, Dp + Out + new Vector2(-10, 4), Gd.At(GameSim.DOOR_NAMES, I), 11, Colors.White, 1, 20, true);
            }
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
            this.Text(C, R.Position + new Vector2(0, -12), "MAP  (M to close)", 12, GOLD, 1, R.Size.X, true);
            this.Text(C, R.End + new Vector2(-R.Size.X, 16), "white = you   dots = teammates   squares by the base = doors A B C", 10, new Color(1, 1, 1, 0.7f), 1, R.Size.X);
        }
        else
        {
            this.Text(C, R.Position + new Vector2(3, 10), "M", 10, new Color(1, 1, 1, 0.6f), 0, -1.0f, true);
        }
    }
}
