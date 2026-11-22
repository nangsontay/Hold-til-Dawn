using Godot;
using HoldTilDawn;

public partial class Main
{
    void CompoundBoundaryItems(GArray Items, Rect2 View)
    {
        if (InteriorActive) return;
        var W = (WorldGeometry)Geometry;
        for (int I = 0; I < W.CompoundBoundaries.Count; I++)
        {
            var S = W.CompoundBoundaries[I];
            if (new Rect2(S.A.Min(S.B),(S.B-S.A).Abs()).Grow(85).Intersects(View))
                Items.Add(new GArray(Mathf.Max(S.A.Y,S.B.Y),18,I));
        }
    }

    void DrawCompoundBoundary(int Index)
    {
        var S = ((WorldGeometry)Geometry).CompoundBoundaries[Index];
        Vector2 A = S.A, B = S.B, Lift = new(0,-S.Height);
        if (S.Id == 13)
        {
            // Chain-link mesh remains transparent, with sturdy posts, top rail and a concrete toe.
            DrawLine(A,B,Ep("535E59"),7);
            if (S.Wire.Length > 0) DrawMultiline(S.Wire,new Color("8D9E98",.75f),1);
            DrawLine(A+Lift,B+Lift,Ep("9DA9A0"),3);
            DrawLine(A+new Vector2(0,-5),B+new Vector2(0,-5),Ep("788A81"),2);
            DrawLine(A,A+Lift-new Vector2(0,7),Ep("414F4B"),6);
            DrawLine(A-new Vector2(1,0),A+Lift-new Vector2(1,7),Ep("B5B9A6"),2);
            DrawLine(B,B+Lift-new Vector2(0,7),Ep("657970"),4);
            // A rusted, bent security wire follows the top instead of floating over the entrance.
            for (int I = 0; I < 4; I++)
            {
                Vector2 P = A.Lerp(B,(I+.5f)/4)+Lift-new Vector2(0,5);
                DrawLine(P-new Vector2(3,3),P+new Vector2(3,2),Ep("9E9378"),1);
                DrawLine(P-new Vector2(3,-2),P+new Vector2(3,-3),Ep("9E9378"),1);
            }
            return;
        }
        FillStatic(S.Face,Ep(S.Side==0 ? "A8AA99" : S.Side==3 ? "8E9688" : "747F76"));
        FillStatic(S.Cap,Ep("C6C3A9"));
        DrawLine(A,B,Ep("515F57"),5);
        DrawLine(A-new Vector2(0,8),B-new Vector2(0,8),Ep("6B796C"),2);
        if (S.Side is 0 or 3)
        {
            float X0 = Mathf.Min(A.X,B.X)+4, X1 = Mathf.Max(A.X,B.X)-4;
            // Exposed brick beneath flaking plaster and diagonal settlement cracks.
            for (int Row=0;Row<3;Row++)
                for (float X=X0+(Row%2)*6;X<X1-8;X+=13)
                {
                    Vector2 P = new(X,A.Y-11-Row*7);
                    if ((int)X%3!=0) DrawRect(new Rect2(P,new Vector2(11,5)),Ep(Row%2==0 ? "8D7360" : "9B8267"));
                }
            Vector2 Crack = A.Lerp(B,.68f);
            DrawLine(Crack+new Vector2(-5,-S.Height+9),Crack+new Vector2(2,-24),Ep("56645D"),2);
            DrawLine(Crack+new Vector2(2,-24),Crack+new Vector2(-6,-9),Ep("56645D"),2);
            if (S.Side==0 && Index%2==0)
            {
                Vector2 P = A.Lerp(B,.43f)+new Vector2(-12,-49);
                DrawRect(new Rect2(P-new Vector2(3,3),new Vector2(30,27)),Ep("6D7C73"));
                DrawRect(new Rect2(P,new Vector2(24,21)),Ep("253B38"));
                DrawLine(P+new Vector2(10,0),P+new Vector2(13,21),Ep("8D9782"),2);
                DrawLine(P+new Vector2(0,11),P+new Vector2(24,8),Ep("94A08E"),2);
                DrawLine(P+new Vector2(17,1),P+new Vector2(21,8),Ep("C0C7AE"),1);
            }
        }
        // Twisted reinforcement and patchy vines make all four surviving sides visibly abandoned.
        Vector2 Rebar = A.Lerp(B,.35f)+Lift;
        DrawLine(Rebar,Rebar+new Vector2(2,-11),Ep("6D594B"),2);
        DrawLine(Rebar+new Vector2(2,-11),Rebar+new Vector2(9,-14),Ep("B19B78"),1);
        if (Index%3==0)
        {
            Vector2 P = A.Lerp(B,.18f);
            DrawLine(P-new Vector2(0,3),P-new Vector2(3,32),Ep("405E3E"),2);
            for(int I=0;I<5;I++) DrawRect(new Rect2(P+new Vector2(I%2==0 ? -7 : 0,-7-I*5),new Vector2(6,3)),Ep("6A8154"));
        }
    }
}
