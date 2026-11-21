using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. A resource node breaks bit by bit as it is gathered instead of shrinking: each unit taken
// (5 to a node) swaps in the next broken stage of its sprite (art/nature/<node>_dmg<k>_<variant>.png, drawn by
// tools/PixelHarvestDamage.cs: trunk notches, rock fractures, cut stems), the node jolts, and a burst of chips in
// its colours flies off (wood chips, stone shards, leaves, blue crystal). Each tool contact triggers a short
// highlight and a rigid jolt; five small slots show resource/progress on the worked node only.
// Once empty it shows its remains (stump, rubble, ...) until dawn.
public partial class Main : Node2D
{
    const int NODE_FULL = 5;
    const int NODE_STAGES = 4;

    struct Chip
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Floor;
        public float Age;
        public float Life;
        public float Size;
        public Color Col;
    }

    readonly List<Chip> Chips = new();
    float ChipClock;
    // what each node held last frame, when it last lost a unit, when it last threw a chip while worked
    readonly Dictionary<Vector2, int> NodeSeen = new();
    readonly Dictionary<Vector2, float> NodeHitAt = new();
    readonly Dictionary<Vector2, float> WorkedNodes = new();
    readonly Dictionary<string, (Vector2 Target, float Start, int Cycle)> HarvestStrikes = new();
    readonly Dictionary<Vector2, int> VisibleNodeAmounts = new();
    readonly Dictionary<Vector2, string> WorkedSprites = new();

    // chip colours per node type: wood, stone, herb, titanium
    static readonly Color[][] CHIP_COLS =
    {
        new[] { new Color("E0B070"), new Color("C08A50"), new Color("7A5634"), new Color("3A6B45") },
        new[] { new Color("D5DAE1"), new Color("A3A8B0"), new Color("7D838C") },
        new[] { new Color("9BE37A"), new Color("4FB862"), new Color("2E7D3E") },
        new[] { new Color("D2E6FF"), new Color("7FB0FF"), new Color("4A72E6"), new Color("A98CFF"), new Color("3D415C") },
    };
    // how high above its foot a node is broken into
    static readonly float[] CHIP_FROM = { 8.0f, 12.0f, 6.0f, 14.0f };

    // the sprite for a node as it stands now; also notices a unit taken since the last frame
    public string NodeSprite(dynamic Nd, int Hsh)
    {
        int Type = Gd.Int(Gd.At(Nd, 0));
        Vector2 Pos = (Vector2)Gd.At(Nd, 1);
        int Amount = Gd.Int(Gd.At(Nd, 2));
        if (NodeSeen.TryGetValue(Pos, out int Was) && (Amount < Was))
        {
            this.BurstChips(Type, Pos, ((Amount <= 0) ? 18 : 10), 1.0f);
            NodeHitAt[Pos] = (float)Clock;
        }
        NodeSeen[Pos] = Amount;
        VisibleNodeAmounts[Pos] = Amount;
        string Custom = Gd.Size(Nd) > 3 ? Gd.Str(Gd.At(Nd, 3)) : "";
        if (Custom.Length > 0)
        {
            string Sprite = Amount <= 0 ? Harvestables.StageSprite(Custom, 5) : Amount < NODE_FULL ? Harvestables.StageSprite(Custom, NODE_FULL - Amount) : Custom;
            if (Gd.Has(Spr, Sprite)) return RememberWorkedSprite(Pos, Sprite);
        }
        if (Amount <= 0)
        {
            return RememberWorkedSprite(Pos, Gd.Str(this.Var(Gd.At(NODE_EMPTY, Type), Hsh)));
        }
        string Name = Gd.Str(Gd.At(NODE_SPR, Type));
        int Stage = Mathf.Clamp((NODE_FULL - Amount), 0, NODE_STAGES);
        if (Stage > 0)
        {
            string Broken = (Name + "_dmg" + Stage);
            if (Gd.Int(Gd.Get(Nvar, Broken, 0)) > 0)
            {
                Name = Broken;
            }
        }
        return RememberWorkedSprite(Pos, Gd.Str(this.Var(Name, Hsh)));
    }

    string RememberWorkedSprite(Vector2 Pos, string Name)
    {
        if (WorkedNodes.ContainsKey(Pos)) WorkedSprites[Pos] = Name;
        return Name;
    }

    // Once per frame, time impacts to the contact frame of each character's harvesting animation.
    public void PrepareHarvestFeedback()
    {
        WorkedNodes.Clear();
        VisibleNodeAmounts.Clear();
        WorkedSprites.Clear();
        foreach (dynamic Id in Gd.Iter(Snap.Ps))
        {
            dynamic P = Gd.At(Snap.Ps, Id);
            if (Gd.Str(Gd.At(P, 22)) != "gather") continue;
            Vector2 Pos = (Vector2)Gd.At(P, 23);
            WorkedNodes[Pos] = Math.Max(WorkedNodes.GetValueOrDefault(Pos), Gd.Float(Gd.At(P, 21)));
            dynamic Timing = Gd.Get(AnimState, Id);
            if (Timing == null || !Gd.Str(Gd.At(Timing, 0)).StartsWith("gather_")) continue;
            float Start = Gd.Float(Gd.At(Timing, 1));
            float Time = Math.Max(0, (float)Clock - Start);
            string Skin = Gd.Str(Gd.At(ROLE_SKIN, Gd.At(P, 1)));
            dynamic Anim = Gd.At(Gd.At(Cinfo.Chars, Skin).Anims, Gd.At(Timing, 0));
            int Count = Gd.Int(Gd.At(Anim, 1));
            int Frame = (int)(Time * Gd.Float(Gd.At(Anim, 2))), Cycle = Frame / Count;
            if (Frame % Count < 3) continue;
            string Key = Gd.Str(Id);
            if (HarvestStrikes.TryGetValue(Key, out var Last) && Last.Target == Pos && Last.Start == Start && Last.Cycle == Cycle) continue;
            HarvestStrikes[Key] = (Pos, Start, Cycle);
            if (!HarvestTypes.TryGetValue(Pos, out int Type)) continue;
            NodeHitAt[Pos] = (float)Clock;
            BurstChips(Type, Pos, Type == 2 ? 6 : 8, 0.65f);
        }
    }

    public float NodeScale(dynamic Nd) => 1.0f;

    public Color NodeModulate(dynamic Nd)
    {
        Vector2 Pos = (Vector2)Gd.At(Nd, 1);
        float Age = (float)Clock - NodeHitAt.GetValueOrDefault(Pos, -9);
        float Light = 1 + 0.38f * Mathf.Clamp(1 - Age / 0.13f, 0, 1);
        return new Color(Light, Light, Light, 1);
    }

    public Vector2 NodeOffset(dynamic Nd)
    {
        Vector2 Pos = (Vector2)Gd.At(Nd, 1);
        if (NodeHitAt.TryGetValue(Pos, out float At) && (float)Clock - At < 0.2f)
            return new Vector2(MathF.Sin(((float)Clock - At) * 65) * 2 * (1 - ((float)Clock - At) / 0.2f), 0).Round();
        return Vector2.Zero;
    }

    public void DrawHarvestFeedback()
    {
        foreach (var (Pos, Progress) in WorkedNodes)
        {
            if (!VisibleNodeAmounts.TryGetValue(Pos, out int Amount) || Amount <= 0 || HiddenInBase(Pos)) continue;
            // Five small slots show units left; the next slot empties while E is held.
            if (!WorkedSprites.TryGetValue(Pos, out string Sprite)) continue;
            Rect2 Bounds = (Rect2)SpriteRect(Sprite, Pos, 1.0f);
            Vector2 At = new Vector2(Pos.X - 16, Bounds.Position.Y - 10).Round();
            DrawRect(new Rect2(At - Vector2.One * 2, new Vector2(36, 8)), new Color("141019"));
            Color Col = CHIP_COLS[HarvestTypes.GetValueOrDefault(Pos)][0];
            for (int I = 0; I < NODE_FULL; I++)
            {
                Vector2 Slot = At + new Vector2(I * 7, 0);
                DrawRect(new Rect2(Slot, new Vector2(5, 4)), new Color("41464B"));
                float Fill = I < Amount - 1 ? 1 : I == Amount - 1 ? 1 - Mathf.Clamp(Progress, 0, 1) : 0;
                if (Fill > 0) DrawRect(new Rect2(Slot, new Vector2(Math.Max(1, Mathf.Ceil(5 * Fill)), 4)), Col);
            }
        }
    }

    void BurstChips(int Type, Vector2 Foot, int N, float Power)
    {
        Color[] Cols = CHIP_COLS[Mathf.Clamp(Type, 0, (CHIP_COLS.Length - 1))];
        float Up = CHIP_FROM[Mathf.Clamp(Type, 0, (CHIP_FROM.Length - 1))];
        for (int I = 0; I < N; I++)
        {
            float Ang = Mathf.Lerp((-Mathf.Pi * 0.95f), (-Mathf.Pi * 0.05f), GD.Randf());
            float Speed = (Mathf.Lerp(50.0f, 120.0f, GD.Randf()) * Power);
            Chips.Add(new Chip
            {
                Pos = (Foot + new Vector2(Mathf.Lerp(-8.0f, 8.0f, GD.Randf()), -Up * Mathf.Lerp(0.6f, 1.2f, GD.Randf()))),
                Vel = (Vector2.FromAngle(Ang) * Speed),
                // where it lands: around the foot, a little in front
                Floor = (Foot.Y + Mathf.Lerp(-4.0f, 8.0f, GD.Randf())),
                Age = 0.0f,
                Life = Mathf.Lerp(0.6f, 1.1f, GD.Randf()),
                Size = GD.Randf() < 0.3f ? 3 : 2,
                Col = Cols[(int)(GD.Randi() % (uint)Cols.Length)],
            });
        }
    }

    // call after the depth-sorted world items
    public void DrawChips()
    {
        float Now = (float)Clock;
        float Dt = Mathf.Clamp((Now - ChipClock), 0.0f, 0.1f);
        ChipClock = Now;
        for (int I = (Chips.Count - 1); I >= 0; I--)
        {
            Chip C = Chips[I];
            C.Age += Dt;
            if (C.Age >= C.Life)
            {
                Chips.RemoveAt(I);
                continue;
            }
            if (C.Pos.Y < C.Floor || C.Vel.Y < 0)
            {
                C.Vel.Y += (420.0f * Dt);
                C.Pos += (C.Vel * Dt);
            }
            if ((C.Pos.Y >= C.Floor) && (C.Vel.Y > 0))
            {
                // a small bounce, then it lies still
                C.Pos.Y = C.Floor;
                C.Vel = ((Mathf.Abs(C.Vel.Y) > 60.0f) ? new Vector2((C.Vel.X * 0.4f), (-C.Vel.Y * 0.3f)) : Vector2.Zero);
            }
            Chips[I] = C;
            float A = Mathf.Clamp(((C.Life - C.Age) / 0.25f), 0.0f, 1.0f);
            var Col = C.Col;
            Col.A = A;
            DrawRect(new Rect2(C.Pos.Round(), new Vector2(C.Size, C.Size)), Col);
        }
    }
}
