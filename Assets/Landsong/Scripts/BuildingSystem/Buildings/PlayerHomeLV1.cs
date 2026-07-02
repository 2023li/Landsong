using Landsong.BuildingSystem;
using UnityEngine;

public class PlayerHomeLV1 : BuildingBase, IResourceProviderPoint
{
    [SerializeField] private bool isResourceProviderPoint = true;

    public bool IsResourceProviderPoint => isResourceProviderPoint;

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

    protected override void OnClicked()
    {
        base.OnClicked();
        Debug.Log("1");

    }
}
