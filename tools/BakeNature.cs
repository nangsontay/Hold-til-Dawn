using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

public partial class BakeNature : Node
{
    // Bakes 3D models into pixel-art sprites seen from the game's 3/4 camera, matching the 32x32 characters:
    // 1 px = 1 game px, hard edges, banded colours and the same 1 px dark outline (GDD style guide).
    // Not part of the game. Run it in a throwaway Godot project whose res://models/ holds the
    // Quaternius "Stylized Nature MegaKit" glTF files (CC0), with this script on the main scene's root:
    //   godot --path <bake project> -- --out=D:/Hold-til-Dawn/art/nature
    // Writes <Model>_<k>.png (k = turned variant) plus index.json: {name: [anchor_x, anchor_y]}, the pixel
    // under the model's origin (its foot). The game draws each sprite with that pixel on the object's position.
    // Each big sprite carries its own cast shadow: a second pass renders only the shadow on a ground plane.
    // For smooth shadows give the bake project: rendering/lights_and_shadows/directional_shadow/size=4096
    // and rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality=3.
    public static readonly dynamic PPU = 32.0f;
    public static readonly dynamic INK = Gd.MakeColor("141019");
    public static readonly dynamic PITCH = 50.0f;
    public static readonly dynamic YAWS = new GArray((-30.0f), 60.0f, 150.0f);
    public static readonly dynamic PAD = 6;
    public static readonly dynamic SHADOW_A = 0.42f;
    public static readonly dynamic SHADOW_COL = Gd.MakeColor(0.04f, 0.06f, 0.1f);
    // Model scale in tiles. Quaternius trees are ~6 m tall; at x0.55 a tree is drawn ~3 tiles tall, about 3.5-4x a 32 px character.
    public static readonly dynamic SCALE = new GDict { { "Tree", 0.55f }, { "TwistedTree", 0.27f }, { "Pine", 0.55f }, { "Rock_Medium", 0.6f }, { "Bush", 0.8f }, { "Plant", 0.7f }, { "Fern", 0.75f }, { "Mushroom", 0.8f }, { "Flower", 0.6f }, { "Grass", 0.6f }, { "Clover", 0.7f }, { "Pebble", 0.7f }, { "RockPath", 0.7f }, { "Petal", 0.7f } };
    public static readonly dynamic FLAT = new GArray("Grass", "Clover", "Flower", "Pebble", "Petal", "RockPath");
    public dynamic Vp = Gd.Wrap(new SubViewport());
    public dynamic Cam = Gd.Wrap(new Camera3D());
    public dynamic Sun = Gd.Wrap(new DirectionalLight3D());
    public dynamic Env = Gd.Wrap(new Environment());
    public dynamic Holder = Gd.Wrap(new Node3D());
    public dynamic Ground = Gd.Wrap(new MeshInstance3D());
    public async override void _Ready()
    {
        dynamic Out = "res://baked";
        foreach (dynamic A in Gd.Iter(Gd.Static(typeof(OS), "get_cmdline_user_args")))
        {
            if (Gd.Truth(Gd.BeginsWith(A, "--out=")))
            {
                Out = Gd.Substr(A, 6);
            }
        }
        Gd.Static(typeof(DirAccess), "make_dir_recursive_absolute", Out);
        foreach (dynamic F in Gd.Iter(Gd.Static(typeof(DirAccess), "get_files_at", Out)))
        {
            if (Gd.Truth(Gd.EndsWith(F, ".png")))
            {
                Gd.Static(typeof(DirAccess), "remove_absolute", Gd.PathJoin(Out, F));
            }
        }
        Vp.TransparentBg = true;
        Vp.OwnWorld3d = true;
        // hard pixel edges
        Vp.Msaa3d = Gd.EnumValue(typeof(Viewport), "MSAA_DISABLED", 0);
        Vp.RenderTargetUpdateMode = Gd.EnumValue(typeof(SubViewport), "UPDATE_ALWAYS", 4);
        Gd.Native(this, "add_child", Vp);
        Env.BackgroundMode = Gd.EnumValue(typeof(Environment), "BG_CLEAR_COLOR", 0);
        Env.AmbientLightSource = Gd.EnumValue(typeof(Environment), "AMBIENT_SOURCE_COLOR", 2);
        dynamic We = Gd.Wrap(new WorldEnvironment());
        We.Environment = Env;
        Vp.AddChild(We);
        // light from the upper left, same for every sprite
        Sun.RotationDegrees = Gd.MakeVector3((-50), (-40), 0);
        // default PSSM mode: SHADOW_ORTHOGONAL casts nothing under an orthogonal camera
        Sun.ShadowEnabled = true;
        Sun.DirectionalShadowMaxDistance = 300.0f;
        Sun.ShadowBlur = 0.8f;
        Vp.AddChild(Sun);
        Cam.Projection = Gd.EnumValue(typeof(Camera3D), "PROJECTION_ORTHOGONAL", 1);
        Cam.KeepAspect = Gd.EnumValue(typeof(Camera3D), "KEEP_HEIGHT", 1);
        Cam.RotationDegrees = Gd.MakeVector3((-PITCH), 0, 0);
        Vp.AddChild(Cam);
        Vp.AddChild(Holder);
        dynamic Pm = Gd.Wrap(new PlaneMesh());
        Pm.Size = Gd.MakeVector2(60, 60);
        Ground.Mesh = Pm;
        dynamic Gm = Gd.Wrap(new StandardMaterial3D());
        Gm.AlbedoColor = Colors.White;
        Gm.Roughness = 1.0f;
        Gm.MetallicSpecular = 0.0f;
        Ground.MaterialOverride = Gm;
        Ground.CastShadow = Gd.EnumValue(typeof(GeometryInstance3D), "SHADOW_CASTING_SETTING_OFF", 0);
        Vp.AddChild(Ground);
        dynamic Index = new GDict {  };
        dynamic Names = new GArray();
        foreach (dynamic F in Gd.Iter(Gd.Static(typeof(DirAccess), "get_files_at", "res://models")))
        {
            if (Gd.Truth(Gd.EndsWith(F, ".gltf")))
            {
                Gd.Append(Names, Gd.GetBasename(F));
            }
        }
        Gd.Sort(Names);
        foreach (dynamic N in Gd.Iter(Names))
        {
            dynamic Flat = Gd.Any(FLAT, (Func<dynamic, dynamic>)((K) => Gd.Contains(N, K)));
            dynamic Yaws = (Gd.Truth(Flat) ? (dynamic)(new GArray(Gd.At(YAWS, 0))) : YAWS);
            foreach (dynamic K in Gd.Iter(Gd.Size(Yaws)))
            {
                Vector2 Anchor = (Vector2)(await this.Bake(N, Gd.At(Yaws, K), !(Gd.Truth(Flat)), Gd.PathJoin(Out, Gd.Format("%s_%d.png", new GArray(N, K)))));
                Index[Gd.Format("%s_%d", new GArray(N, K))] = new GArray(Anchor.X, Anchor.Y);
            }
            GD.Print("baked ", N);
        }
        dynamic Fa = Gd.Static(typeof(FileAccess), "open", Gd.PathJoin(Out, "index.json"), Gd.EnumValue(typeof(FileAccess), "WRITE", 2));
        Fa.StoreString(Json.Stringify(Gd.ToVariant(Index), "\t", true));
        Fa.Close();
        Gd.Native(this, "get_tree").Quit();
    }

    public async Task<dynamic> Bake(dynamic N, dynamic Yaw, dynamic Shadow, dynamic Path)
    {
        foreach (dynamic C in Gd.Iter(Holder.GetChildren()))
        {
            C.Free();
        }
        dynamic Inst = Gd.Load(Gd.Format("res://models/%s.gltf", N)).Instantiate();
        dynamic S = 0.6f;
        foreach (dynamic Key in Gd.Iter(SCALE))
        {
            if (Gd.Contains(N, Key))
            {
                S = Gd.At(SCALE, Key);
            }
        }
        Inst.Scale = (Vector3.One * S);
        { var Component1 = Inst.RotationDegrees; Component1.Y = Yaw; Inst.RotationDegrees = Component1; }
        Holder.AddChild(Inst);
        dynamic Meshes = Inst.FindChildren("*", "MeshInstance3D", true, false);
        // fit the camera to the model and to where its shadow falls on the ground
        dynamic Right = Cam.GlobalTransform.Basis.X;
        dynamic Up = Cam.GlobalTransform.Basis.Y;
        dynamic Back = Cam.GlobalTransform.Basis.Z;
        // direction the light travels
        dynamic D = (-Sun.GlobalTransform.Basis.Z);
        dynamic Lo = Gd.MakeVector2(float.PositiveInfinity, float.PositiveInfinity);
        dynamic Hi = Gd.MakeVector2((-float.PositiveInfinity), (-float.PositiveInfinity));
        foreach (dynamic M in Gd.Iter(Meshes))
        {
            dynamic Mi = M;
            dynamic Box = (Mi.GlobalTransform * Mi.GetAabb());
            foreach (dynamic I in Gd.Iter(8))
            {
                dynamic C = Box.GetEndpoint(I);
                dynamic Pts = new GArray(C);
                if ((Gd.Truth(Shadow) && (C.Y > 0)))
                {
                    // where this corner's shadow lands
                    Gd.Append(Pts, (C - (D * (C.Y / D.Y))));
                }
                foreach (dynamic P in Gd.Iter(Pts))
                {
                    dynamic Q = Gd.MakeVector2(P.Dot(Right), P.Dot(Up));
                    Lo = Lo.Min(Q);
                    Hi = Hi.Max(Q);
                }
            }
        }
        dynamic Size = Gd.MakeVector2I((Gd.Ceili(((Hi.X - Lo.X) * PPU)) + (PAD * 2)), (Gd.Ceili(((Hi.Y - Lo.Y) * PPU)) + (PAD * 2)));
        GD.Print(Gd.Format("  %s yaw %d -> %s", new GArray(N, Yaw, Size)));
        Vp.Size = Size;
        Cam.Size = (Size.Y / PPU);
        dynamic Mid = ((Lo + Hi) / 2.0f);
        Cam.Position = (((Right * Mid.X) + (Up * Mid.Y)) + (Back * 20.0f));
        // pass 1: the model itself, contrasty light, self-shadowed, no ground
        Ground.Visible = false;
        foreach (dynamic M in Gd.Iter(Meshes))
        {
            M.CastShadow = Gd.EnumValue(typeof(GeometryInstance3D), "SHADOW_CASTING_SETTING_ON", 1);
        }
        // cool sky fill, so the shaded side reads blue-dark
        Env.AmbientLightColor = Gd.MakeColor(0.62f, 0.7f, 0.85f);
        Env.AmbientLightEnergy = 0.42f;
        Env.TonemapMode = Gd.EnumValue(typeof(Environment), "TONE_MAPPER_FILMIC", 2);
        Sun.LightColor = Gd.MakeColor(1.0f, 0.94f, 0.82f);
        Sun.LightEnergy = 1.7f;
        dynamic Col = await this.Grab();
        this.Pixelate(Col, Shadow);
        if (!(Gd.Truth(Shadow)))
        {
            Col.SavePng(Path);
            return this.Anchor(Size, Mid);
        }
        // pass 2: only the shadow, on a white ground plane lit by the sun alone
        Ground.Visible = true;
        foreach (dynamic M in Gd.Iter(Meshes))
        {
            M.CastShadow = Gd.EnumValue(typeof(GeometryInstance3D), "SHADOW_CASTING_SETTING_SHADOWS_ONLY", 3);
        }
        Env.AmbientLightEnergy = 0.0f;
        Env.TonemapMode = Gd.EnumValue(typeof(Environment), "TONE_MAPPER_LINEAR", 0);
        Sun.LightColor = Colors.White;
        Sun.LightEnergy = 1.0f;
        dynamic Sh = await this.Grab();
        Sh.Convert(Gd.EnumValue(typeof(Image), "FORMAT_L8", 0));
        dynamic Lum = Sh.GetData();
        dynamic Lit = 1;
        foreach (dynamic V in Gd.Iter(Lum))
        {
            Lit = Gd.Maxi(Lit, V);
        }
        dynamic Px = Gd.PackedByteArray();
        Gd.Resize(Px, ((Size.X * Size.Y) * 4));
        foreach (dynamic I in Gd.Iter(Gd.Size(Lum)))
        {
            // hard-edged shadow
            dynamic A = (((1.0f - (Gd.Float(Gd.At(Lum, I)) / Lit)) > 0.2f) ? (dynamic)(SHADOW_A) : 0.0f);
            Px[(I * 4)] = Gd.Int(SHADOW_COL.R8);
            Px[((I * 4) + 1)] = Gd.Int(SHADOW_COL.G8);
            Px[((I * 4) + 2)] = Gd.Int(SHADOW_COL.B8);
            Px[((I * 4) + 3)] = Gd.Int((A * 255.0f));
        }
        dynamic Out = Gd.Static(typeof(Image), "create_from_data", Size.X, Size.Y, false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5), Px);
        Out.BlendRect(Col, Gd.MakeRect2I(Vector2I.Zero, Size), Vector2I.Zero);
        Out.SavePng(Path);
        return this.Anchor(Size, Mid);
    }

    public async Task<dynamic> Grab()
    {
        foreach (dynamic I in Gd.Iter(4))
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        return Vp.GetTexture().GetImage();
    }

    public dynamic Anchor(dynamic Size, dynamic Mid)
    {
        // pixel under the model origin (world 0,0,0)
        return Gd.MakeVector2(((Size.X / 2.0f) - (Mid.X * PPU)), ((Size.Y / 2.0f) + (Mid.Y * PPU)));
    }

    public void Pixelate(dynamic Img, dynamic Outline)
    {
        // Pixel-art finish: alpha is on or off, colours fall into bands, and big sprites get a 1 px dark outline.
        Img.Convert(Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        dynamic W = Img.GetWidth();
        dynamic H = Img.GetHeight();
        foreach (dynamic Y in Gd.Iter(H))
        {
            foreach (dynamic X in Gd.Iter(W))
            {
                dynamic C = Img.GetPixel(X, Y);
                if ((C.A < 0.5f))
                {
                    Img.SetPixel(X, Y, Gd.MakeColor(0, 0, 0, 0));
                }
                else
                {
                    Img.SetPixel(X, Y, Color.FromHsv(Gd.Snappedf(C.H, (1.0f / 48.0f)), Gd.Snappedf(C.S, 0.12f), Gd.Snappedf(C.V, 0.09f)));
                }
            }
        }
        if (!(Gd.Truth(Outline)))
        {
            return;
        }
        dynamic Src = Gd.Duplicate(Img);
        foreach (dynamic Y in Gd.Iter(H))
        {
            foreach (dynamic X in Gd.Iter(W))
            {
                if ((Src.GetPixel(X, Y).A > 0.0f))
                {
                    continue;
                }
                foreach (dynamic D in Gd.Iter(new GArray(Gd.MakeVector2I(1, 0), Gd.MakeVector2I((-1), 0), Gd.MakeVector2I(0, 1), Gd.MakeVector2I(0, (-1)))))
                {
                    dynamic Q = (Gd.MakeVector2I(X, Y) + D);
                    if (((Q.X >= 0) && (Q.Y >= 0) && (Q.X < W) && (Q.Y < H) && (Src.GetPixel(Q.X, Q.Y).A > 0.0f)))
                    {
                        Img.SetPixel(X, Y, INK);
                        break;
                    }
                }
            }
        }
    }

}
