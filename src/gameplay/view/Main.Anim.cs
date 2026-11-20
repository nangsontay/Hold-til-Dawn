using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Which one-shot sprite animation a player plays. The frames are in the character sheets
// (art/chars/*.png, drawn by tools/PixelPoses.cs; chars.json "anims" says where they are):
//   attack_fists  - bare hands: guard, jab, impact, back to guard
//   attack_spear  - stone spear: drawn back, thrust, full reach, recover
//   attack_blade  - metal blade: raised, slash across, follow-through, recover
//   attack_hammer - hammer: lifted overhead, coming down, smashed into the ground, recover
//   attack_bow    - starter bow and bow: nock, full draw, release, recover (the arrow is its own sprite)
//   down          - knocked down: hit, knees give, falling, lying still until revived
// An attack starts when GameSim's P.Swing does (MEC-02, MEC-03) and plays once.
public partial class Main : Node2D
{
    struct Swing
    {
        public float Start, Seen;
        public bool Any;
    }

    // per character: when its current swing was first and last seen
    readonly Dictionary<string, Swing> Swings = new();
    // per player: when they were first seen knocked down
    readonly Dictionary<string, float> DownSince = new();

    static readonly Dictionary<string, string> WEAPON_ANIM = new()
    {
        { "fists", "attack_fists" }, { "stone_spear", "attack_spear" }, { "metal_blade", "attack_blade" },
        { "hammer", "attack_hammer" }, { "starter_bow", "attack_bow" }, { "bow", "attack_bow" },
    };

    // The snapshot only says "swinging now", so a swing seen again after a gap is a new one.
    // Returns when the latest swing started, or -1 if none was seen yet.
    public float SwingStart(string Key, bool Flag)
    {
        float Now = (float)Clock;
        Swings.TryGetValue(Key, out var S);
        if (Flag)
        {
            if (!S.Any || ((Now - S.Seen) > 0.06f))
            {
                S.Start = Now;
                S.Any = true;
            }
            S.Seen = Now;
            Swings[Key] = S;
        }
        return (S.Any ? S.Start : -1.0f);
    }

    // seconds a one-shot animation of a sheet lasts (0 when the sheet has no animation of that name)
    public float AnimLength(string Skin, string Name)
    {
        dynamic C = Gd.Get(Cinfo.Chars, Skin, null);
        dynamic A = ((C == null) ? null : Gd.Get(Gd.Get(C, "anims", new GDict()), Name, null));
        return ((A == null) ? 0.0f : (Gd.Float(Gd.At(A, 1)) / Gd.Float(Gd.At(A, 2))));
    }

    // the attack of the weapon a player holds and how far into it they are (null when not attacking)
    public (string Name, float T)? PlayerAttack(dynamic Id, dynamic P, string Skin)
    {
        float Start = this.SwingStart(Gd.Str(Id), Gd.Truth(Gd.At(P, 11)));
        if (Start < 0)
        {
            return null;
        }
        string Weapon = Gd.Str(Gd.At(P, 8));
        string Name = (WEAPON_ANIM.TryGetValue(Weapon, out string N) ? N : "attack_fists");
        float T = ((float)Clock - Start);
        return ((T < this.AnimLength(Skin, Name)) ? (Name, T) : null);
    }

    // seconds since a player went down (the fall plays once, then they lie still); forgets them once they are up
    public float DownTime(dynamic Id, bool Down)
    {
        string Key = Gd.Str(Id);
        if (!Down)
        {
            DownSince.Remove(Key);
            return 0.0f;
        }
        if (!DownSince.TryGetValue(Key, out float Since))
        {
            Since = (float)Clock;
            DownSince[Key] = Since;
        }
        return ((float)Clock - Since);
    }
}
