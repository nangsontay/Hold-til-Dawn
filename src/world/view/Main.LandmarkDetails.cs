using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Ground colour and quiet wildlife distinguish Atlas locations at gameplay scale.
// Visuals only: no extra lights, loot, damage or changes to the navigation mesh.
public partial class Main
{
    const double LandmarkGroundBudgetMs = 1.0;
    sealed class LandmarkSoilJob
    {
        public int Id, X, Y, MinX, MaxX, MaxY;
        public float Rx, Ry;
        public Vector2 Centre;
        public Rect2 Area;
        public Color Soil, Fleck;
        public Image Image;
        public ImageTexture Texture;
        public LandmarkGroundMask Mask;
        public WorldGeometry.LandmarkSoil Surface;
        public long WorkTicks;
        public int Slices;
    }

    WorldGeometry LandmarkSoilWorld;
    readonly Dictionary<int, LandmarkSoilJob> LandmarkSoilCache = new();
    readonly Queue<LandmarkSoilJob> LandmarkSoilJobs = new();
    readonly bool ProfileLandmarks = Array.IndexOf(OS.GetCmdlineUserArgs(), "--profile-landmarks") >= 0;
    public bool HasPendingLandmarkGround => LandmarkSoilJobs.Count > 0;
    public double LandmarkGroundWorstSliceMs { get; private set; }

    void DrawLandmarkSoils(WorldGeometry W, Rect2 View)
    {
        if (!ReferenceEquals(LandmarkSoilWorld, W))
        {
            foreach (var Job in LandmarkSoilCache.Values)
            {
                Job.Image?.Dispose();
                Job.Texture?.Dispose();
            }
            LandmarkSoilCache.Clear();
            LandmarkSoilJobs.Clear();
            LandmarkSoilWorld = W;
        }
        foreach (var Surface in WorldGeometry.LandmarkSoils)
        {
            var (Id, Rx, Ry, Hex, _) = Surface;
            if (!W.LmAt.ContainsKey(Id)) continue;
            Vector2 C = W.SoilCentre(Surface);
            if (!View.Grow(Rx * 32).HasPoint(C)) continue;
            if (!LandmarkSoilCache.TryGetValue(Id, out var Job))
            {
                // Queue work while the location is still in the camera's off-screen culling margin.
                // Never scan terrain, paint an image or upload a texture from _Draw().
                Vector2 Origin = (C - new Vector2(Rx * 32, Ry * 32)).Floor();
                int Bottom = Id is 5 or 7 ? Math.Max((int)(Ry * 32), 112) : (int)(Ry * 32);
                Job = new LandmarkSoilJob {
                    Id = Id, Centre = C, Rx = Rx, Ry = Ry, Soil = new Color(Hex), Surface = Surface,
                    Fleck = new Color(new Color(Hex).Lightened(0.15f), 0.7f),
                    MinX = -(int)(Rx * 32), X = -(int)(Rx * 32), MaxX = (int)(Rx * 32),
                    Y = -(int)(Ry * 32), MaxY = (int)(Ry * 32),
                    Area = new Rect2(Origin, new Vector2((int)(Rx * 64) + 8, Bottom + (int)(Ry * 32) + 8))
                };
                LandmarkSoilCache[Id] = Job;
                LandmarkSoilJobs.Enqueue(Job);
            }
            if (Job.Texture != null) DrawTextureRect(Job.Texture, Job.Area, false);
        }
    }

    // All Godot image/texture operations remain on the main thread, with a small CPU budget per rendered frame.
    public void TickLandmarkGround()
    {
        if (LandmarkSoilJobs.Count == 0) return;
        long Started = System.Diagnostics.Stopwatch.GetTimestamp();
        long Limit = Started + (long)(System.Diagnostics.Stopwatch.Frequency * LandmarkGroundBudgetMs / 1000);
        var Job = LandmarkSoilJobs.Peek();
        Job.Mask ??= new LandmarkGroundMask(LandmarkSoilWorld, Job.Area);
        Job.Image ??= Image.CreateEmpty((int)Job.Area.Size.X, (int)Job.Area.Size.Y, false, Image.Format.Rgba8);
        do
        {
            if (Job.Y <= Job.MaxY)
            {
                PaintLandmarkCell(Job);
                Job.X += 4;
                if (Job.X > Job.MaxX) { Job.X = Job.MinX; Job.Y += 4; }
            }
            else
            {
                Job.Texture = ImageTexture.CreateFromImage(Job.Image);
                Job.Image.Dispose();
                Job.Image = null;
                LandmarkSoilJobs.Dequeue();
                break;
            }
        } while (System.Diagnostics.Stopwatch.GetTimestamp() < Limit);
        long Elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - Started;
        Job.WorkTicks += Elapsed;
        Job.Slices++;
        LandmarkGroundWorstSliceMs = Math.Max(LandmarkGroundWorstSliceMs, Elapsed * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (ProfileLandmarks && Job.Texture != null)
            GD.Print($"LANDMARK BAKE {Job.Id:00}: {Job.WorkTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F2} ms across {Job.Slices} frames; worst slice {LandmarkGroundWorstSliceMs:F2} ms");
    }

    void PaintLandmarkCell(LandmarkSoilJob Job)
    {
        Vector2 P = (Job.Centre + new Vector2(Job.X, Job.Y)).Round();
        float Coverage = WorldGeometry.SoilCoverage(Job.Surface, P - Job.Centre);
        float Noise = H01((int)P.X, (int)P.Y);
        if (Coverage <= 0 || !Job.Mask.CanPaint(P, 3, AllowBeach: true, PreserveTrails: false)) return;
        Color Base = Job.Id == 16 ? LandmarkSoilWorld.CaveGroundColour(P) : Job.Soil;
        Color Patch = Noise > .72f ? Base.Lightened(.035f) : Noise < .22f ? Base.Darkened(.035f) : Base;
        Patch.A = Coverage;
        FillLandmark(Job, P, new Vector2I(4, 4), Patch);
        if (Noise > 0.92f) FillLandmark(Job, P + new Vector2(1, 2), new Vector2I(2, 1), new Color(Job.Fleck, Job.Fleck.A * Coverage));
        if (Job.Id == 16 && Noise < .025f)
            FillLandmark(Job, P, new Vector2I(4, 1), new Color(Base.Darkened(.18f), Coverage));
    }

    static void FillLandmark(LandmarkSoilJob Job, Vector2 P, Vector2I Size, Color Colour)
    {
        Job.Image.FillRect(new Rect2I((Vector2I)(P - Job.Area.Position).Round(), Size), Colour);
    }

    void DrawLandmarkLife(WorldGeometry W, Rect2 View, float Time)
    {
        bool Night = Gd.Str(Gd.Get(Snap, "ph", "day")) == "night";
        foreach (int Id in new[] { 4, 8, 15, 18 })
        {
            if (!W.LmAt.TryGetValue(Id, out Vector2 C) || !View.Grow(200).HasPoint(C)) continue;
            if (Id == 8) C = W.SwampCentre;
            if (Id == 15 && W.Ponds.Count > 0) C = W.Ponds[0].Centre;
            for (int I = 0; I < (Id == 8 ? 12 : 6); I++)
            {
                float A = Time * 0.24f + I * 2.4f;
                Vector2 P = (C + new Vector2(MathF.Sin(A) * (45 + I * 7), MathF.Cos(A * 1.3f) * (28 + I * 5))).Round();
                if (!View.HasPoint(P)) continue;
                if (Night || Id == 8)
                {
                    float Alpha = 0.25f + 0.65f * MathF.Pow(0.5f + 0.5f * MathF.Sin(Time * 1.8f + I * 1.9f), 3);
                    // HDR points remain visible through the world's night tint, without casting gameplay light.
                    Color Glow = Night ? new Color(4.2f, 3.8f, 1.5f, Alpha) : new Color("D4DE87", Alpha);
                    DrawRect(new Rect2(P - Vector2.One * 2, new Vector2(5, 5)), new Color(Glow, Alpha * 0.07f));
                    DrawRect(new Rect2(P, Vector2.One), Glow);
                }
                else
                {
                    int Wing = MathF.Sin(Time * 10 + I) > 0 ? 2 : 1;
                    Color Col = new Color(Id == 15 ? "9DD5D5" : I % 2 == 0 ? "E3BE74" : "DAB4D9", 0.85f);
                    DrawRect(new Rect2(P + new Vector2(-Wing, -1), new Vector2(Wing, 2)), Col);
                    DrawRect(new Rect2(P + new Vector2(1, -1), new Vector2(Wing, 2)), Col);
                    DrawRect(new Rect2(P, new Vector2(1, 2)), new Color("4A4B34"));
                }
            }
        }
    }
}
