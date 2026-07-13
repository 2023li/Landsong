namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 建筑模块生命周期接口。模块只实现自己关心的阶段，BuildingBase 按 ModuleSet 中的稳定顺序分发。
    /// </summary>
    public interface IBuildingModuleInitialized
    {
        void OnBuildingInitialized(BuildingBase building);
    }

    public interface IBuildingModuleRegistered
    {
        void OnBuildingRegistered(BuildingBase building);
    }

    public interface IBuildingModulePlaced
    {
        void OnBuildingPlaced(BuildingBase building);
    }

    public interface IBuildingModuleConstructionStarted
    {
        void OnBuildingConstructionStarted(BuildingBase building);
    }

    public interface IBuildingModuleConstructionCompleted
    {
        void OnBuildingConstructionCompleted(BuildingBase building);
    }

    public interface IBuildingModuleLevelApplied
    {
        void OnBuildingLevelApplied(BuildingBase building, int previousLevel, int currentLevel);
    }

    public interface IBuildingModuleUnregistered
    {
        void OnBuildingUnregistered(BuildingBase building);
    }

    public interface IBuildingModuleDemolished
    {
        void OnBuildingDemolished(BuildingBase building);
    }

    public interface IBuildingModuleClicked
    {
        void OnBuildingClicked(BuildingBase building);
    }

    public interface IBuildingModuleDoubleClicked
    {
        void OnBuildingDoubleClicked(BuildingBase building);
    }

    /// <summary>
    /// 每回合自动执行的模块。返回 false 会中止后续模块，保持确定性的失败传播。
    /// </summary>
    public interface IBuildingAutomaticTurnModule
    {
        bool ProcessAutomaticTurn(BuildingBase building);
    }
}
