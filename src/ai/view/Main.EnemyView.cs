using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. How enemies and bosses look on the client: sprite choice, tint, health bars, boss bar.
// Sprite sheets in art/chars/ belong to Mai Thien Ngon; this file only maps enemy state to them.
public partial class Main : Node2D
{
    public static readonly dynamic ENEMY_SKIN = new GArray("grunt", "spitter", "wrecker", "wrecker");
    public static readonly dynamic ENEMY_TINT = new GArray(Colors.White, Colors.White, Colors.White, Gd.MakeColor(1.2f, 0.85f, 1.25f));

    public void DrawEnemy(dynamic E, dynamic Foot)
    {
        int Ty = Gd.Int(Gd.At(E, 1));
        float Size = Gd.Float(Gd.At(Cinfo.Chars, Gd.At(ENEMY_SKIN, Ty)).Size);
        // GDD size in tiles over the sprite's own size: the 40 px Wrecker is drawn 1:1
        float S = Gd.Float((((Gd.At(E, 5) / (0.5f * T)) * 32.0f) / Size));
        Color Mod = (Color)(Gd.At(ENEMY_TINT, Ty));
        if (Gd.Truth(Gd.Has(Flash, Gd.At(E, 0))))
        {
            Mod = Gd.MakeColor(2.0f, 2.0f, 2.0f);
        }
        this.Char(Gd.Format("e%d", Gd.At(E, 0)), Gd.At(ENEMY_SKIN, Ty), Foot, null, S, Mod);
        if (((Gd.At(E, 3) < Gd.At(E, 4)) && (Ty != GameSim.BOSS)))
        {
            this.Bar((Foot - Gd.MakeVector2(0, (((Size * 0.7f) * S) + 4))), (Gd.At(E, 5) * 2.2f), (Gd.At(E, 3) / Gd.At(E, 4)), NEON_RED);
        }
    }

    // --- HUD drawing (screen space) -----------------------------------------
    public void DrawBossBar(dynamic C, dynamic Cx)
    {
        foreach (dynamic E in Gd.Iter(Snap.En))
        {
            if ((Gd.At(E, 1) == GameSim.BOSS))
            {
                dynamic Br = Gd.MakeRect2((Cx - 230), 66, 460, 10);
                this.Text(C, Gd.MakeVector2((Cx - 230), 62), "NOX-01  JUGGERNAUT", 12, NEON_PINK, 1, 460, true);
                C.DrawRect(Br.Grow(2), INK);
                C.DrawRect(Gd.MakeRect2(Br.Position, Gd.MakeVector2(((Br.Size.X * Gd.At(E, 3)) / Gd.At(E, 4)), Br.Size.Y)), NEON_PINK);
                foreach (dynamic X in Gd.Iter(new GArray((Br.Position.X - 6), (Br.End.X + 6))))
                {
                    this.Diamond(C, Gd.MakeVector2(X, Br.GetCenter().Y), 8, NEON_PINK);
                }
            }
        }
    }
}
