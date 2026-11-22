using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class Main
{
    static readonly Dictionary<string, Color> ExpeditionPalette = new();
    static Color Ep(string Hex)
    {
        if (!ExpeditionPalette.TryGetValue(Hex, out Color C)) ExpeditionPalette[Hex] = C = new Color(Hex);
        return C;
    }
    // Cached outlines are immutable: FillStatic triangulates each one only once.
    static readonly Vector2[] JeepBonnet = { new(-72,-38), new(-55,-51), new(-17,-47), new(-13,-28), new(-70,-25) };
    static readonly Vector2[] OpenBonnet = { new(-59,-50), new(-55,-77), new(-20,-71), new(-18,-47) };
    static readonly Vector2[] BrokenDoor = { new(-4,-33), new(12,-62), new(38,-57), new(29,-24) };
    static readonly Vector2[] TruckTop = { new(-28,-66), new(-12,-82), new(81,-82), new(91,-66) };

    void ExpeditionItems(GArray Items, Rect2 View)
    {
        if (InteriorActive) return;
        var W = (WorldGeometry)Geometry;
        for (int I = 0; I < W.ExpeditionVehicles.Count; I++)
        {
            var V = W.ExpeditionVehicles[I];
            if (View.Grow(155).HasPoint(V.Pos)) Items.Add(new GArray(V.Pos.Y, 16, I));
        }
        for (int I = 0; I < W.ExpeditionCliffs.Count; I++)
        {
            var F = W.ExpeditionCliffs[I];
            if (View.Grow(100).HasPoint((F.A + F.B) / 2)) Items.Add(new GArray(MathF.Max(F.A.Y, F.B.Y), 17, I));
        }
    }

    void DrawExpeditionVehicle(int Index)
    {
        var V = ((WorldGeometry)Geometry).ExpeditionVehicles[Index];
        DrawSetTransform(V.Pos.Round(), V.Tilt);
        bool Jeep = V.Model == 0;
        int Left = Jeep ? -76 : -94, Right = Jeep ? 75 : 94;
        // A low 3/4-view chassis with distinct exposed engine, cabin, doors and cargo bed.
        DrawRect(new Rect2(Left + 8, -15, Right - Left, 23), new Color("262A24", .40f));
        DrawRect(new Rect2(Left, -29, Right - Left, 20), Ep("242D29"));
        DrawRect(new Rect2(Left + 2, -34, Right - Left - 4, 14), Ep("64715A"));
        DrawLine(new(Left, -33), new(Right, -33), Ep("A9A080"), 3);
        if (Jeep)
        {
            FillStatic(JeepBonnet, Ep("7C8164"), false);
            // The hood is folded up, exposing the engine block.
            DrawRect(new Rect2(-61,-48,40,17), Ep("1B292A"));
            for (int I = 0; I < 4; I++) DrawRect(new Rect2(-56 + I * 8,-44,5,10), Ep("8B9290"));
            FillStatic(OpenBonnet, Ep("6C7758"), false);
            DrawLine(new(-54,-73), new(-24,-68), Ep("ACAD8C"), 2);
            DrawRect(new Rect2(-15,-72,65,38), Ep("303D35"));
            DrawRect(new Rect2(-18,-77,70,8), Ep("9B9B76"));
            DrawRect(new Rect2(-10,-66,27,21), Ep("527C80"));
            DrawRect(new Rect2(22,-65,23,20), Ep("263B3C"));
            DrawLine(new(-8,-48), new(11,-64), Ep("ADC3B4"), 2);
            DrawLine(new(26,-64), new(33,-54), Ep("171F1C"), 2);
            DrawLine(new(33,-54), new(44,-58), Ep("171F1C"), 2);
            FillStatic(BrokenDoor, Ep("8B9C8D"), false);
            DrawLine(new(12,-58), new(31,-54), Ep("C5CCB8"), 2);
            DrawRect(new Rect2(48,-49,23,20), Ep("4B624C"));
            DrawLine(new(50,-47), new(67,-47), Ep("91A17B"), 2);
            DrawLine(new(Left,-27), new(Left-4,-18), Ep("A4AAA0"), 5);
            DrawCircle(new(Left+10,-26), 4, Ep("CFBE86"));
        }
        else
        {
            DrawRect(new Rect2(-89,-54,23,24), Ep("737F61"));
            DrawRect(new Rect2(-66,-82,37,54), Ep("4E6454"));
            DrawRect(new Rect2(-69,-85,43,6), Ep("9A9C79"));
            DrawRect(new Rect2(-61,-75,26,23), Ep("29464D"));
            DrawLine(new(-59,-55), new(-41,-72), Ep("9CB8AF"), 2);
            DrawRect(new Rect2(-59,-44,23,12), Ep("76846C"));
            DrawRect(new Rect2(-41,-46,7,2), Ep("BCC4AB"));
            DrawRect(new Rect2(-27,-37,118,14), Ep("535F49"));
            if (V.Model == 1)
            {
                DrawRect(new Rect2(-27,-68,118,30), Ep("767D5D"));
                FillStatic(TruckTop, Ep("9A9C74"), false);
                for (int I = 0; I < 5; I++)
                {
                    DrawLine(new(-20+I*25,-65), new(-20+I*25,-39), Ep("545E43"), 3);
                    DrawLine(new(-20+I*25,-65), new(-9+I*25,-79), Ep("BCB58B"), 2);
                }
                DrawLine(new(43,-57), new(55,-47), Ep("2C352B"), 3);
                DrawLine(new(55,-47), new(62,-53), Ep("2C352B"), 3);
            }
            else
            {
                for (int I = 0; I < 4; I++)
                {
                    float X = -24+I*37;
                    DrawLine(new(X,-37), new(X,-81), Ep("8D9C91"), 3);
                    if (I < 3) DrawLine(new(X,-81), new(X+37,-81), Ep("B4BAA4"), 2);
                }
                for (int I = 0; I < 3; I++)
                {
                    var Box = new Rect2(-15+I*32,-59-I%2*11,29,23+I%2*11);
                    DrawRect(Box, Ep(V.Model == 3 ? "9F6B43" : "7D8B79"));
                    DrawRect(Box, Ep("39473A"), false, 2);
                    DrawLine(Box.Position+new Vector2(4,3), Box.End-new Vector2(4,3), Ep("C1B48A"), 2);
                    DrawLine(Box.Position+new Vector2(Box.Size.X-4,3), Box.Position+new Vector2(4,Box.Size.Y-3), Ep("39473A"), 2);
                }
                // Torn canvas hangs from the rear hoop, exposing the cargo.
                DrawRect(new Rect2(-24,-79,38,9), Ep("7F8760"));
                for (int I = 0; I < 4; I++) DrawRect(new Rect2(-24+I*8,-70,5,8+I%2*6), Ep("636F50"));
                DrawLine(new(91,-36), new(106,-22), Ep("87907B"), 4);
            }
            for (int I = 0; I < 7; I++) DrawRect(new Rect2(-22+I*17,-34,2,9), Ep("ABB098"));
            DrawRect(new Rect2(-98,-26,9,8), Ep("A3A995"));
            DrawCircle(new(-82,-38), 4, Ep("D2C793"));
        }
        // Rust patches, exposed bare metal and punctured tyres retain their silhouette at game zoom.
        for (int I = 0; I < 9; I++)
            DrawRect(new Rect2(Left+12+I*16,-29-I%3*3,7+I%3*2,3), Ep(I%2==0 ? "935A36" : "BEC1A2"));
        for (int I = 0; I < 30; I++)
        {
            Vector2 P = new(Left+8+(I*43%(Right-Left-15)),-24+I%3*3);
            DrawRect(new Rect2(P,new Vector2(2+I%3,1)),Ep(I%3==0 ? "C6C4A8" : I%3==1 ? "A66F49" : "3C4E3B"));
        }
        DrawExpeditionWheel(new(Jeep ? -48 : -64, -5), Jeep ? 14 : 16, V.Model == 3);
        DrawExpeditionWheel(new(Jeep ? 49 : 38, -5), Jeep ? 14 : 16, true);
        if (!Jeep) DrawExpeditionWheel(new(72,-5), 16, false);
        DrawSetTransform(Vector2.Zero);
    }

    void DrawExpeditionWheel(Vector2 P, float Radius, bool Flat)
    {
        DrawCircle(P,Radius,Ep("18221F"));
        DrawCircle(P,Radius-3,Ep("37413A"));
        DrawCircle(P,Radius-6,Ep("8B9381"));
        DrawCircle(P,3,Ep("303E36"));
        for(int I=0;I<6;I++)
        {
            Vector2 D=Vector2.FromAngle(I*Mathf.Tau/6);
            DrawLine(P+D*(Radius-3),P+D*(Radius-1),Ep("5A6252"),2);
        }
        if(Flat) DrawRect(new Rect2(P+new Vector2(-Radius+2,Radius-6),new Vector2(Radius*2-4,7)),Ep("514F3D"));
    }

    void DrawExpeditionCliff(int Index)
    {
        var F = ((WorldGeometry)Geometry).ExpeditionCliffs[Index];
        FillStatic(F.Face, Ep("676F67")); FillStatic(F.Cap, Ep("B2B09A"));
        DrawLine(F.A, F.B, Ep("444F49"), 3);
        for (int I = 1; I <= 3; I++)
        {
            Vector2 Lift = new(0,-I*7);
            DrawLine(F.A+Lift, F.B+Lift, Ep(I==2 ? "929783" : "7C8477"), 2);
        }
        Vector2 Crack = F.A.Lerp(F.B,.55f);
        DrawLine(Crack-new Vector2(3,27), Crack+new Vector2(2,-13), Ep("4E5C54"), 2);
        DrawLine(Crack+new Vector2(2,-13), Crack+new Vector2(-5,-3), Ep("4E5C54"), 2);
        for(int I=0;I<12;I++)
        {
            Vector2 P=F.A.Lerp(F.B,(I+1)/14f)+new Vector2(0,-38-I%3*4);
            DrawRect(new Rect2(P.Round(),new Vector2(3+I%3,2)),Ep(I%2==0 ? "D1C9AA" : "879486"));
        }
    }

    void DrawExpeditionGround(WorldGeometry W, Rect2 View)
    {
        foreach (var Site in W.ExpeditionSites.Values)
        {
            if (!new Rect2(Site.Centre-Site.Half,Site.Half*2).Grow(90).Intersects(View)) continue;
            Vector2 C = Site.Centre;
            if (Site.Id == 11)
            {
                // Broad stone treads make the ascent readable even after the former yellow routes were removed.
                for (int I = 0; I < 23; I++)
                {
                    float Y = -173+I*16, Width = 51+(I%3-1)*3;
                    DrawRect(new Rect2(C+new Vector2(-Width,Y+3),new Vector2(Width*2,12)),Ep("5B6D69"));
                    DrawRect(new Rect2(C+new Vector2(-Width,Y),new Vector2(Width*2,10)),Ep(I%3==0 ? "ACB7B0" : "929F99"));
                    DrawLine(C+new Vector2(-Width+2,Y),C+new Vector2(Width-2,Y),Ep("C1CAC2"),2);
                    // Offset joints, missing corners and weathering break up the regular stair silhouette.
                    float Joint=-18+(I%3)*11;
                    DrawLine(C+new Vector2(Joint,Y+1),C+new Vector2(Joint-2,Y+10),Ep("657871"),2);
                    DrawLine(C+new Vector2(Joint+32,Y+1),C+new Vector2(Joint+30,Y+9),Ep("657871"),1);
                    DrawRect(new Rect2(C+new Vector2(I%2==0 ? -Width : Width-5,Y+6),new Vector2(5,4)),Ep("717E71"));
                    float Crack = -21+(I*17%39);
                    DrawLine(C+new Vector2(Crack,Y+2),C+new Vector2(Crack+4,Y+8),Ep("778272"),1);
                }
                for (int Side=-1;Side<=1;Side+=2)
                    for (int I = 0; I < 6; I++)
                    {
                        Vector2 P = C+new Vector2(Side*66,-186+I*75);
                        DrawLine(P,P+new Vector2(0,-22),Ep("5B6352"),4);
                        DrawLine(P+new Vector2(-1,-22),P+new Vector2(-1,-4),Ep("B9B192"),1);
                        if(I<5) DrawLine(P+new Vector2(0,-18),P+new Vector2(0,57),Ep("9A9575"),2);
                    }
                // A separate pennant makes the direction of the north wind visible.
                Vector2 Flag = C+new Vector2(-77,-179);
                DrawLine(Flag,Flag+new Vector2(0,-52),Ep("535E57"),3);
                for(int I=0;I<7;I++)
                    DrawRect(new Rect2(Flag+new Vector2(2+I*5,-51+MathF.Sin((float)Clock*2+I*.5f)*3),new Vector2(5,11-I)),Ep(I%2==0 ? "C7B483" : "885D42"));
                continue;
            }
            // Churned mud under each vehicle, with tyre ruts on both sides of each wheel line.
            foreach (var V in W.ExpeditionVehicles)
            {
                if (V.Id != Site.Id) continue;
                DrawSetTransform(V.Pos+new Vector2(0,3),V.Tilt,new Vector2(1,.28f));
                DrawCircle(Vector2.Zero,V.Model==0 ? 83 : 104,Ep("5C5945"));
                DrawCircle(new Vector2(13,9),V.Model==0 ? 60 : 81,Ep("69624B"));
                DrawSetTransform(Vector2.Zero);
                for(int I=0;I<16;I++)
                    for(int Side=-1;Side<=1;Side+=2)
                    {
                        Vector2 P = V.Pos+new Vector2(-112+I*13,Side*21+9);
                        DrawLine(P,P+new Vector2(7,2),Ep("544D3D"),3);
                    }
            }
            if(Site.Id==5)
            {
                for(int I=0;I<16;I++)
                    for(int Side=-1;Side<=1;Side+=2)
                    {
                        Vector2 P=C+new Vector2(-160+I*7,-110+I*6+Side*16);
                        DrawLine(P,P+new Vector2(5,4),Ep("605A46"),4);
                    }
                DrawLine(C+new Vector2(74,-9),C+new Vector2(145,1),Ep("C0B9A0"),2);
                DrawLine(C+new Vector2(82,-6),C+new Vector2(115,5),Ep("5B6256"),1);
                DrawSetTransform(C+new Vector2(-41,8),0,new Vector2(1,.45f));
                DrawCircle(Vector2.Zero,25,Ep("3B443D")); DrawSetTransform(Vector2.Zero);
                // A detached door and bonnet fragments remain on the ground, away from the walking aisle.
                DrawRect(new Rect2(C+new Vector2(54,98),new Vector2(39,17)),Ep("5E7464"));
                DrawRect(new Rect2(C+new Vector2(59,100),new Vector2(23,9)),Ep("89A4A0"));
                DrawCircle(C+new Vector2(-160,18),13,Ep("2B322C"));
                DrawCircle(C+new Vector2(-160,18),6,Ep("8B8F7D"));
            }
            else
            {
                // Spilled timber and steel cargo lies between the stopped trucks, off the central passage.
                for(int I=0;I<7;I++)
                {
                    Vector2 P=C+new Vector2(91+I*11,118+I%3*6);
                    DrawLine(P,P+new Vector2(34,-5),Ep(I%2==0 ? "A29C7D" : "78694D"),5);
                    DrawLine(P,P+new Vector2(29,-4),Ep("C3B799"),1);
                }
            }
        }
    }
}
