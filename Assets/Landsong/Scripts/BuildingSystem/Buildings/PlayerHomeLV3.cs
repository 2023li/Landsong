using Landsong.BuildingSystem;
using UnityEngine;

public class PlayerHomeLV3 : BuildingBase
{
    protected override void OnPlaced()
    {

    }

    protected override void OnRegistered()
    {
        GameSystem?.Dynasty?.RegisterPalace(this);
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
