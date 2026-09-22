#if UNITY_EDITOR
using System;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    internal sealed class ProductionEffectFixture : IDisposable
    {
        readonly CatalogFixture.Scope scope = new CatalogFixture.Scope();
        internal readonly BlobAssetReference<BuildingCatalogBlob> Buildings;
        internal readonly BlobAssetReference<BuffCatalogBlob> Buffs;
        internal readonly BlobAssetReference<ItemCatalogBlob> Items;
        static T Formal<T>(string domain)
            where T : ScriptableObject => AssetDatabase.LoadAssetAtPath<T>("Assets/Landsong/ECSContent/Catalogs/Source/" + domain + "Catalog.asset");
        internal ProductionEffectFixture(BuildingId core, BuffId buff, ItemId item, int flat, float percentage)
        {
            var buildings = scope.Clone(Formal<BuildingCatalogAsset>("Building"));
            var buffs = scope.Clone(Formal<BuffCatalogAsset>("Buff"));
            var items = scope.Clone(Formal<ItemCatalogAsset>("Item"));
            foreach (var asset in items.Definitions)
                asset.NaturalLossRate = 0;
            var original = buildings.Definitions[core.Index].Capabilities;
            buildings.Definitions[core.Index].Capabilities = new BuildingCapabilitiesSource
            {
                Housing = new BuildingHousingSource
                {
                    Enabled = true,
                    Population = original.Housing.Population
                },
                Storage = new BuildingStorageSource
                {
                    Enabled = true,
                    Warehouses = original.Storage.Warehouses,
                    Providers = original.Storage.Providers
                },
                Quests = new BuildingQuestsSource
                {
                    Enabled = true,
                    Capacity = original.Quests.Capacity
                },
                Garrison = original.Garrison,
                Production = new BuildingProductionSource
                {
                    Enabled = true,
                    Cycles = new[]
                    {
                        new BuildingProductionCycleSource
                        {
                            Interval = 1
                        }
                    },
                    Outputs = new[]
                    {
                        new BuildingProductionOutputSource
                        {
                            Item = items.Definitions[item.Index],
                            Quantity = 1
                        }
                    }
                }
            };
            buffs.Definitions[buff.Index].Effects = new DefinitionEffectsSource
            {
                FlatProduction = new[]
                {
                    new FlatProductionEffectSource
                    {
                        Item = items.Definitions[item.Index],
                        Quantity = flat
                    }
                },
                Items = new[]
                {
                    new ItemNumericEffectSource
                    {
                        Target = items.Definitions[item.Index],
                        Effect = NumericEffectKind.ProductionMultiplier,
                        Magnitude = percentage
                    }
                }
            };
            var groupIndex = new ItemGroupCatalogIndex(Formal<ItemGroupCatalogAsset>("ItemGroup"));
            var itemIndex = new ItemCatalogIndex(items);
            var buildingIndex = new BuildingCatalogIndex(buildings);
            var heroIndex = new HeroCatalogIndex(Formal<HeroCatalogAsset>("Hero"));
            var soldierIndex = new SoldierCatalogIndex(Formal<SoldierCatalogAsset>("Soldier"));
            var technologyIndex = new TechnologyCatalogIndex(Formal<TechnologyCatalogAsset>("Technology"));
            Items = ItemCatalogCompiler.Build(items, groupIndex);
            Buildings = BuildingCatalogCompiler.Build(buildings, new BuildingLimitGroupCatalogIndex(Formal<BuildingLimitGroupCatalogAsset>("BuildingLimitGroup")), new CropCatalogIndex(Formal<CropCatalogAsset>("Crop")), heroIndex, itemIndex, groupIndex, soldierIndex, new StorageSlotCatalogIndex(Formal<StorageSlotCatalogAsset>("StorageSlot")), technologyIndex);
            Buffs = BuffCatalogCompiler.Build(buffs, buildingIndex, heroIndex, itemIndex, soldierIndex, new TalentCatalogIndex(Formal<TalentCatalogAsset>("Talent")), technologyIndex);
        }

        internal void Install(EntityManager em, Entity root)
        {
            em.SetComponentData(root, new BuildingCatalog { Value = Buildings });
            em.SetComponentData(root, new BuffCatalog { Value = Buffs });
            em.SetComponentData(root, new ItemCatalog { Value = Items });
        }

        public void Dispose()
        {
            if (Buffs.IsCreated)
                Buffs.Dispose();
            if (Buildings.IsCreated)
                Buildings.Dispose();
            if (Items.IsCreated)
                Items.Dispose();
            scope.Dispose();
        }
    }
}
#endif
