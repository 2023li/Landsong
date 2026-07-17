namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 由纯表现 Prefab 上的组件实现。运行时表现控制器在建筑状态变化后调用，
    /// Consumer 只能读取建筑能力并刷新视觉，不得修改玩法状态或持有存档真相。
    /// </summary>
    public interface IBuildingViewStateConsumer
    {
        void Refresh(BuildingBase building);
    }

    /// <summary>
    /// 可选的纯表现配置校验入口。终态架构校验器会检查实现者，
    /// 让后续新增状态表现组件也能复用同一条资产验收链。
    /// </summary>
    public interface IBuildingViewStateConfiguration
    {
        bool TryValidateConfiguration(out string error);
    }
}
