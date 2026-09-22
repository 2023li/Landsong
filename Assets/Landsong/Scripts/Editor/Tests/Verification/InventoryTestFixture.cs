#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS.Editor
{
    sealed class InventoryTestFixture : IDisposable
    {
        readonly BlobAssetReference<ItemCatalogBlob> items;
        readonly BlobAssetReference<ItemGroupCatalogBlob> groups;
        readonly BlobAssetReference<StorageSlotCatalogBlob> slots;
        readonly BlobAssetReference<BuildingCatalogBlob> buildings;
        readonly BlobAssetReference<BuffCatalogBlob> buffs;
        readonly BlobAssetReference<FeatureCatalogBlob> features;
        public InventoryTestFixture()
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<ItemCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 2);
                definitions[0] = new ItemDefinition
                {
                    Metadata = new DefinitionMetadata
                    {
                        Id = "a-food",
                        Name = "a-food"
                    },
                    PrimaryGroup = ItemGroupId.FromIndex(0),
                    MaximumStack = 10,
                    NaturalLossRate = .1f
                };
                definitions[1] = new ItemDefinition
                {
                    Metadata = new DefinitionMetadata
                    {
                        Id = "b-wood",
                        Name = "b-wood"
                    },
                    MaximumStack = 10,
                    NaturalLossRate = .2f
                };
                items = builder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent);
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<ItemGroupCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 2);
                definitions[0] = new ItemGroupDefinition
                {
                    Metadata = new DefinitionMetadata
                    {
                        Id = "food"
                    },
                    ParentGroup = ItemGroupId.FromIndex(1)
                };
                definitions[1] = new ItemGroupDefinition
                {
                    Metadata = new DefinitionMetadata
                    {
                        Id = "parent"
                    }
                };
                groups = builder.CreateBlobAssetReference<ItemGroupCatalogBlob>(Allocator.Persistent);
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<StorageSlotCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 2);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "normal"
                };
                definitions[0].DefaultLossMultiplier = 1;
                definitions[1].Metadata = new DefinitionMetadata
                {
                    Id = "cold"
                };
                definitions[1].DefaultLossMultiplier = .5f;
                builder.Allocate(ref definitions[1].AcceptedGroups, 1)[0] = ItemGroupId.FromIndex(1);
                slots = builder.CreateBlobAssetReference<StorageSlotCatalogBlob>(Allocator.Persistent);
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BuildingCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 1);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "warehouse"
                };
                definitions[0].MaximumLevel = 1;
                definitions[0].Capabilities.Storage.Enabled = true;
                var rows = builder.Allocate(ref definitions[0].Capabilities.Storage.Warehouses, 2);
                rows[0] = new BuildingWarehouseLevel
                {
                    SlotType = StorageSlotId.FromIndex(1),
                    Slots = 1
                };
                rows[1] = new BuildingWarehouseLevel
                {
                    SlotType = StorageSlotId.FromIndex(0),
                    Slots = 1,
                    RequiredWorkers = 2
                };
                builder.Allocate(ref definitions[0].Capabilities.Storage.Conditions, 1)[0] = new BuildingStorageCondition
                {
                    RequiredWorkers = 2,
                    UnderstaffedLossMultiplier = 2,
                    MaintenanceLossPercent = 300
                };
                buildings = builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BuffCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 1);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "buff"
                };
                builder.Allocate(ref definitions[0].Effects.Items, 1)[0] = new ItemNumericEffect
                {
                    Effect = NumericEffectKind.LossMultiplier,
                    Magnitude = .5f
                };
                buffs = builder.CreateBlobAssetReference<BuffCatalogBlob>(Allocator.Persistent);
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<FeatureCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 1);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "feature.Inventory"
                };
                features = builder.CreateBlobAssetReference<FeatureCatalogBlob>(Allocator.Persistent);
            }
        }

        public void Install(EntityManager em, Entity root)
        {
            em.AddComponentData(root, new ItemCatalog { Value = items });
            em.AddComponentData(root, new ItemGroupCatalog { Value = groups });
            em.AddComponentData(root, new StorageSlotCatalog { Value = slots });
            em.AddComponentData(root, new BuildingCatalog { Value = buildings });
            em.AddComponentData(root, new BuffCatalog { Value = buffs });
            em.AddComponentData(root, new FeatureCatalog { Value = features });
            em.AddBuffer<OwnedBuff>(root);
            em.AddBuffer<PolicyChoice>(root);
            em.AddBuffer<UnlockedFeature>(root).Add(new UnlockedFeature { Feature = FeatureId.FromIndex(0) });
        }

        public void Dispose()
        {
            items.Dispose();
            groups.Dispose();
            slots.Dispose();
            buildings.Dispose();
            buffs.Dispose();
            features.Dispose();
        }
    }
}
#endif
