#if UNITY_EDITOR
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;
using Landsong.Content;
using Landsong.ECS.Presentation;
using Landsong.EditorTools;

namespace Landsong.ECS.Editor
{
    // Editor build orchestration only; runtime consumers bind their own domain catalog.
    public static class DisplayCatalogBuild
    {
        [MenuItem("Landsong/ECS/Compile domain display catalogs")]
        public static void CompileCurrent()
        {
            DomainCatalogBuild.Validate();
            ItemDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<ItemCatalogAsset>());
            ItemGroupDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<ItemGroupCatalogAsset>());
            StorageSlotDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<StorageSlotCatalogAsset>());
            BuildingLimitGroupDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<BuildingLimitGroupCatalogAsset>());
            PolicyGroupDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<PolicyGroupCatalogAsset>());
            TechnologyDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<TechnologyCatalogAsset>());
            QuestDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<QuestCatalogAsset>());
            ExpeditionDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<ExpeditionCatalogAsset>());
            SoldierDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<SoldierCatalogAsset>());
            HeroDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<HeroCatalogAsset>());
            EnemyDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<EnemyCatalogAsset>());
            BuffDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<BuffCatalogAsset>());
            PolicyDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<PolicyCatalogAsset>());
            TalentDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<TalentCatalogAsset>());
            TalentSlotDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<TalentSlotCatalogAsset>());
            RoyalTraitDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<RoyalTraitCatalogAsset>());
            FeatureDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<FeatureCatalogAsset>());
            CropDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<CropCatalogAsset>());
            ProjectileDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<ProjectileCatalogAsset>());
            OpportunityDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<OpportunityCatalogAsset>());
            LootDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<LootCatalogAsset>());
            BuildingDisplayCatalogCompiler.Compile(ContentAuthoringContext.Catalog<BuildingCatalogAsset>());
            BindPrefabs();
            AssetDatabase.SaveAssets();
        }

        static void BindPrefabs()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ContentAssetPaths.UiPrefabs }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool ownsCatalog = source.GetComponent<UI_GamePanel_BuildingActionBar>() != null || source.GetComponent<UI_GamePanel_Technology>() != null || source.GetComponent<UI_GamePanel_Quest>() != null || source.GetComponent<UI_GamePanel_Policy>() != null || source.GetComponent<UI_GamePanel_Talent>() != null || source.GetComponent<UI_GamePanel_Court>() != null || source.GetComponent<UI_GamePanel_Hud>() != null || source.GetComponent<UI_GamePanel_Inventory>() != null || source.GetComponent<UI_GamePanel_BuildingDetails>() != null;
                if (!ownsCatalog)
                    continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (contents.TryGetComponent<UI_GamePanel_BuildingActionBar>(out var buildingactionbar))
                    {
                        buildingactionbar.BuildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingDisplayCatalog>(BuildingDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_Technology>(out var technology))
                    {
                        technology.Technologies = AssetDatabase.LoadAssetAtPath<TechnologyDisplayCatalog>(TechnologyDisplayCatalogCompiler.Path);
                        technology.RewardItems = AssetDatabase.LoadAssetAtPath<ItemDisplayCatalog>(ItemDisplayCatalogCompiler.Path);
                        technology.RewardBuildings = AssetDatabase.LoadAssetAtPath<BuildingDisplayCatalog>(BuildingDisplayCatalogCompiler.Path);
                        technology.RewardBuffs = AssetDatabase.LoadAssetAtPath<BuffDisplayCatalog>(BuffDisplayCatalogCompiler.Path);
                        technology.RewardFeatures = AssetDatabase.LoadAssetAtPath<FeatureDisplayCatalog>(FeatureDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_Quest>(out var quest))
                    {
                        quest.QuestDisplay = AssetDatabase.LoadAssetAtPath<QuestDisplayCatalog>(QuestDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_Policy>(out var policy))
                    {
                        policy.Policies = AssetDatabase.LoadAssetAtPath<PolicyDisplayCatalog>(PolicyDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_Talent>(out var talent))
                    {
                        talent.Talents = AssetDatabase.LoadAssetAtPath<TalentDisplayCatalog>(TalentDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_Court>(out var court))
                    {
                        court.Talents = AssetDatabase.LoadAssetAtPath<TalentDisplayCatalog>(TalentDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_Hud>(out var hud))
                    {
                        hud.Heroes = AssetDatabase.LoadAssetAtPath<HeroDisplayCatalog>(HeroDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_Inventory>(out var inventory))
                    {
                        inventory.Items = AssetDatabase.LoadAssetAtPath<ItemDisplayCatalog>(ItemDisplayCatalogCompiler.Path);
                    }

                    if (contents.TryGetComponent<UI_GamePanel_BuildingDetails>(out var buildingdetails))
                    {
                        buildingdetails.Items = AssetDatabase.LoadAssetAtPath<ItemDisplayCatalog>(ItemDisplayCatalogCompiler.Path);
                        buildingdetails.Crops = AssetDatabase.LoadAssetAtPath<CropDisplayCatalog>(CropDisplayCatalogCompiler.Path);
                    }

                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        static void ValidateBindings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ContentAssetPaths.UiPrefabs }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source.TryGetComponent<UI_GamePanel_BuildingActionBar>(out var buildingactionbar))
                {
                    if (buildingactionbar.BuildingCatalog != AssetDatabase.LoadAssetAtPath<BuildingDisplayCatalog>(BuildingDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Building显示目录：BuildingCatalog");
                }

                if (source.TryGetComponent<UI_GamePanel_Technology>(out var technology))
                {
                    if (technology.Technologies != AssetDatabase.LoadAssetAtPath<TechnologyDisplayCatalog>(TechnologyDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Technology显示目录：Technologies");
                    if (technology.RewardItems != AssetDatabase.LoadAssetAtPath<ItemDisplayCatalog>(ItemDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Item显示目录：RewardItems");
                    if (technology.RewardBuildings != AssetDatabase.LoadAssetAtPath<BuildingDisplayCatalog>(BuildingDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Building显示目录：RewardBuildings");
                    if (technology.RewardBuffs != AssetDatabase.LoadAssetAtPath<BuffDisplayCatalog>(BuffDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Buff显示目录：RewardBuffs");
                    if (technology.RewardFeatures != AssetDatabase.LoadAssetAtPath<FeatureDisplayCatalog>(FeatureDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Feature显示目录：RewardFeatures");
                }

                if (source.TryGetComponent<UI_GamePanel_Quest>(out var quest))
                {
                    if (quest.QuestDisplay != AssetDatabase.LoadAssetAtPath<QuestDisplayCatalog>(QuestDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Quest显示目录：QuestDisplay");
                }

                if (source.TryGetComponent<UI_GamePanel_Policy>(out var policy))
                {
                    if (policy.Policies != AssetDatabase.LoadAssetAtPath<PolicyDisplayCatalog>(PolicyDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Policy显示目录：Policies");
                }

                if (source.TryGetComponent<UI_GamePanel_Talent>(out var talent))
                {
                    if (talent.Talents != AssetDatabase.LoadAssetAtPath<TalentDisplayCatalog>(TalentDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Talent显示目录：Talents");
                }

                if (source.TryGetComponent<UI_GamePanel_Court>(out var court))
                {
                    if (court.Talents != AssetDatabase.LoadAssetAtPath<TalentDisplayCatalog>(TalentDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Talent显示目录：Talents");
                }

                if (source.TryGetComponent<UI_GamePanel_Hud>(out var hud))
                {
                    if (hud.Heroes != AssetDatabase.LoadAssetAtPath<HeroDisplayCatalog>(HeroDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Hero显示目录：Heroes");
                }

                if (source.TryGetComponent<UI_GamePanel_Inventory>(out var inventory))
                {
                    if (inventory.Items != AssetDatabase.LoadAssetAtPath<ItemDisplayCatalog>(ItemDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Item显示目录：Items");
                }

                if (source.TryGetComponent<UI_GamePanel_BuildingDetails>(out var buildingdetails))
                {
                    if (buildingdetails.Items != AssetDatabase.LoadAssetAtPath<ItemDisplayCatalog>(ItemDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Item显示目录：Items");
                    if (buildingdetails.Crops != AssetDatabase.LoadAssetAtPath<CropDisplayCatalog>(CropDisplayCatalogCompiler.Path))
                        throw new System.InvalidOperationException(path + " 缺少显式Crop显示目录：Crops");
                }
            }
        }

        public static void Validate()
        {
            ValidateBindings();
            ItemDisplayCatalogCompiler.Validate();
            ItemGroupDisplayCatalogCompiler.Validate();
            StorageSlotDisplayCatalogCompiler.Validate();
            BuildingLimitGroupDisplayCatalogCompiler.Validate();
            PolicyGroupDisplayCatalogCompiler.Validate();
            TechnologyDisplayCatalogCompiler.Validate();
            QuestDisplayCatalogCompiler.Validate();
            ExpeditionDisplayCatalogCompiler.Validate();
            SoldierDisplayCatalogCompiler.Validate();
            HeroDisplayCatalogCompiler.Validate();
            EnemyDisplayCatalogCompiler.Validate();
            BuffDisplayCatalogCompiler.Validate();
            PolicyDisplayCatalogCompiler.Validate();
            TalentDisplayCatalogCompiler.Validate();
            TalentSlotDisplayCatalogCompiler.Validate();
            RoyalTraitDisplayCatalogCompiler.Validate();
            FeatureDisplayCatalogCompiler.Validate();
            CropDisplayCatalogCompiler.Validate();
            ProjectileDisplayCatalogCompiler.Validate();
            OpportunityDisplayCatalogCompiler.Validate();
            LootDisplayCatalogCompiler.Validate();
            BuildingDisplayCatalogCompiler.Validate();
        }
    }
}
#endif
