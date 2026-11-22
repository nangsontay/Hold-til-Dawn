using Godot;

public partial class Main
{
    void DrawCompoundGround(WorldGeometry World, Rect2 View)
    {
        foreach (var (Id, Floor) in World.Compounds)
        {
            if (!Floor.Intersects(View)) continue;
            Vector2 C = Floor.GetCenter();
            if (Id == 6)
            {
                // The exposed foundations still describe the old building's rooms, with an open central corridor.
                Color Foundation = new("64675D"), Scorch = new("444A43", .45f);
                DrawLine(Floor.Position + new Vector2(8, 25), new Vector2(Floor.End.X - 8, Floor.Position.Y + 25), Foundation, 9);
                DrawLine(Floor.Position + new Vector2(13, 25), new Vector2(Floor.Position.X + 13, Floor.End.Y - 12), Foundation, 8);
                DrawLine(C + new Vector2(32, -72), C + new Vector2(32, -15), Foundation, 6);
                DrawLine(C + new Vector2(32, -15), C + new Vector2(110, -15), Foundation, 6);
                DrawSetTransform(C + new Vector2(-79, 3), 0, new Vector2(1.5f, .65f));
                DrawCircle(Vector2.Zero, 41, Scorch); DrawSetTransform(Vector2.Zero);
                for (int I = 0; I < 23; I++)
                {
                    float X = -127 + (I * 47 % 225), Y = -70 + (I * 29 % 150);
                    Vector2 P = C + new Vector2(X, Y);
                    if (Floor.Grow(-9).HasPoint(P))
                    {
                        DrawLine(P, P + new Vector2(12 + I % 12, 4), new Color("575E56"), 1);
                        DrawLine(P + new Vector2(12 + I % 12, 4), P + new Vector2(15 + I % 12, 11), new Color("575E56"), 1);
                    }
                }
                // Fallen roof trusses and fragments lie in the side bays, leaving the centre navigable.
                for (int Side=-1;Side<=1;Side+=2)
                    for(int I=0;I<7;I++)
                    {
                        Vector2 P=C+new Vector2(Side*(60+I*17%76),-62+I*23);
                        DrawSetTransform(P,I*.53f);
                        DrawRect(new Rect2(-7,-3,14+I%3*3,6),Ep(I%2==0 ? "A8AB99" : "75837A"));
                        DrawLine(new(-7,-3),new(7+I%3*3,-3),Ep("C5C3AC"),1);
                        DrawSetTransform(Vector2.Zero);
                    }
                for(int Side=-1;Side<=1;Side+=2)
                {
                    Vector2 A=C+new Vector2(Side*52,-48),B=C+new Vector2(Side*140,-10);
                    DrawLine(A,B,Ep("4E5F59"),6);
                    DrawLine(A-new Vector2(0,2),B-new Vector2(0,2),Ep("ABB3A4"),2);
                    for(int I=1;I<5;I++)
                    {
                        Vector2 P=A.Lerp(B,I/5f);
                        DrawLine(P-new Vector2(9,7),P+new Vector2(9,-7),Ep("8D7560"),3);
                    }
                }
            }
            else if (Id == 13)
            {
                Color Paint = new("CFB46E", .7f), Worn = new("C8B077", .35f);
                // Longitudinal forklift aisle, marked loading rectangles and drainage at the seaward edge.
                float LaneY = C.Y + 6;
                for (float X = Floor.Position.X + 18; X < Floor.End.X - 20; X += 28)
                    DrawLine(new Vector2(X, LaneY), new Vector2(X + 15, LaneY), Worn, 2);
                foreach (float X in new[] { -142f, 142f })
                {
                    var Bay = new Rect2(C + new Vector2(X - 99, -135), new Vector2(198, 65));
                    DrawRect(Bay, Paint, false, 2);
                }
                DrawRect(new Rect2(C+new Vector2(-255,73),new Vector2(197,55)),Paint,false,2);
                for (int I = 0; I < 7; I++)
                {
                    Vector2 A = C + new Vector2(-35 + I * 11, 103);
                    DrawLine(A, A + new Vector2(18, -19), Paint, 3);
                }
                for (float Y = Floor.Position.Y + 20; Y < Floor.End.Y - 12; Y += 8)
                    DrawRect(new Rect2(Floor.End.X - 10, Y, 5, 3), new Color("4C5552"));
                for (int Side = -1; Side <= 1; Side += 2)
                    for (int I = 0; I < 13; I++)
                    {
                        Vector2 P = C + new Vector2(-104 + I * 10, 46 + Side * 9 + I % 3);
                        DrawLine(P, P + new Vector2(6, 0), new Color("515A54", .38f), 2);
                    }
                DrawSetTransform(C + new Vector2(-102, 44), 0, new Vector2(1.4f, .6f));
                DrawCircle(Vector2.Zero, 15, new Color("414B46", .5f)); DrawSetTransform(Vector2.Zero);
                // Entry inspection lane, drainage and a rolling-gate track inside the expanded perimeter.
                Vector2 Gate=World.CompoundGates[13];
                DrawLine(Gate-new Vector2(75,0),Gate+new Vector2(75,0),Ep("444E4C"),4);
                DrawLine(Gate-new Vector2(75,-5),Gate+new Vector2(75,5),Ep("AFB2A0"),1);
                for(int I=0;I<7;I++)
                {
                    Vector2 P=C+new Vector2(-20+I*7,121);
                    DrawLine(P,P+new Vector2(-8,12),Paint,2);
                }
            }
        }
    }
}
