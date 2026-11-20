using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. The shared storage chest in the base (BAL-18, GameSim.STORAGE_POS): its lid opens when
// anyone walks up to it (in reach to use it) and shuts again when they leave. art/props/chest.png holds the frames
// side by side, closed to wide open (drawn by tools/PixelProps.cs); props.json gives the anchor of one frame.
public partial class Main : Node2D
{
    const int CHEST_FRAMES = 4;
    // lid speed: full swing in 1 / this seconds
    const float CHEST_OPEN_SPEED = 5.0f;
    const float CHEST_SHUT_SPEED = 3.5f;
    // 0 shut, 1 wide open
    float ChestOpen;
    float ChestClock;

    // The lighthouse is tall (it stands above the roof, src/world/view/Main.BaseFog.cs): while you stand behind it, it
    // fades so you can see yourself.
    float LighthouseFade;
    float LighthouseClock;

    public Color LighthouseTint()
    {
        float Now = (float)Clock;
        float Dt = Mathf.Clamp((Now - LighthouseClock), 0.0f, 0.1f);
        LighthouseClock = Now;
        bool Behind = false;
        dynamic P = Gd.Get(Prop, "lighthouse");
        if (P != null)
        {
            Vector2 Size = (Vector2)(((Texture2D)Gd.Unwrap(Gd.At(P, 0))).GetSize() * (float)LIGHTHOUSE_SCALE);
            Vector2 Anchor = (Vector2)Gd.At(P, 1) * (float)LIGHTHOUSE_SCALE;
            Rect2 Body = new Rect2(((Vector2)LIGHTHOUSE - Anchor), Size);
            Vector2 Me = (Vector2)LocalPos;
            // your sprite (about a tile wide, 1.3 tiles tall) overlaps the tower and your feet are behind its foot
            Behind = ((Me.Y < ((Vector2)LIGHTHOUSE).Y) && Body.Intersects(new Rect2((Me - new Vector2(12, 40)), new Vector2(24, 40))));
        }
        LighthouseFade = Mathf.MoveToward(LighthouseFade, (Behind ? 1.0f : 0.0f), (Dt * 4.0f));
        return new Color(1, 1, 1, (1.0f - (0.6f * LighthouseFade)));
    }

    // call once per frame while building the world items
    public void TickChest()
    {
        float Now = (float)Clock;
        float Dt = Mathf.Clamp((Now - ChestClock), 0.0f, 0.1f);
        ChestClock = Now;
        bool Near = false;
        foreach (dynamic Id in Gd.Iter(Gd.Get(Snap, "ps", new GDict())))
        {
            dynamic P = Gd.At(Snap.Ps, Id);
            if ((Gd.At(P, 5) != 0))
            {
                continue;
            }
            Vector2 Pos = (Vector2)((Id == MyId) ? (dynamic)(LocalPos) : Gd.Get(ShownP, Id, Gd.At(P, 2)));
            if (GameSim.NearStorage(Pos))
            {
                Near = true;
                break;
            }
        }
        ChestOpen = Mathf.MoveToward(ChestOpen, (Near ? 1.0f : 0.0f), (Dt * (Near ? CHEST_OPEN_SPEED : CHEST_SHUT_SPEED)));
    }

    public void DrawChest(Vector2 Pos)
    {
        dynamic P = Gd.Get(Prop, "chest");
        if ((P == null))
        {
            return;
        }
        // Gd.Load hands back a wrapped object
        var Tex = (Texture2D)Gd.Unwrap(Gd.At(P, 0));
        Vector2 Anchor = Gd.At(P, 1);
        float W = (Tex.GetWidth() / CHEST_FRAMES);
        float H = Tex.GetHeight();
        int F = Mathf.Clamp(Mathf.RoundToInt((ChestOpen * (CHEST_FRAMES - 1))), 0, (CHEST_FRAMES - 1));
        DrawTextureRectRegion(Tex, new Rect2((Pos - Anchor).Round(), new Vector2(W, H)), new Rect2((F * W), 0, W, H));
        // wide open: a glint rises from inside now and then
        if ((F == (CHEST_FRAMES - 1)))
        {
            var Mouth = (Pos + new Vector2(0, -15));
            float Spark = Mathf.PosMod(((float)Clock * 0.9f), 1.0f);
            if ((Spark < 0.35f))
            {
                var Sp = (Mouth + new Vector2(Mathf.Lerp(-6.0f, 6.0f, Mathf.PosMod(Mathf.Floor(((float)Clock * 0.9f)) * 0.37f, 1.0f)), (-4.0f - (Spark * 18.0f))));
                var Sc = new Color(1.0f, 0.95f, 0.7f, (1.0f - (Spark / 0.35f)));
                DrawRect(new Rect2(Sp.Round(), new Vector2(1, 1)), Sc);
                DrawRect(new Rect2((Sp.Round() + new Vector2(-1, 0)), new Vector2(3, 1)), new Color(Sc, (Sc.A * 0.5f)));
                DrawRect(new Rect2((Sp.Round() + new Vector2(0, -1)), new Vector2(1, 3)), new Color(Sc, (Sc.A * 0.5f)));
            }
        }
    }
}
