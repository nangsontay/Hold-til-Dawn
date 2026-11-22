using Godot;
using System;
using HoldTilDawn;

public partial class Main
{
    static readonly Vector2[] ReefRoof = {new(-70,-66),new(-43,-95),new(56,-95),new(75,-66)};
    static readonly Vector2[] BeaconHood = {new(-21,-78),new(0,-95),new(21,-78)};
    static readonly Vector2[] CompassNorth = {new(0,-48),new(-9,0),new(0,-9),new(9,0)};
    static readonly Vector2[] CompassSouth = {new(0,48),new(-9,0),new(0,9),new(9,0)};

    void LookoutItems(GArray Items, Rect2 View)
    {
        if(InteriorActive) return;
        var W=(WorldGeometry)Geometry;
        for(int I=0;I<W.LookoutObjects.Count;I++)
        {
            var S=W.LookoutObjects[I];
            if(View.Grow(150).HasPoint(S.Pos)) Items.Add(new GArray(S.Pos.Y,19,I));
        }
        for(int I=0;I<W.LookoutRails.Count;I++)
        {
            var S=W.LookoutRails[I];
            if(View.Grow(70).HasPoint((S.A+S.B)/2)) Items.Add(new GArray(Mathf.Max(S.A.Y,S.B.Y),20,I));
        }
    }

    void DrawLookoutGround(WorldGeometry W, Rect2 View, float Time)
    {
        foreach(var Site in W.Lookouts.Values)
        {
            if(!new Rect2(Site.Centre-Site.Radii,Site.Radii*2).Grow(75).Intersects(View)) continue;
            Vector2 C=Site.Centre;
            if(Site.Id==12)
            {
                DrawRect(Site.Deck.Grow(5),Ep("536F68"));
                DrawRect(Site.Deck,Ep("B4B69A"));
                for(float Y=Site.Deck.Position.Y+3;Y<Site.Deck.End.Y;Y+=9)
                {
                    DrawLine(new(Site.Deck.Position.X+2,Y),new(Site.Deck.End.X-2,Y),Ep("7D9581"),2);
                    for(int I=0;I<4;I++)
                    {
                        Vector2 P=new(Site.Deck.Position.X+25+I*55,Y+2);
                        DrawRect(new Rect2(P,new Vector2(1,1)),Ep("506D65"));
                    }
                }
                // Shell fragments and bleached coral rubble identify the whole clearing, not only the pavilion.
                for(int I=0;I<30;I++)
                {
                    float A=I*2.39996f;
                    Vector2 P=C+new Vector2(MathF.Cos(A)*164,MathF.Sin(A)*116);
                    DrawArc(P.Round(),3+I%3,Mathf.Pi,Mathf.Tau,7,Ep(I%2==0 ? "F1D6C2" : "E5DCC0"),2);
                    DrawLine(P+new Vector2(-2,1),P+new Vector2(2,1),Ep("AB9E8D"),1);
                }
                for(int I=0;I<5;I++)
                    DrawRect(new Rect2(C+new Vector2(-36,73+I*8),new Vector2(72,6)),Ep(I%2==0 ? "B0B398" : "8C9C85"));
            }
            else
            {
                FillStatic(W.CapeScarpFace,Ep("465F63")); FillStatic(W.CapeScarpCap,Ep("A4B2A5"));
                FillStatic(Site.Outline,Ep("A8B9B0"));
                for(int I=0;I<24;I++)
                {
                    Vector2 P=C+new Vector2(-105+(I*37%205),-81+(I*29%163));
                    if((P-C).Length()<55) continue;
                    DrawLine(P,P+new Vector2(7+I%9,3),Ep("6F8983"),1);
                    DrawLine(P+new Vector2(7+I%9,3),P+new Vector2(10+I%9,8),Ep("6F8983"),1);
                }
                DrawSetTransform(C);
                DrawArc(Vector2.Zero,56,0,Mathf.Tau,48,Ep("5B7F79"),2);
                DrawArc(Vector2.Zero,61,0,Mathf.Tau,48,Ep("D2D4B9"),2);
                for(int I=0;I<16;I++)
                {
                    Vector2 D=Vector2.FromAngle(I*Mathf.Tau/16);
                    DrawLine(D*(I%4==0 ? 46 : 51),D*56,Ep("577972"),2);
                }
                FillStatic(CompassNorth,Ep("476E6D"),false); FillStatic(CompassSouth,Ep("CCB88E"),false);
                DrawSetTransform(C,Mathf.Pi/2);
                FillStatic(CompassNorth,Ep("65857F"),false); FillStatic(CompassSouth,Ep("DBCFB0"),false);
                DrawSetTransform(Vector2.Zero);
                DrawCircle(C,6,Ep("D7C494")); DrawCircle(C,2,Ep("536E69"));
                DrawString(ThemeDB.FallbackFont,C+new Vector2(-4,-66),"N",HorizontalAlignment.Left,-1,12,Ep("3F6464"));
                DrawString(ThemeDB.FallbackFont,C+new Vector2(-4,78),"S",HorizontalAlignment.Left,-1,12,Ep("3F6464"));
            }
        }
        if(InteriorActive) return;
        for(int I=0;I<W.Coral.Count;I++)
        {
            Vector2 P=W.Coral[I];
            if(!View.Grow(32).HasPoint(P)) continue;
            FillStatic(W.ReefBeds[I],new Color(I%2==0 ? "55ADAB" : "78BBAE",.27f),false);
            Color Col=CORAL[I%CORAL.Length]; Col.A=.64f+.08f*MathF.Sin(Time*1.2f+I);
            if(I%3==0)
            {
                for(int K=-2;K<=2;K++)
                {
                    Vector2 D=Vector2.FromAngle((K+2)*.68f+.2f+I*1.4f);
                    Vector2 Tip=P+D*14;
                    DrawLine(P,Tip,Col,3);
                    DrawLine(P+D*8,Tip+D.Orthogonal()*4,Col.Lightened(.22f),2);
                }
            }
            else
                for(int K=0;K<5;K++)
                {
                    Vector2 Q=P+Vector2.FromAngle(K*2.4f+I)*(5+K%3*2);
                    DrawCircle(Q.Round(),4+K%3,Col);
                    DrawCircle((Q-new Vector2(1,1)).Round(),2,Col.Lightened(.3f));
                }
            DrawLine(P+new Vector2(-9,18),P+new Vector2(9,18),new Color("B8E3D7",.2f+.1f*MathF.Sin(Time+I)),1);
        }
        for(int I=0;I<W.CapeGulls.Count;I++)
        {
            Vector2 P=W.CapeGulls[I]+new Vector2(MathF.Sin(Time*.5f+I)*16,MathF.Cos(Time*.7f+I)*6);
            if(!View.HasPoint(P)) continue;
            float Wing=3+MathF.Sin(Time*3+I)*3;
            DrawLine(P-new Vector2(8,Wing),P,Ep("E1E4CC"),2);
            DrawLine(P,P+new Vector2(8,-Wing),Ep("E1E4CC"),2);
        }
    }

    void DrawLookoutRail(int Index)
    {
        var S=((WorldGeometry)Geometry).LookoutRails[Index];
        Vector2 A=S.A,B=S.B; bool Wood=S.Id==12;
        Color Body=Ep(Wood ? "729D91" : "738F8B"),Light=Ep(Wood ? "D4DAC1" : "C0CFC1");
        float H=Wood ? 28 : 17;
        DrawLine(A,B,Ep("4D6D66"),5);
        DrawLine(A-new Vector2(0,H),B-new Vector2(0,H),Light,5);
        if(Wood) DrawLine(A-new Vector2(0,12),B-new Vector2(0,12),Body,3);
        for(int I=0;I<=2;I++)
        {
            Vector2 P=A.Lerp(B,I/2f);
            DrawLine(P,P-new Vector2(0,H+3),Body,Wood ? 4 : 6);
            DrawLine(P-new Vector2(1,1),P-new Vector2(1,H+3),Light,1);
        }
    }

    void DrawLookoutObject(int Index)
    {
        var S=((WorldGeometry)Geometry).LookoutObjects[Index];
        DrawSetTransform(S.Pos);
        switch(S.Model)
        {
            case 0: // Coral pavilion, faded sea-green roof and salt-bleached posts.
                for(int Side=-1;Side<=1;Side+=2)
                {
                    DrawLine(new(Side*48,0),new(Side*48,-72),Ep("586F60"),8);
                    DrawLine(new(Side*48-2,-2),new(Side*48-2,-70),Ep("D9DCC1"),3);
                }
                DrawRect(new Rect2(-58,-22,116,7),Ep("7D9A84"));
                DrawLine(new(-56,-17),new(54,-17),Ep("D2D8BA"),2);
                FillStatic(ReefRoof,Ep("4D9D95"),false);
                DrawLine(new(-69,-65),new(74,-65),Ep("CEE0CA"),6);
                for(int I=0;I<13;I++)
                    DrawLine(new(-65+I*11,-69),new(-40+I*8,-92),Ep(I%2==0 ? "7DBAB0" : "397C79"),2);
                DrawLine(new(-43,-96),new(56,-96),Ep("B8D0BC"),3);
                DrawRect(new Rect2(-24,-57,48,13),Ep("DCD8B4"));
                DrawLine(new(-18,-50),new(17,-50),Ep("497A75"),2);
                break;
            case 1: // Illustrated reef identification board.
            case 5: // Maritime bearing/chart board with a different palette.
                DrawLine(new(-18,1),new(-18,-30),Ep("6B7863"),5);
                DrawLine(new(18,1),new(18,-30),Ep("6B7863"),5);
                DrawRect(new Rect2(-28,-59,56,38),Ep("5E7D72"));
                DrawRect(new Rect2(-24,-55,48,30),Ep(S.Model==1 ? "8AC5B7" : "D2C9A6"));
                if(S.Model==1)
                    for(int I=0;I<4;I++)
                    {
                        Vector2 P=new(-17+I*11,-32);
                        DrawLine(P,P+new Vector2(-3,-12),CORAL[I],2);
                        DrawLine(P-new Vector2(1,6),P+new Vector2(4,-13),CORAL[I],2);
                    }
                else
                {
                    DrawLine(new(-19,-48),new(9,-42),Ep("6F8C80"),2);
                    DrawLine(new(9,-42),new(-9,-28),Ep("6F8C80"),2);
                    DrawCircle(new(14,-34),6,Ep("8E7358"));
                    DrawLine(new(14,-42),new(14,-26),Ep("DBD5B7"),1);
                }
                break;
            case 2: // Air cylinders, fins and diving mask on a marine equipment rack.
                DrawRect(new Rect2(-24,-54,48,5),Ep("D7D8BA"));
                for(int Side=-1;Side<=1;Side+=2)
                {
                    DrawLine(new(Side*23,1),new(Side*23,-54),Ep("698777"),4);
                    DrawRect(new Rect2(Side*10-6,-39,12,32),Ep(Side<0 ? "D2BE82" : "9EBDC0"));
                    DrawRect(new Rect2(Side*10-3,-44,6,5),Ep("475F5A"));
                    DrawRect(new Rect2(Side*10-6,-19,12,4),Ep("426A65"));
                }
                DrawRect(new Rect2(-14,-50,28,10),Ep("456C70"));
                DrawRect(new Rect2(-10,-48,8,5),Ep("BAD8CB"));
                DrawRect(new Rect2(2,-48,8,5),Ep("BAD8CB"));
                DrawLine(new(16,-41),new(24,-51),Ep("BBA67A"),3);
                DrawLine(new(-17,-4),new(-26,7),Ep("3F7F83"),6);
                break;
            case 3: // Short navigation beacon: masonry plinth, striped body and amber lantern.
                DrawRect(new Rect2(-25,-8,50,15),Ep("536F70"));
                DrawRect(new Rect2(-20,-57,40,53),Ep("D3D7BF"));
                for(int I=0;I<3;I++) DrawRect(new Rect2(-20,-51+I*18,40,8),Ep("A46C53"));
                DrawRect(new Rect2(-17,-76,34,21),Ep("3D6468"));
                DrawRect(new Rect2(-13,-73,26,15),Ep("D8B36F"));
                for(int Side=-1;Side<=1;Side+=2) DrawLine(new(Side*7,-74),new(Side*7,-57),Ep("607773"),2);
                FillStatic(BeaconHood,Ep("426C70"),false);
                DrawCircle(new(0,-66),4,new Color(1.6f,1.05f,.43f,.75f+.15f*MathF.Sin((float)Clock*2)));
                DrawRect(new Rect2(-6,-19,12,16),Ep("4C6765"));
                break;
            case 4: // Storm signal mast, with staggered fabric strips rather than another hut.
                DrawLine(new(0,2),new(0,-101),Ep("4F6F70"),6);
                DrawLine(new(-2,0),new(-2,-101),Ep("BCC9BA"),2);
                DrawLine(new(-7,-103),new(7,-103),Ep("C4CBB2"),3);
                for(int J=0;J<3;J++)
                    for(int I=0;I<6;I++)
                        DrawRect(new Rect2(2+I*6,-96+J*24+MathF.Sin((float)Clock*2.2f+I*.5f+J)*3,6,12-I),Ep((I+J)%2==0 ? "CFA06F" : "E4D9B4"));
                DrawLine(new(0,-62),new(-18,4),Ep("8A9B88"),1);
                break;
            default: // Weathered bench.
                DrawLine(new(-22,0),new(-22,-14),Ep("546D64"),4);
                DrawLine(new(22,0),new(22,-14),Ep("546D64"),4);
                DrawRect(new Rect2(-28,-21,56,11),Ep(S.Id==12 ? "B6C1A2" : "A5AAA0"));
                DrawLine(new(-27,-19),new(27,-19),Ep("D0D6B9"),2);
                DrawRect(new Rect2(-28,-34,56,8),Ep("7D9885"));
                break;
        }
        DrawSetTransform(Vector2.Zero);
    }
}
