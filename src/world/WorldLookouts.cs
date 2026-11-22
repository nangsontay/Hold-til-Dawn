using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGeometry
{
    public sealed record LookoutSite(int Id, Vector2 Centre, Vector2 Radii, Rect2 Deck, Vector2[] Outline);
    public readonly record struct LookoutObject(int Id, int Model, Vector2 Pos);
    public readonly record struct LookoutRail(int Id, Vector2 A, Vector2 B);
    public readonly Dictionary<int, LookoutSite> Lookouts = new();
    public readonly List<LookoutObject> LookoutObjects = new();
    public readonly List<LookoutRail> LookoutRails = new();
    public readonly List<(int Id, Vector2 Pos, float Radius)> LookoutPlacements = new();
    public readonly List<Vector2[]> ReefBeds = new();
    public readonly List<Vector2> CapeGulls = new();
    public Vector2[] CapeScarpFace = Array.Empty<Vector2>(), CapeScarpCap = Array.Empty<Vector2>();
    public static Vector2 LookoutRadii(int Id) => Id == 12 ? new(188,144) : new(184,168);

    Vector2 LookoutSpot(int Id)
    {
        Vector2 Want = At(Id) + (Id == 17 ? Tl(-4,12) : Tl(3,-10)), Half = LookoutRadii(Id);
        Vector2 Best = Want; float Score = float.PositiveInfinity;
        // Fit the complete scene before placing anything; coastal props no longer get scattered independently.
        for(int Y=-24;Y<=24;Y++)
            for(int X=-24;X<=24;X++)
            {
                Vector2 C = (Want + Tl(X*.5f,Y*.5f)).Round();
                float Cost = C.DistanceSquaredTo(Want);
                if(Cost>=Score) continue;
                bool Fits=true;
                for(int Dy=-2;Dy<=2 && Fits;Dy++)
                    for(int Dx=-2;Dx<=2;Dx++)
                    {
                        Vector2 P=C+Half*new Vector2(Dx*.5f,Dy*.5f);
                        if(!IslandMap.OnLand(P,22) || IslandMap.InRiver(P,48) || InClear(P,12) || NearPad(P)) { Fits=false; break; }
                    }
                if(Fits) { Score=Cost; Best=C; }
            }
        if(float.IsPositiveInfinity(Score)) throw new InvalidOperationException($"No dry footprint for lookout {Id}");
        return Best;
    }

    LookoutSite NewLookout(int Id, Vector2 DeckHalf)
    {
        Vector2 C=LookoutSpot(Id), Half=LookoutRadii(Id);
        Vector2[] Outline = Id==12
            ? new[] {C+new Vector2(-112,-70),C+new Vector2(112,-70),C+new Vector2(112,70),C+new Vector2(-112,70)}
            : new[] {C+new Vector2(-88,-98),C+new Vector2(88,-98),C+new Vector2(126,-60),C+new Vector2(126,60),
                C+new Vector2(88,98),C+new Vector2(-88,98),C+new Vector2(-126,60),C+new Vector2(-126,-60)};
        var Site=new LookoutSite(Id,C,Half,new Rect2(C-DeckHalf,DeckHalf*2),Outline);
        Lookouts[Id]=Site; LmAt[Id]=C;
        Floors.Add((Site.Deck,false)); Clear(C,Half.Length()+32);
        CompoundEntrances[Id]=C+new Vector2(0,(Id==12 ? 1 : -1)*(Half.Y-12));
        return Site;
    }

    void LookoutProp(LookoutSite Site, Vector2 Offset, string Sprite, float FootRadius=12)
    {
        Vector2 P=Site.Centre+Offset;
        Prop(P,Sprite); LookoutPlacements.Add((Site.Id,P,FootRadius));
    }
    void LookoutFixture(LookoutSite Site, Vector2 Offset, int Model, float Radius)
    {
        Vector2 P=Site.Centre+Offset;
        LookoutObjects.Add(new(Site.Id,Model,P)); LookoutPlacements.Add((Site.Id,P,Radius));
        if(Radius>0) Solid(P,Radius);
    }
    void LookoutRailing(int Id, Vector2 A, Vector2 B)
    {
        CompoundLine(A,B,7);
        int Count=Math.Max(1,(int)MathF.Ceiling(A.DistanceTo(B)/48));
        for(int I=0;I<Count;I++) LookoutRails.Add(new(Id,A.Lerp(B,I/(float)Count),A.Lerp(B,(I+1f)/Count)));
    }

    void BuildCoralLookout()
    {
        var Site=NewLookout(12,new(112,70)); Vector2 C=Site.Centre;
        // A bleached timber marine lookout, reef identification board and abandoned diving equipment.
        LookoutObjects.Add(new(12,0,C+new Vector2(0,-48)));
        LookoutPlacements.Add((12,C+new Vector2(-48,-48),9));
        LookoutPlacements.Add((12,C+new Vector2(48,-48),9));
        Solid(C+new Vector2(-48,-48),9); Solid(C+new Vector2(48,-48),9);
        LookoutFixture(Site,new(-146,24),1,15);
        LookoutFixture(Site,new(144,45),2,18);
        LookoutFixture(Site,new(-69,33),6,16);
        LookoutProp(Site,new(72,17),"lm_scope_0",9);
        LookoutProp(Site,new(-142,-67),"lm_buoys_0",10);
        LookoutProp(Site,new(133,81),"lm_lifering_0",10);
        LookoutProp(Site,new(-106,100),"lm_rope_0",8);
        Vector2[] E=Site.Outline;
        LookoutRailing(12,E[0],E[1]); LookoutRailing(12,E[0],E[3]); LookoutRailing(12,E[1],E[2]);
        LookoutRailing(12,E[3],C+new Vector2(-48,70)); LookoutRailing(12,C+new Vector2(48,70),E[2]);
        var R=new Random(1212);
        var Beds=new List<(Vector2 Pos,Vector2[] Edge)>();
        for(int Y=-340;Y<=340;Y+=36)
            for(int X=-340;X<=440;X+=36)
            {
                Vector2 P=C+new Vector2(X+R.Next(-14,15),Y+R.Next(-14,15));
                float D=IslandMap.Depth(P);
                if(D> -30 || D < -150 || IslandMap.InRiver(P,32)) continue;
                var Bed=new Vector2[12]; bool Wet=true;
                float Size=.8f+(float)R.NextDouble()*.45f;
                for(int I=0;I<Bed.Length;I++)
                {
                    float A=I*Mathf.Tau/Bed.Length;
                    float Edge=.88f+.15f*MathF.Sin(I*2.1f+X*.13f+Y*.07f);
                    Bed[I]=(P+new Vector2(MathF.Cos(A)*23,MathF.Sin(A)*15)*Size*Edge).Round();
                    if(IslandMap.OnLand(Bed[I],-8)) Wet=false;
                }
                if(!Wet) continue;
                Beds.Add((P.Round(),Bed));
            }
        Beds.Sort((A,B)=>A.Pos.DistanceSquaredTo(C).CompareTo(B.Pos.DistanceSquaredTo(C)));
        for(int I=0;I<Math.Min(42,Beds.Count);I++) { Coral.Add(Beds[I].Pos); ReefBeds.Add(Beds[I].Edge); }
    }

    void BuildSouthCapeLookout()
    {
        var Site=NewLookout(17,new(126,98)); Vector2 C=Site.Centre;
        // Cold slate, a compass terrace, a navigation beacon and storm-worn signal equipment.
        LookoutFixture(Site,new(110,28),3,21);
        LookoutFixture(Site,new(-128,-86),4,8);
        LookoutFixture(Site,new(116,-88),5,14);
        LookoutFixture(Site,new(-94,96),6,16);
        LookoutProp(Site,new(-82,17),"lm_scope_0",9);
        LookoutProp(Site,new(125,107),"lm_lifering_0",8);
        LookoutProp(Site,new(-139,48),"lm_rope_0",8);
        Vector2[] E=Site.Outline;
        for(int I=1;I<E.Length;I++) LookoutRailing(17,E[I],E[(I+1)%E.Length]);
        // North is the landward approach; preserve a wide gap in the parapet.
        LookoutRailing(17,E[0],C+new Vector2(-48,-98)); LookoutRailing(17,C+new Vector2(48,-98),E[1]);
        Vector2[] Rim={new(-138,77),new(-104,124),new(-54,147),new(45,144),new(124,110),new(151,59)};
        CapeScarpFace=new Vector2[Rim.Length*2]; CapeScarpCap=new Vector2[Rim.Length*2];
        for(int I=0;I<Rim.Length;I++)
        {
            Vector2 P=C+Rim[I];
            CapeScarpFace[I]=P; CapeScarpFace[^(I+1)]=P+new Vector2(0,12);
            CapeScarpCap[I]=P; CapeScarpCap[^(I+1)]=P-new Vector2(0,14);
        }
        for(int I=0;I<5;I++)
            for(int Y=180;Y<=600;Y+=12)
            {
                Vector2 P=C+new Vector2(-240+I*120,Y);
                if(IslandMap.OnLand(P,-48)) continue;
                CapeGulls.Add(P); break;
            }
    }

    public bool InLookoutPassage(Vector2 P)
    {
        foreach(var Site in Lookouts.Values)
            if(MathF.Abs(P.X-Site.Centre.X)<32 && Site.Deck.Grow(90).HasPoint(P)) return true;
        return false;
    }
}
