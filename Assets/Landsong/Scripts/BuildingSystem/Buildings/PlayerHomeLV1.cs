using System.Collections.Generic;
using Landsong.BuildingSystem;
using UnityEngine;

public class PlayerHomeLV1 : BuildingBase
{
    private const int InventorySlotCapacity = 5;

    protected override void Awake()
    {
        base.Awake();
        EnsureInventorySlotCapacity();
    }

    public override string GetOverviewInfo()
    {
        return $"仓库 +{InventorySlotCapacity}格";
    }

    protected override void OnRegistered()
    {
        EnsureInventorySlotCapacity();
        GameSystem?.Dynasty?.RegisterPalace(this);
    }

    protected override void OnPlaced()
    {
        
    }
    protected override bool OnTurn()
    {
       return true;
    }

   


    //拆除
    protected override void OnDemolished()
    {
        GameSystem?.Dynasty?.UnregisterPalace(this);
    }

    //点击
    protected override void OnClicked()
    {
       

    }

    private void EnsureInventorySlotCapacity()
    {
        var module = EnsureBuildingModule<BuildingInventorySlotCapacityModule>();
        module.SetProvidedSlotCount(InventorySlotCapacity);
    }
}
