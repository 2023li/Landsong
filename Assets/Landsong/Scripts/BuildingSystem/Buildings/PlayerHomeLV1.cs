using Landsong.BuildingSystem;
using UnityEngine;

public class PlayerHomeLV1 : BuildingBase
{

    protected override void OnRegistered()
    {
        GameSystem?.Dynasty?.RegisterPalace(this);
    }

    protected override void OnPlaced()
    {
        
    }
    protected override bool OnTurn()
    {
       return true;
    }

    protected override void OnDemolished()
    {
        GameSystem?.Dynasty?.UnregisterPalace(this);
    }
}
