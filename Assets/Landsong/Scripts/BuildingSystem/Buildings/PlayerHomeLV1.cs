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

    public override string GetBaseInfo()
    {
        return $"仓库 +{InventorySlotCapacity}格 xxx";
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

 

    protected override void OnDemolished()
    {
        GameSystem?.Dynasty?.UnregisterPalace(this);
    }

    protected override void OnClicked()
    {
        base.OnClicked();
        Debug.Log("1");

    }

    private void EnsureInventorySlotCapacity()
    {
        var module = EnsureBuildingModule<BuildingInventorySlotCapacityModule>();
        module.SetProvidedSlotCount(InventorySlotCapacity);
    }
}
