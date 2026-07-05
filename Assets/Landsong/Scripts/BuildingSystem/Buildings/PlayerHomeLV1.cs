using Landsong.BuildingSystem;

public class PlayerHomeLV1 : BuildingBase, IBuildingPopulationSource
{
    private const int InventorySlotCapacity = 5;
    private const int TechnologyPointsPerTurn = 1;
    private const int PopulationContribution = 10;

    public int CurrentPopulation => PopulationContribution;

    protected override void Awake()
    {
        base.Awake();
        EnsureInventorySlotCapacity();
        EnsureTechnologyPointsPerTurn();
    }

    public override string GetOverviewInfo()
    {
        return $"人口 +{PopulationContribution}，仓库 +{InventorySlotCapacity}格，科技点 +{TechnologyPointsPerTurn}/回合";
    }

    protected override void OnRegistered()
    {
        EnsureInventorySlotCapacity();
        EnsureTechnologyPointsPerTurn();
        UpdatePopulationContribution();
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
        ClearDynastyRegistration();
    }

    protected override void OnUnregistered()
    {
        ClearDynastyRegistration();
    }

    //点击
    protected override void OnClicked()
    {
    }

    private void EnsureInventorySlotCapacity()
    {
        var module = EnsureBuildingModule<BM_库存格容量>();
        module.SetProvidedSlotCount(InventorySlotCapacity);
    }

    private void EnsureTechnologyPointsPerTurn()
    {
        var module = EnsureBuildingModule<BM_科技点产出>();
        module.SetProvidedTechnologyPointsPerTurn(TechnologyPointsPerTurn);
    }

    private void UpdatePopulationContribution()
    {
        GameSystem?.Dynasty?.SetPopulationContribution(this, PopulationContribution);
    }

    private void ClearDynastyRegistration()
    {
        GameSystem?.Dynasty?.RemovePopulationContribution(this);
        GameSystem?.Dynasty?.UnregisterPalace(this);
    }
}
