using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingCapabilitiesSource
    {
        [LabelText("建造与施工")]
        public BuildingConstructionSource Construction = new BuildingConstructionSource();
        [LabelText("升级与经验")]
        public BuildingUpgradeSource Upgrade = new BuildingUpgradeSource();
        [LabelText("维护与修复")]
        public BuildingMaintenanceSource Maintenance = new BuildingMaintenanceSource();
        [LabelText("物品生产")]
        public BuildingProductionSource Production = new BuildingProductionSource();
        [LabelText("任务与邀约")]
        public BuildingQuestsSource Quests = new BuildingQuestsSource();
        [LabelText("放置地形")]
        public BuildingPlacementSource Placement = new BuildingPlacementSource();
        [LabelText("桥梁与阶梯通行")]
        public BuildingTerrainConnectionSource Connection = new BuildingTerrainConnectionSource();
        [LabelText("仓储与资源提供")]
        public BuildingStorageSource Storage = new BuildingStorageSource();
        [LabelText("岗位与补贴")]
        public BuildingWorkforceSource Workforce = new BuildingWorkforceSource();
        [LabelText("人口与住宅")]
        public BuildingHousingSource Housing = new BuildingHousingSource();
        [LabelText("科研")]
        public BuildingResearchSource Research = new BuildingResearchSource();
        [LabelText("种植")]
        public BuildingFarmingSource Farming = new BuildingFarmingSource();
        [LabelText("采集")]
        public BuildingGatheringSource Gathering = new BuildingGatheringSource();
        [LabelText("驻军")]
        public BuildingGarrisonSource Garrison = new BuildingGarrisonSource();
        [LabelText("神殿供奉")]
        public BuildingSanctumSource Sanctum = new BuildingSanctumSource();
        [LabelText("市场")]
        public BuildingMarketSource Market = new BuildingMarketSource();
        [LabelText("范围效果")]
        public BuildingEffectsSource Effects = new BuildingEffectsSource();
        [LabelText("情报与警铃")]
        public BuildingDefenceSource Defence = new BuildingDefenceSource();
        [LabelText("远征")]
        public BuildingExpeditionsSource Expeditions = new BuildingExpeditionsSource();
    }
}
