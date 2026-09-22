#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(ItemDefinitionAsset))]
    public sealed class ItemDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Item领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<ItemCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Item领域目录。");
                var asset = (ItemDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(ItemGroupDefinitionAsset))]
    public sealed class ItemGroupDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到ItemGroup领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<ItemGroupCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少ItemGroup领域目录。");
                var asset = (ItemGroupDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(StorageSlotDefinitionAsset))]
    public sealed class StorageSlotDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到StorageSlot领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<StorageSlotCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少StorageSlot领域目录。");
                var asset = (StorageSlotDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(BuildingLimitGroupDefinitionAsset))]
    public sealed class BuildingLimitGroupDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到BuildingLimitGroup领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<BuildingLimitGroupCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少BuildingLimitGroup领域目录。");
                var asset = (BuildingLimitGroupDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(PolicyGroupDefinitionAsset))]
    public sealed class PolicyGroupDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到PolicyGroup领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<PolicyGroupCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少PolicyGroup领域目录。");
                var asset = (PolicyGroupDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(TechnologyDefinitionAsset))]
    public sealed class TechnologyDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Technology领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<TechnologyCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Technology领域目录。");
                var asset = (TechnologyDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(QuestDefinitionAsset))]
    public sealed class QuestDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Quest领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<QuestCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Quest领域目录。");
                var asset = (QuestDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(ExpeditionDefinitionAsset))]
    public sealed class ExpeditionDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Expedition领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<ExpeditionCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Expedition领域目录。");
                var asset = (ExpeditionDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(SoldierDefinitionAsset))]
    public sealed class SoldierDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Soldier领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<SoldierCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Soldier领域目录。");
                var asset = (SoldierDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(HeroDefinitionAsset))]
    public sealed class HeroDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Hero领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<HeroCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Hero领域目录。");
                var asset = (HeroDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(EnemyDefinitionAsset))]
    public sealed class EnemyDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Enemy领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<EnemyCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Enemy领域目录。");
                var asset = (EnemyDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(BuffDefinitionAsset))]
    public sealed class BuffDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Buff领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<BuffCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Buff领域目录。");
                var asset = (BuffDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(PolicyDefinitionAsset))]
    public sealed class PolicyDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Policy领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<PolicyCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Policy领域目录。");
                var asset = (PolicyDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(TalentDefinitionAsset))]
    public sealed class TalentDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Talent领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<TalentCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Talent领域目录。");
                var asset = (TalentDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(TalentSlotDefinitionAsset))]
    public sealed class TalentSlotDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到TalentSlot领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<TalentSlotCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少TalentSlot领域目录。");
                var asset = (TalentSlotDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(RoyalTraitDefinitionAsset))]
    public sealed class RoyalTraitDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到RoyalTrait领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<RoyalTraitCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少RoyalTrait领域目录。");
                var asset = (RoyalTraitDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(FeatureDefinitionAsset))]
    public sealed class FeatureDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Feature领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<FeatureCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Feature领域目录。");
                var asset = (FeatureDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(CropDefinitionAsset))]
    public sealed class CropDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Crop领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<CropCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Crop领域目录。");
                var asset = (CropDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(ProjectileDefinitionAsset))]
    public sealed class ProjectileDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Projectile领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<ProjectileCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Projectile领域目录。");
                var asset = (ProjectileDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(OpportunityDefinitionAsset))]
    public sealed class OpportunityDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Opportunity领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<OpportunityCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Opportunity领域目录。");
                var asset = (OpportunityDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(LootDefinitionAsset))]
    public sealed class LootDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            if (GUILayout.Button("注册到Loot领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<LootCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Loot领域目录。");
                var asset = (LootDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    [CustomEditor(typeof(BuildingDefinitionAsset))]
    public sealed class BuildingDefinitionInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("领域模板通过 Baking 生效。稳定标识用于存档，请勿随意重命名。", MessageType.Info);
            ContentAuthoringContext.DrawContext();
            base.OnInspectorGUI();
            BuildingModuleInspector.DrawSummary((BuildingDefinitionAsset)target);
            if (GUILayout.Button("注册到Building领域目录"))
            {
                var catalog = ContentAuthoringContext.Catalog<BuildingCatalogAsset>();
                if (catalog == null)
                    throw new InvalidOperationException("缺少Building领域目录。");
                var asset = (BuildingDefinitionAsset)target;
                ContentAuthoringRegistration.Register(catalog, asset);

                Selection.activeObject = catalog;
            }
        }
    }

    public static class ContentValidation
    {
        [MenuItem("Landsong/ECS/Night event catalog")]
        public static void OpenNightCatalog() => Selection.activeObject = ContentAuthoringContext.Catalog<NightEventCatalogAsset>();
        [MenuItem("Landsong/ECS/Validate native content")]
        public static void Validate() => Debug.Log("独立领域配置验证通过：" + DomainCatalogBuild.Validate() + " 个定义。");
    }
}
#endif
