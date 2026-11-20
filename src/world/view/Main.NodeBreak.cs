using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. A resource node breaks bit by bit as it is gathered instead of shrinking: each unit taken
// (5 to a node) swaps in the next broken stage of its sprite (art/nature/<node>_dmg<k>_<variant>.png, drawn by
// tools/PixelNature.cs: pieces knocked out, cracks, chips on the ground), the node jolts, and a burst of chips in
// its colours flies off (wood chips, stone shards, leaves, blue crystal). While someone works it, it shakes and a
// few small chips fly now and then. Once empty it shows its remains (stump, rubble, ...) until dawn.
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
    readonly Dictionary<Vector2, float> NodeChipAt = new();

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
        if (Amount <= 0)
        {
            return Gd.Str(this.Var(Gd.At(NODE_EMPTY, Type), Hsh));
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
        return Gd.Str(this.Var(Name, Hsh));
    }

    // Full size; it shakes while someone works it (and throws a small chip now and then) and jolts as a piece breaks off.
    public float NodeScale(dynamic Nd)
    {
        Vector2 Pos = (Vector2)Gd.At(Nd, 1);
        if (Gd.Int(Gd.At(Nd, 2)) <= 0)
        {
            return 1.0f;
        }
        bool Worked = false;
        foreach (dynamic Id in Gd.Iter(Snap.Ps))
        {
            dynamic P = Gd.At(Snap.Ps, Id);
            if (((Gd.At(P, 22) == "gather") && (((Vector2)Gd.At(P, 23)).DistanceTo(Pos) < 1.0f)))
            {
                Worked = true;
                break;
            }
        }
        float Now = (float)Clock;
        float S = 1.0f;
        if (Worked)
        {
            S *= (1.0f + (0.025f * Mathf.Sin((Now * 28.0f))));
            if ((Now - NodeChipAt.GetValueOrDefault(Pos, -9.0f)) > 0.32f)
            {
                NodeChipAt[Pos] = Now;
                this.BurstChips(Gd.Int(Gd.At(Nd, 0)), Pos, 2, 0.5f);
            }
        }
        if (NodeHitAt.TryGetValue(Pos, out float At))
        {
            float K = ((Now - At) / 0.2f);
            if (K < 1.0f)
            {
                S *= (1.0f + (0.08f * (1.0f - K)));
            }
        }
        return S;
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
                Size = ((GD.Randf() < 0.3f) ? 2.0f : 1.0f) * ((Power >= 1.0f) ? 2.0f : 1.0f),
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
