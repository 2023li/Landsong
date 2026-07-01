using Landsong.BuildingSystem;
using UnityEngine;

public class ResidentialHousingLV2 : BuildingBase
{
    [SerializeField, Min(0)] private int populationContribution = 2;

    protected override void OnPlaced()
    {
     
    }

    protected override void OnRegistered()
    {
        GameSystem?.Dynasty?.SetPopulationContribution(this, populationContribution);
    }

    protected override bool OnTurn()
    {
        return true;
    }

    protected override void OnUnregistered()
    {
        GameSystem?.Dynasty?.RemovePopulationContribution(this);
    }
}
