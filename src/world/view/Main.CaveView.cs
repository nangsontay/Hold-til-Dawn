using Godot;

public partial class Main
{
    WorldGeometry CaveRoofWorld;
    float CaveRoofAlpha = 1, CaveRoofClock;
    public float CaveRoofOpacity => CaveRoofAlpha;
    public bool CaveInteriorActive { get; private set; }

    public bool HiddenOutsideCave(Vector2 P) => CaveInteriorActive && !((WorldGeometry)Geometry).InCaveFloor(P);

    void DrawCaveIsolation(WorldGeometry W)
    {
        if (!CaveInteriorActive) return;
        foreach (var Mask in W.CaveExteriorMasks) FillStatic(Mask, new Color("080C10"), false);
    }

    void DrawCaveRoof(WorldGeometry W, Rect2 View)
    {
        if (!ReferenceEquals(CaveRoofWorld, W)) { CaveRoofWorld = W; CaveRoofAlpha = 1; CaveRoofClock = (float)Clock; }
        float Now = (float)Clock;
        float Dt = Mathf.Clamp(Now - CaveRoofClock, 0, .1f); CaveRoofClock = Now;
        Vector2 Player = (Vector2)LocalPos;
        bool Inside = W.IsCaveInterior(Player);
        CaveRoofAlpha = Mathf.MoveToward(CaveRoofAlpha, Inside ? 0 : 1, Dt * 4);
        if (CaveRoofAlpha <= 0 || !new Rect2(W.CaveCentre - WorldGeometry.CaveRadii, WorldGeometry.CaveRadii * 2).Intersects(View)) return;
        int I = 0;
        foreach (var Facet in W.CaveRoofFacets)
        {
            Vector2 Centre = Vector2.Zero;
            foreach (var Point in Facet) Centre += Point;
            Centre /= Facet.Length;
            float Light = Mathf.Clamp((W.CaveCentre.Y - Centre.Y) / (22 * 32), -.25f, .25f);
            Color Rock = new Color("7F8475").Lightened(Light + (I % 5 - 2) * .015f);
            Rock.A = CaveRoofAlpha;
            FillStatic(Facet, Rock); I++;
        }
        Color Seam = new("4D594E", CaveRoofAlpha * .6f);
        foreach (var Band in W.CaveRoofSeams) DrawPolyline(Band, Seam, 2);
        for (int K = 1; K < W.CaveRoof.Length; K += 3)
        {
            Vector2 Edge = W.CaveRoof[K], In = Edge.Lerp(W.CaveCentre, .32f);
            DrawLine(Edge, In + new Vector2(9, -8), new Color("4B574B", CaveRoofAlpha * .75f), 2);
            DrawLine(In + new Vector2(9, -8), In + new Vector2(-4, 20), new Color("4B574B", CaveRoofAlpha * .75f), 2);
        }
    }

    void DrawCaveWalls(WorldGeometry W, Rect2 View)
    {
        if (!new Rect2(W.CaveCentre - WorldGeometry.CaveRadii, WorldGeometry.CaveRadii * 2).Intersects(View)) return;
        int I = 0;
        foreach (var (Face, Cap, Facet, Strata, Crack) in W.CaveWalls)
        {
            if (!Bounds(Cap, 80).Intersects(View)) { I++; continue; }
            Color Side = new(I % 3 == 0 ? "52564F" : "5D6056");
            Color Top = new(I % 3 == 0 ? "8A8979" : I % 3 == 1 ? "77786D" : "9D9987");
            FillStatic(Face, Side);
            FillStatic(Cap, Top);
            FillStatic(Facet, Top.Lightened(.08f));
            DrawPolyline(Strata, Side.Darkened(.22f), 2);
            DrawPolyline(Crack, new Color("464C43"), 2);
            DrawLine(Face[4], Face[5], new Color("3A4239"), 2);
            I++;
        }
        for (int Side = -1; Side <= 1; Side += 2)
        {
            Vector2 P = W.CaveEntrance + new Vector2(0, Side * 2.4f * 32);
            DrawLine(P + new Vector2(-10, -49), P + new Vector2(12, -4), new Color("B1AA8D"), 3);
        }
    }
}
