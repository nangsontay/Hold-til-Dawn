using Godot;
using System;
using System.Collections.Generic;
using FileAccess = Godot.FileAccess;

// Owner: Viet Anh. Looks at every sprite animation in art/chars without playing a match:
//   godot --path . tools/AnimViewer.tscn
// The top shows the chosen animation playing in all 8 directions, the bottom every frame of it in one direction.
// The red cross is the anchor (where the game puts the character's feet).
//   Left / Right  character        Up / Down  animation        Z / X  direction of the frame strip
//   Space  pause    , / .  one frame back / forward (paused)    - / +  speed    Esc  quit
// Start somewhere else with  -- --char=wrecker --anim=down  (--shot=file.png saves a screenshot and quits).
public partial class AnimViewer : Node2D
{
    static readonly string[] DIRS = { "right", "down", "left", "up", "down_right", "down_left", "up_left", "up_right" };
    static readonly float[] SPEEDS = { 0.125f, 0.25f, 0.5f, 1.0f, 2.0f };
    // a one-shot animation holds its last frame this long before playing again
    const float HOLD = 0.8f;

    struct Anim
    {
        public string Name;
        public int First, Count;
        public float Fps;
        public bool Loop;
    }

    class Sheet
    {
        public string Name;
        public Texture2D Tex;
        public int Size;
        public Vector2 Anchor;
        public List<Anim> Anims = new();
    }

    readonly List<Sheet> Sheets = new();
    int Who, Which, StripDir = 1, Speed = 3;
    bool Paused;
    float T, Up;
    string Shot = "";

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        Godot.Collections.Dictionary Frames = null, Fps = null;
        foreach (string File in new[] { "chars.json", "bosses.json" })
        {
            var Info = Json.ParseString(FileAccess.GetFileAsString("res://art/chars/" + File)).AsGodotDictionary();
            // bosses.json has no idle / run of its own: the bosses use the same columns as chars.json
            if (Info.ContainsKey("frames"))
            {
                Frames = Info["frames"].AsGodotDictionary();
                Fps = Info["fps"].AsGodotDictionary();
            }
            var Chars = Info["chars"].AsGodotDictionary();
            foreach (var (Key, Value) in Chars)
            {
                var C = Value.AsGodotDictionary();
                var A = C["anchor"].AsGodotArray();
                var S = new Sheet
                {
                    Name = Key.AsString(),
                    Tex = GD.Load<Texture2D>("res://art/chars/" + Key.AsString() + ".png"),
                    Size = C["size"].AsInt32(),
                    Anchor = new Vector2(A[0].AsSingle(), A[1].AsSingle()),
                };
                // the looping ones (idle, run) are listed at the top of the json, the one-shots under each character
                foreach (var (N, F) in Frames)
                {
                    var R = F.AsGodotArray();
                    S.Anims.Add(new Anim { Name = N.AsString(), First = R[0].AsInt32(), Count = R[1].AsInt32(), Fps = Fps[N].AsSingle(), Loop = true });
                }
                if (C.ContainsKey("anims"))
                {
                    var Names = new List<string>();
                    foreach (var (N, _) in C["anims"].AsGodotDictionary())
                    {
                        Names.Add(N.AsString());
                    }
                    // attacks first (in the order of the sheet), then down
                    var One = C["anims"].AsGodotDictionary();
                    Names.Sort((X, Y) => One[X].AsGodotArray()[0].AsInt32().CompareTo(One[Y].AsGodotArray()[0].AsInt32()));
                    foreach (string N in Names)
                    {
                        var R = One[N].AsGodotArray();
                        S.Anims.Add(new Anim { Name = N, First = R[0].AsInt32(), Count = R[1].AsInt32(), Fps = R[2].AsSingle() });
                    }
                }
                Sheets.Add(S);
            }
        }
        // start on the first attack of the first character, unless told otherwise
        Which = Math.Min(2, (Sheets[0].Anims.Count - 1));
        foreach (string Arg in OS.GetCmdlineUserArgs())
        {
            if (Arg.StartsWith("--char=", StringComparison.Ordinal))
            {
                Who = Math.Max(0, Sheets.FindIndex(S => (S.Name == Arg[7..])));
            }
            else if (Arg.StartsWith("--anim=", StringComparison.Ordinal))
            {
                Which = Math.Max(0, Sheets[Who].Anims.FindIndex(A => (A.Name == Arg[7..])));
            }
            else if (Arg.StartsWith("--shot=", StringComparison.Ordinal))
            {
                Shot = Arg[7..];
            }
        }
    }

    Anim Current => Sheets[Who].Anims[Which];

    // the column shown at time T
    int Frame(Anim A, float At)
    {
        int F = (int)Mathf.Floor(At * A.Fps);
        if (A.Loop)
        {
            return (A.First + (((F % A.Count) + A.Count) % A.Count));
        }
        float Cycle = ((A.Count / A.Fps) + HOLD);
        F = (int)Mathf.Floor((Mathf.PosMod(At, Cycle) * A.Fps));
        return (A.First + Math.Min(F, (A.Count - 1)));
    }

    public override void _Process(double Delta)
    {
        if (!Paused)
        {
            T += ((float)Delta * SPEEDS[Speed]);
        }
        QueueRedraw();
        Up += (float)Delta;
        if ((Shot != "") && (Up > 0.5f))
        {
            GetViewport().GetTexture().GetImage().SavePng(Shot);
            GetTree().Quit();
        }
    }

    public override void _UnhandledInput(InputEvent E)
    {
        if (!(E is InputEventKey K) || !K.Pressed)
        {
            return;
        }
        var S = Sheets[Who];
        switch (K.Keycode)
        {
            case Key.Right:
                Pick(1);
                break;
            case Key.Left:
                Pick(-1);
                break;
            case Key.Down:
                Which = ((Which + 1) % S.Anims.Count);
                T = 0;
                break;
            case Key.Up:
                Which = (((Which - 1) + S.Anims.Count) % S.Anims.Count);
                T = 0;
                break;
            case Key.Z:
                StripDir = (((StripDir - 1) + 8) % 8);
                break;
            case Key.X:
                StripDir = ((StripDir + 1) % 8);
                break;
            case Key.Space:
                Paused = !Paused;
                break;
            case Key.Period:
                Paused = true;
                T += (1.0f / Current.Fps);
                break;
            case Key.Comma:
                Paused = true;
                T = Math.Max(0, (T - (1.0f / Current.Fps)));
                break;
            case Key.Minus:
            case Key.KpSubtract:
                Speed = Math.Max(0, (Speed - 1));
                break;
            case Key.Equal:
            case Key.Plus:
            case Key.KpAdd:
                Speed = Math.Min((SPEEDS.Length - 1), (Speed + 1));
                break;
            case Key.Escape:
                GetTree().Quit();
                break;
        }
    }

    // another character, keeping the same animation when it has one of that name
    void Pick(int Step)
    {
        string Was = Current.Name;
        Who = (((Who + Step) + Sheets.Count) % Sheets.Count);
        int I = Sheets[Who].Anims.FindIndex(A => (A.Name == Was));
        Which = ((I >= 0) ? I : Math.Min(Which, (Sheets[Who].Anims.Count - 1)));
        T = 0;
    }

    public override void _Draw()
    {
        var View = GetViewportRect().Size;
        var Font = ThemeDB.FallbackFont;
        var S = Sheets[Who];
        var A = Current;
        DrawRect(new Rect2(Vector2.Zero, View), new Color(0.13f, 0.14f, 0.17f));
        const float Pad = 12, Gap = 10;
        float Head = 40, Foot = 26;
        // the bottom strip is a third of the height, the 8 directions get the rest
        float StripH = ((View.Y - Head - Foot) * 0.32f);
        float GridH = ((View.Y - Head - Foot) - StripH - Gap);
        float Big = Mathf.Max(1, Mathf.Floor(Mathf.Min(((View.X - (2 * Pad) - (3 * Gap)) / (4 * S.Size)), ((GridH - Gap - 32) / (2 * S.Size)))));
        float Small = Mathf.Max(1, Mathf.Floor(Mathf.Min(((View.X - (2 * Pad)) / (A.Count * (S.Size + 4))), ((StripH - 18) / S.Size))));
        string Title = $"{S.Name}  ({(Who + 1)}/{Sheets.Count})   {A.Name}  [{A.First}..{(A.First + A.Count - 1)}]  {A.Fps} fps  x{SPEEDS[Speed]}{(Paused ? "  PAUSED" : "")}";
        DrawString(Font, new Vector2(Pad, 26), Title, HorizontalAlignment.Left, -1, 18, Colors.White);
        int Col = Frame(A, T);
        float CellW = (S.Size * Big);
        float X0 = Mathf.Floor(((View.X - ((4 * CellW) + (3 * Gap))) / 2));
        for (int D = 0; D < 8; D++)
        {
            var At = new Vector2((X0 + ((D % 4) * (CellW + Gap))), (Head + ((D / 4) * (CellW + 16 + Gap))));
            Cell(S, Col, D, At, Big, (D == StripDir));
            DrawString(Font, (At + new Vector2(0, (CellW + 13))), DIRS[D], HorizontalAlignment.Left, -1, 12, new Color(0.75f, 0.78f, 0.85f));
        }
        // every frame of the animation in one direction, the one playing outlined
        float Sy = (View.Y - Foot - StripH);
        DrawString(Font, new Vector2(Pad, (Sy + 12)), $"frames, {DIRS[StripDir]} (Z / X)", HorizontalAlignment.Left, -1, 12, new Color(0.75f, 0.78f, 0.85f));
        float Sw = (S.Size * Small);
        float Sx = Mathf.Floor(((View.X - ((A.Count * Sw) + ((A.Count - 1) * 4))) / 2));
        for (int F = 0; F < A.Count; F++)
        {
            var At = new Vector2((Sx + (F * (Sw + 4))), (Sy + 18));
            Cell(S, (A.First + F), StripDir, At, Small, ((A.First + F) == Col));
        }
        DrawString(Font, new Vector2(Pad, (View.Y - 8)), "Left/Right character   Up/Down animation   Z/X strip direction   Space pause   , . step   - + speed   Esc quit", HorizontalAlignment.Left, -1, 12, new Color(0.55f, 0.58f, 0.65f));
    }

    // one sheet cell scaled up, on a checkerboard, with the anchor marked
    void Cell(Sheet S, int Col, int Row, Vector2 At, float Zoom, bool Lit)
    {
        float W = (S.Size * Zoom);
        float Q = Mathf.Max(4, (4 * Zoom));
        for (float Y = 0; Y < W; Y += Q)
        {
            for (float X = 0; X < W; X += Q)
            {
                bool Odd = (((int)(X / Q) + (int)(Y / Q)) % 2 == 1);
                DrawRect(new Rect2((At + new Vector2(X, Y)), new Vector2(Mathf.Min(Q, (W - X)), Mathf.Min(Q, (W - Y)))), (Odd ? new Color(0.24f, 0.26f, 0.3f) : new Color(0.2f, 0.22f, 0.26f)));
            }
        }
        DrawTextureRectRegion(S.Tex, new Rect2(At, new Vector2(W, W)), new Rect2((Col * S.Size), (Row * S.Size), S.Size, S.Size));
        var P = (At + (S.Anchor * Zoom));
        var Red = new Color(1, 0.3f, 0.3f, 0.8f);
        DrawLine((P - new Vector2(Zoom * 2, 0)), (P + new Vector2(Zoom * 2, 0)), Red, 1);
        DrawLine((P - new Vector2(0, Zoom * 2)), (P + new Vector2(0, Zoom * 2)), Red, 1);
        DrawRect(new Rect2(At, new Vector2(W, W)), (Lit ? new Color(1, 0.85f, 0.3f) : new Color(0.35f, 0.38f, 0.45f)), false, (Lit ? 2 : 1));
    }
}
