using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Ground colour and quiet wildlife distinguish Atlas locations at gameplay scale.
// Visuals only: no extra lights, loot, damage or changes to the navigation mesh.
public partial class Main
{
    const double LandmarkGroundBudgetMs = 1.0;
    static readonly (int Id, float Rx, float Ry, string Hex)[] LandmarkSoils = {
        (2, 5.7f, 3.7f, "A39761"), (3, 6.5f, 4.1f, "B7B098"),
        (4, 7.0f, 4.7f, "77965B"), (5, 3.5f, 2.3f, "897C59"),
        (7, 5.5f, 3.7f, "736347"), (9, 4.7f, 3.5f, "9C9781"),
        (11, 4.3f, 3.0f, "AAA38A"), (15, 5.5f, 3.5f, "60804B"),
        (16, 4.9f, 2.5f, "B1A68B"), (18, 5.0f, 3.7f, "796347")
    };

    sealed class LandmarkSoilJob
    {
        public int Id, X, Y, MinX, MaxX, MaxY, Track;
        public float Rx, Ry;
        public Vector2 Centre;
        public Rect2 Area;
        public Color Soil, Fleck;
        public Image Image;
        public ImageTexture Texture;
        public LandmarkGroundMask Mask;
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
        foreach (var (Id, Rx, Ry, Hex) in LandmarkSoils)
        {
            if (!W.LmAt.TryGetValue(Id, out Vector2 C) || !View.Grow(Rx * 32).HasPoint(C)) continue;
            if (!LandmarkSoilCache.TryGetValue(Id, out var Job))
            {
                // Queue work while the location is still in the camera's off-screen culling margin.
                // Never scan terrain, paint an image or upload a texture from _Draw().
                Vector2 Origin = (C - new Vector2(Rx * 32, Ry * 32)).Floor();
                int Bottom = Id is 5 or 7 ? Math.Max((int)(Ry * 32), 112) : (int)(Ry * 32);
                Job = new LandmarkSoilJob {
                    Id = Id, Centre = C, Rx = Rx, Ry = Ry, Soil = new Color(Hex),
                    Fleck = new Color(new Color(Hex).Lightened(0.15f), 0.35f),
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
            else if (Job.Id is 5 or 7 && Job.Track < 28)
            {
                // Cache tyre marks too: their terrain queries used to run on every draw.
                int I = Job.Track / 2, Side = Job.Track % 2 == 0 ? -1 : 1;
                Vector2 P = (Job.Centre + new Vector2(Side * 19 + MathF.Sin(I * 0.12f) * 16, 25 + I * 6)).Round();
                if (Job.Mask.CanPaint(P, 4))
                    FillLandmark(Job, P, new Vector2I(5, 2), new Color("554A36", 0.38f));
                Job.Track++;
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
        float D = new Vector2(Job.X / (Job.Rx * 32), Job.Y / (Job.Ry * 32)).Length();
        float Noise = H01((int)P.X, (int)P.Y);
        if (D > 0.72f + 0.28f * Noise || !IslandMap.OnGrass(P, 12) || !Job.Mask.CanPaint(P, 8)) return;
        Color Patch = Job.Soil;
        Patch.A = (1 - D * 0.8f) * 0.34f;
        FillLandmark(Job, P, new Vector2I(4, 4), Patch);
        if (Noise > 0.92f) FillLandmark(Job, P + new Vector2(1, 2), new Vector2I(2, 1), Job.Fleck);
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
