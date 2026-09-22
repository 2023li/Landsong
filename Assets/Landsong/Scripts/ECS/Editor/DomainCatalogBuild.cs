#if UNITY_EDITOR
using System;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class DomainCatalogBuild
    {
        public static int Validate()
        {
            var template = ContentAuthoringContext.Template();
            if (template == null)
                throw new InvalidOperationException("缺少显式模拟根组合模板。");
            var content = ContentAuthoringContext.Content();
            int count = 0;
            using (var blob = ItemCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = ItemGroupCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = StorageSlotCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = BuildingCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = BuildingLimitGroupCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = PolicyGroupCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = TechnologyCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = QuestCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = ExpeditionCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = SoldierCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = HeroCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = EnemyCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = BuffCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = PolicyCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = TalentCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = TalentSlotCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = RoyalTraitCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = FeatureCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = CropCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = ProjectileCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = OpportunityCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var blob = LootCatalogBaking.Compile(content))
                count += blob.Value.Definitions.Length;
            using (var starting = StartingRewardsAuthoring.Build(template.GetComponent<StartingRewardsAuthoring>()))
            {
            }

            using (var events = NightEventCatalogBaking.Compile(content))
            {
            }

            using (var portraits = PortraitLibraryBuilder.Build(template.GetComponent<PortraitLibraryAuthoring>().Portraits))
            {
            }

            CourtSettingsAuthoring.Validate(template.GetComponent<CourtSettingsAuthoring>().Settings);
            ExpeditionSettingsAuthoring.Validate(template.GetComponent<ExpeditionSettingsAuthoring>().Settings);
            IntelligenceSettingsAuthoring.Validate(template.GetComponent<IntelligenceSettingsAuthoring>().Settings);
            NightRulesAuthoring.Validate(template.GetComponent<NightRulesAuthoring>().Settings);
            NightSettingsAuthoring.Validate(template.GetComponent<NightSettingsAuthoring>().Settings);
            NightSettingsAuthoring.Validate(template.GetComponent<NightSettingsAuthoring>().DayReturn);
            PeacefulRulesAuthoring.Validate(template.GetComponent<PeacefulRulesAuthoring>().Settings);
            QuestGenerationSettingsAuthoring.Validate(template.GetComponent<QuestGenerationSettingsAuthoring>().Settings);
            RoyalFamilySettingsAuthoring.Validate(template.GetComponent<RoyalFamilySettingsAuthoring>().Settings);
            TalentSettingsAuthoring.Validate(template.GetComponent<TalentSettingsAuthoring>().Settings);
            var enemies = content.Enemies.Definitions;
            bool needsLoot = Array.Exists(enemies, asset => asset.SpecialDrops.Length != 0);
            var loot = content.Loot.Definitions;
            if (needsLoot && !Array.Exists(loot, asset => asset != null && asset.Prefab != null))
                throw new InvalidOperationException("敌军配置了特殊掉落，世界组合必须提供带实体预制体的掉落定义。");
            return count;
        }
    }
}
#endif
