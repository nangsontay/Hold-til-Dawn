using Godot;
using HoldTilDawn;

public partial class Main
{
    public int BagExplorationChest = -1;
    public bool AtExplorationChestLocal(dynamic Me, int Id)
    {
        if (Me == null || Gd.Int(Gd.At(Me,5)) != 0 || Gd.Truth(Gd.At(Me,19))) return false;
        foreach (GArray C in SupplyRows())
            if (Gd.Int(C[0]) == Id && Gd.Truth(C[4]) && ((Vector2)LocalPos).DistanceSquaredTo((Vector2)C[1]) <= GameSim.CHEST_REACH*GameSim.CHEST_REACH)
            {
                dynamic View = Gd.Get(Gd.Get(Snap,"chests",new GDict()),MyId,null);
                return View != null && Gd.Int(Gd.At(View,0)) == Id;
            }
        return false;
    }
    GArray ExplorationChestSlots()
    {
        dynamic View = Gd.Get(Gd.Get(Snap,"chests",new GDict()),MyId,null);
        return View != null && Gd.Int(Gd.At(View,0)) == BagExplorationChest ? (GArray)Gd.At(View,1) : new GArray();
    }
    public void SyncChestPanel(dynamic Me, dynamic Previous)
    {
        if (Me == null || Gd.Size(Me) <= 35 || Gd.Int(Gd.At(Me,32)) < 0) return;
        if (Previous != null && Gd.Size(Previous) > 35 && Gd.Equal(Gd.At(Me,35),Gd.At(Previous,35))) return;
        BagExplorationChest = Gd.Int(Gd.At(Me,32));
        BagAtChest = false; BagOpen = true; CraftOpen = false; MapBig = false; CloseBuild(); DragFrom = null;
    }
    // Drawn on the same whole-pixel grid as the existing item icons, without external texture loads.
    static void DrawWorkshopIcon(CanvasItem C, string Id, Vector2 At, float S)
    {
        void Box(float X,float Y,float W,float H,string Hex) => C.DrawRect(new Rect2((At+new Vector2(X,Y)*S).Round(),new Vector2(W,H)*S),new Color(Hex));
        if (Id == "chest_key")
        {
            Box(-8,-5,8,8,"242526"); Box(-7,-4,6,6,"E3BC61"); Box(-5,-2,2,2,"33372E");
            Box(-1,-2,9,3,"E3BC61"); Box(4,1,2,4,"E3BC61"); Box(7,0,2,3,"E3BC61"); Box(-6,-4,3,1,"FFDD8B");
        }
        else
        {
            Box(-8,-5,16,12,"242B33"); Box(-7,-4,14,10,"527B89"); Box(-6,1,12,4,"345460");
            Box(-4,-8,8,4,"AFB6A7"); Box(-2,-7,4,2,"242B33"); Box(-7,-1,14,2,"B9C5B3");
            Box(2,-3,2,6,"E8BE6D"); Box(0,-1,6,2,"E8BE6D"); Box(-5,2,4,1,"9AAEA7");
        }
    }
}
