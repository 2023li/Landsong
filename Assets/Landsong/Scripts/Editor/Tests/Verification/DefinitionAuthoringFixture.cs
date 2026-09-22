#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    internal sealed class DefinitionAuthoringFixture : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal T Asset<T>()
            where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            owned.Add(value);
            return value;
        }

        internal readonly BuffCatalogAsset BuffCatalog;
        internal readonly BuffDefinitionAsset Buff;
        internal readonly BuildingCatalogAsset BuildingCatalog;
        internal readonly BuildingDefinitionAsset Building;
        internal readonly EnemyCatalogAsset EnemyCatalog;
        internal readonly EnemyDefinitionAsset Enemy;
        internal readonly ExpeditionCatalogAsset ExpeditionCatalog;
        internal readonly ExpeditionDefinitionAsset Expedition;
        internal readonly FeatureCatalogAsset FeatureCatalog;
        internal readonly FeatureDefinitionAsset Feature;
        internal readonly HeroCatalogAsset HeroCatalog;
        internal readonly HeroDefinitionAsset Hero;
        internal readonly ItemCatalogAsset ItemCatalog;
        internal readonly ItemDefinitionAsset Item;
        internal readonly LootCatalogAsset LootCatalog;
        internal readonly LootDefinitionAsset Loot;
        internal readonly OpportunityCatalogAsset OpportunityCatalog;
        internal readonly OpportunityDefinitionAsset Opportunity;
        internal readonly PolicyCatalogAsset PolicyCatalog;
        internal readonly PolicyDefinitionAsset Policy;
        internal readonly PolicyGroupCatalogAsset PolicyGroupCatalog;
        internal readonly PolicyGroupDefinitionAsset PolicyGroup;
        internal readonly QuestCatalogAsset QuestCatalog;
        internal readonly QuestDefinitionAsset Quest;
        internal readonly RoyalTraitCatalogAsset RoyalTraitCatalog;
        internal readonly RoyalTraitDefinitionAsset RoyalTrait;
        internal readonly SoldierCatalogAsset SoldierCatalog;
        internal readonly SoldierDefinitionAsset Soldier;
        internal readonly TalentCatalogAsset TalentCatalog;
        internal readonly TalentDefinitionAsset Talent;
        internal readonly TalentSlotCatalogAsset TalentSlotCatalog;
        internal readonly TalentSlotDefinitionAsset TalentSlot;
        internal readonly TechnologyCatalogAsset TechnologyCatalog;
        internal readonly TechnologyDefinitionAsset Technology;
        internal DefinitionAuthoringFixture()
        {
            BuffCatalog = Asset<BuffCatalogAsset>();
            Buff = Asset<BuffDefinitionAsset>();
            Buff.Metadata = new DefinitionMetadataSource
            {
                Id = "buff",
                Name = "Buff"
            };
            BuffCatalog.Definitions = new[]
            {
                Buff
            };
            BuildingCatalog = Asset<BuildingCatalogAsset>();
            Building = Asset<BuildingDefinitionAsset>();
            Building.Metadata = new DefinitionMetadataSource
            {
                Id = "building",
                Name = "Building"
            };
            BuildingCatalog.Definitions = new[]
            {
                Building
            };
            EnemyCatalog = Asset<EnemyCatalogAsset>();
            Enemy = Asset<EnemyDefinitionAsset>();
            Enemy.Metadata = new DefinitionMetadataSource
            {
                Id = "enemy",
                Name = "Enemy"
            };
            EnemyCatalog.Definitions = new[]
            {
                Enemy
            };
            ExpeditionCatalog = Asset<ExpeditionCatalogAsset>();
            Expedition = Asset<ExpeditionDefinitionAsset>();
            Expedition.Metadata = new DefinitionMetadataSource
            {
                Id = "expedition",
                Name = "Expedition"
            };
            ExpeditionCatalog.Definitions = new[]
            {
                Expedition
            };
            FeatureCatalog = Asset<FeatureCatalogAsset>();
            Feature = Asset<FeatureDefinitionAsset>();
            Feature.Metadata = new DefinitionMetadataSource
            {
                Id = "feature",
                Name = "Feature"
            };
            FeatureCatalog.Definitions = new[]
            {
                Feature
            };
            HeroCatalog = Asset<HeroCatalogAsset>();
            Hero = Asset<HeroDefinitionAsset>();
            Hero.Metadata = new DefinitionMetadataSource
            {
                Id = "hero",
                Name = "Hero"
            };
            HeroCatalog.Definitions = new[]
            {
                Hero
            };
            ItemCatalog = Asset<ItemCatalogAsset>();
            Item = Asset<ItemDefinitionAsset>();
            Item.Metadata = new DefinitionMetadataSource
            {
                Id = "item",
                Name = "Item"
            };
            ItemCatalog.Definitions = new[]
            {
                Item
            };
            LootCatalog = Asset<LootCatalogAsset>();
            Loot = Asset<LootDefinitionAsset>();
            Loot.Metadata = new DefinitionMetadataSource
            {
                Id = "loot",
                Name = "Loot"
            };
            LootCatalog.Definitions = new[]
            {
                Loot
            };
            OpportunityCatalog = Asset<OpportunityCatalogAsset>();
            Opportunity = Asset<OpportunityDefinitionAsset>();
            Opportunity.Metadata = new DefinitionMetadataSource
            {
                Id = "opportunity",
                Name = "Opportunity"
            };
            OpportunityCatalog.Definitions = new[]
            {
                Opportunity
            };
            PolicyCatalog = Asset<PolicyCatalogAsset>();
            Policy = Asset<PolicyDefinitionAsset>();
            Policy.Metadata = new DefinitionMetadataSource
            {
                Id = "policy",
                Name = "Policy"
            };
            PolicyCatalog.Definitions = new[]
            {
                Policy
            };
            PolicyGroupCatalog = Asset<PolicyGroupCatalogAsset>();
            PolicyGroup = Asset<PolicyGroupDefinitionAsset>();
            PolicyGroup.Metadata = new DefinitionMetadataSource
            {
                Id = "policygroup",
                Name = "PolicyGroup"
            };
            PolicyGroupCatalog.Definitions = new[]
            {
                PolicyGroup
            };
            QuestCatalog = Asset<QuestCatalogAsset>();
            Quest = Asset<QuestDefinitionAsset>();
            Quest.Metadata = new DefinitionMetadataSource
            {
                Id = "quest",
                Name = "Quest"
            };
            QuestCatalog.Definitions = new[]
            {
                Quest
            };
            RoyalTraitCatalog = Asset<RoyalTraitCatalogAsset>();
            RoyalTrait = Asset<RoyalTraitDefinitionAsset>();
            RoyalTrait.Metadata = new DefinitionMetadataSource
            {
                Id = "royaltrait",
                Name = "RoyalTrait"
            };
            RoyalTraitCatalog.Definitions = new[]
            {
                RoyalTrait
            };
            SoldierCatalog = Asset<SoldierCatalogAsset>();
            Soldier = Asset<SoldierDefinitionAsset>();
            Soldier.Metadata = new DefinitionMetadataSource
            {
                Id = "soldier",
                Name = "Soldier"
            };
            SoldierCatalog.Definitions = new[]
            {
                Soldier
            };
            TalentCatalog = Asset<TalentCatalogAsset>();
            Talent = Asset<TalentDefinitionAsset>();
            Talent.Metadata = new DefinitionMetadataSource
            {
                Id = "talent",
                Name = "Talent"
            };
            TalentCatalog.Definitions = new[]
            {
                Talent
            };
            TalentSlotCatalog = Asset<TalentSlotCatalogAsset>();
            TalentSlot = Asset<TalentSlotDefinitionAsset>();
            TalentSlot.Metadata = new DefinitionMetadataSource
            {
                Id = "talentslot",
                Name = "TalentSlot"
            };
            TalentSlotCatalog.Definitions = new[]
            {
                TalentSlot
            };
            TechnologyCatalog = Asset<TechnologyCatalogAsset>();
            Technology = Asset<TechnologyDefinitionAsset>();
            Technology.Metadata = new DefinitionMetadataSource
            {
                Id = "technology",
                Name = "Technology"
            };
            TechnologyCatalog.Definitions = new[]
            {
                Technology
            };
            Building.MaximumLevel = 3;
            Talent.MaximumLevel = 3;
            Quest.Behavior = QuestBehaviorFlags.Draft;
        }

        internal BlobAssetReference<TechnologyCatalogBlob> CompileTechnology() => TechnologyCatalogCompiler.Build(TechnologyCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new ExpeditionCatalogIndex(ExpeditionCatalog), new FeatureCatalogIndex(FeatureCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new QuestCatalogIndex(QuestCatalog), new SoldierCatalogIndex(SoldierCatalog), new TalentCatalogIndex(TalentCatalog));
        internal BlobAssetReference<QuestCatalogBlob> CompileQuest() => QuestCatalogCompiler.Build(QuestCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new ExpeditionCatalogIndex(ExpeditionCatalog), new FeatureCatalogIndex(FeatureCatalog), new ItemCatalogIndex(ItemCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
        internal BlobAssetReference<ExpeditionCatalogBlob> CompileExpedition() => ExpeditionCatalogCompiler.Build(ExpeditionCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new FeatureCatalogIndex(FeatureCatalog), new ItemCatalogIndex(ItemCatalog), new QuestCatalogIndex(QuestCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
        internal BlobAssetReference<EnemyCatalogBlob> CompileEnemy() => EnemyCatalogCompiler.Build(EnemyCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new FeatureCatalogIndex(FeatureCatalog), new ItemCatalogIndex(ItemCatalog));
        internal BlobAssetReference<OpportunityCatalogBlob> CompileOpportunity() => OpportunityCatalogCompiler.Build(OpportunityCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new FeatureCatalogIndex(FeatureCatalog), new ItemCatalogIndex(ItemCatalog));
        internal BlobAssetReference<LootCatalogBlob> CompileLoot() => LootCatalogCompiler.Build(LootCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new FeatureCatalogIndex(FeatureCatalog), new ItemCatalogIndex(ItemCatalog));
        internal BlobAssetReference<TalentCatalogBlob> CompileTalent() => TalentCatalogCompiler.Build(TalentCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new ExpeditionCatalogIndex(ExpeditionCatalog), new FeatureCatalogIndex(FeatureCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new QuestCatalogIndex(QuestCatalog), new RoyalTraitCatalogIndex(RoyalTraitCatalog), new SoldierCatalogIndex(SoldierCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
        internal BlobAssetReference<BuffCatalogBlob> CompileBuff() => BuffCatalogCompiler.Build(BuffCatalog, new BuildingCatalogIndex(BuildingCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new SoldierCatalogIndex(SoldierCatalog), new TalentCatalogIndex(TalentCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
        internal BlobAssetReference<PolicyCatalogBlob> CompilePolicy() => PolicyCatalogCompiler.Build(PolicyCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new ExpeditionCatalogIndex(ExpeditionCatalog), new FeatureCatalogIndex(FeatureCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new PolicyGroupCatalogIndex(PolicyGroupCatalog), new QuestCatalogIndex(QuestCatalog), new SoldierCatalogIndex(SoldierCatalog), new TalentCatalogIndex(TalentCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
        internal BlobAssetReference<TalentSlotCatalogBlob> CompileTalentSlot() => TalentSlotCatalogCompiler.Build(TalentSlotCatalog, new BuildingCatalogIndex(BuildingCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new RoyalTraitCatalogIndex(RoyalTraitCatalog), new SoldierCatalogIndex(SoldierCatalog), new TalentCatalogIndex(TalentCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
        internal BlobAssetReference<RoyalTraitCatalogBlob> CompileRoyalTrait() => RoyalTraitCatalogCompiler.Build(RoyalTraitCatalog, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new ExpeditionCatalogIndex(ExpeditionCatalog), new FeatureCatalogIndex(FeatureCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new QuestCatalogIndex(QuestCatalog), new SoldierCatalogIndex(SoldierCatalog), new TalentCatalogIndex(TalentCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
        internal BlobAssetReference<DefinitionRewards> Compile(DefinitionRewardsSource source)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var value = ref builder.ConstructRoot<DefinitionRewards>();
                DefinitionRewardsCompiler.Compile(ref builder, source, ref value, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new FeatureCatalogIndex(FeatureCatalog), new ItemCatalogIndex(ItemCatalog));
                return builder.CreateBlobAssetReference<DefinitionRewards>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        internal BlobAssetReference<DefinitionEffects> Compile(DefinitionEffectsSource source)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var value = ref builder.ConstructRoot<DefinitionEffects>();
                DefinitionEffectsCompiler.Compile(ref builder, source, ref value, new BuildingCatalogIndex(BuildingCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new SoldierCatalogIndex(SoldierCatalog), new TalentCatalogIndex(TalentCatalog), new TechnologyCatalogIndex(TechnologyCatalog));
                return builder.CreateBlobAssetReference<DefinitionEffects>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        internal BlobAssetReference<TalentJobEffects> Compile(TalentJobEffectsSource source)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var value = ref builder.ConstructRoot<TalentJobEffects>();
                TalentJobEffectsCompiler.Compile(ref builder, source, ref value, new BuildingCatalogIndex(BuildingCatalog), new HeroCatalogIndex(HeroCatalog), new ItemCatalogIndex(ItemCatalog), new SoldierCatalogIndex(SoldierCatalog));
                return builder.CreateBlobAssetReference<TalentJobEffects>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        internal BlobAssetReference<TalentPeriodicIncome> Compile(TalentPeriodicIncomeSource source)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var value = ref builder.ConstructRoot<TalentPeriodicIncome>();
                TalentPeriodicIncomeCompiler.Compile(ref builder, source, ref value, new BuffCatalogIndex(BuffCatalog), new BuildingCatalogIndex(BuildingCatalog), new FeatureCatalogIndex(FeatureCatalog), new ItemCatalogIndex(ItemCatalog));
                return builder.CreateBlobAssetReference<TalentPeriodicIncome>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(owned[i]);
        }
    }
}
#endif
