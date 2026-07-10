using System;
using System.Collections.Generic;

namespace Landsong.BuildingSystem.Buildings
{
    public sealed class BuildingUnderConstruction : BuildingBase, IBuildingResourceConsumptionSource
    {
        private string lastFailureStatusId = string.Empty;
        private string lastFailureStatusText = string.Empty;

        public IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions
        {
            get
            {
                if (!TryGetConstructionModules(out var consumptionModule, out var levelModule)
                    || levelModule.IsReadyToUpgrade)
                {
                    return EmptyResourceChanges;
                }

                return consumptionModule.GetResourceConsumptionsForTurn(levelModule.CurrentExperience);
            }
        }

        public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions =>
            TryGetModule<BM_施工材料消耗>(out var module)
                ? module.LastResourceConsumptions
                : EmptyResourceChanges;

        protected override void OnInitialized()
        {
            EnsureBuildingModule<BM_施工材料消耗>();
            EnsureBuildingModule<BM_等级升级>();
        }

        protected override void OnPlaced()
        {
        }

        protected override void OnRegistered()
        {
        }

        protected override bool OnTurn()
        {
            lastFailureStatusId = string.Empty;
            lastFailureStatusText = string.Empty;

            if (!TryGetConstructionModules(out var consumptionModule, out var levelModule))
            {
                lastFailureStatusId = BuildingRuntimeStatusCatalog.BS_消耗失败;
                lastFailureStatusText = "施工模块配置缺失";
                return false;
            }

            if (levelModule.IsReadyToUpgrade)
            {
                return true;
            }

            if (!consumptionModule.TryConsumeForTurn(
                    this,
                    levelModule.CurrentExperience,
                    out lastFailureStatusId,
                    out lastFailureStatusText))
            {
                return false;
            }

            levelModule.AddExperience(1);
            return true;
        }

        public override IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
        {
            List<BuildingRuntimeStatus> statuses = null;
            AppendCommonRuntimeStatuses(ref statuses);
            if (!string.IsNullOrWhiteSpace(lastFailureStatusId))
            {
                AppendRuntimeStatus(
                    ref statuses,
                    new BuildingRuntimeStatus(lastFailureStatusId, lastFailureStatusText));
            }

            return statuses ?? EmptyRuntimeStatuses;
        }

        public override string GetOverviewInfo()
        {
            if (!TryGetModule<BM_等级升级>(out var levelModule))
            {
                return "施工模块未配置";
            }

            return $"施工 {levelModule.CurrentExperience}/{levelModule.RequiredExperience}";
        }

        protected override void RestoreBuildingData(BuildingDataBase data)
        {
            if (data is not ResidentialHousingLV0LegacyData legacyData
                || !TryGetModule<BM_等级升级>(out var levelModule))
            {
                return;
            }

            levelModule.SetExperience(legacyData.Exp);
        }

        private bool TryGetConstructionModules(
            out BM_施工材料消耗 consumptionModule,
            out BM_等级升级 levelModule)
        {
            var hasConsumptionModule = TryGetModule(out consumptionModule);
            var hasLevelModule = TryGetModule(out levelModule);
            return hasConsumptionModule && hasLevelModule;
        }

        protected override void OnUnregistered()
        {
        }

        [Serializable]
        [BuildingDataTypeId("building.residential_housing.lv0")]
        private sealed class ResidentialHousingLV0LegacyData : BuildingDataBase
        {
            public int Exp = 0;
        }
    }
}
