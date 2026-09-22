#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Editor
{
    sealed class WorkforceTestFixture : IDisposable
    {
        readonly BlobAssetReference<ItemCatalogBlob> items;
        readonly BlobAssetReference<BuildingCatalogBlob> buildings;
        public WorkforceTestFixture()
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<ItemCatalogBlob>();
                builder.Allocate(ref root.Definitions, 1)[0] = new ItemDefinition
                {
                    Metadata = new DefinitionMetadata
                    {
                        Id = "coin"
                    },
                    TradeValue = 3,
                    MaximumStack = 10000
                };
                items = builder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent);
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BuildingCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 4);
                for (int i = 0; i < 4; i++)
                {
                    definitions[i].Metadata.Id = "b" + (i + 1);
                    definitions[i].Footprint = new int2(1);
                }

                definitions[2].PlacementAndVisuals.ProviderPriority = 2;
                builder.Allocate(ref definitions[0].Capabilities.Workforce.Levels, 1)[0] = new BuildingWorkforceLevel
                {
                    Capacity = 10,
                    BaseAttraction = 20,
                    RecruitmentCost = 10,
                    Currency = ItemId.FromIndex(0)
                };
                builder.Allocate(ref definitions[0].Capabilities.Storage.Conditions, 1)[0] = new BuildingStorageCondition
                {
                    AttractionPenalty = 5
                };
                BuildingSpatialEffect Effect(int magnitude, string group, BuildingEffectStacking stacking = BuildingEffectStacking.HighestInGroup, int workers = 0, BuildingId target = default) => new BuildingSpatialEffect
                {
                    Type = BuildingEnvironmentKind.Beauty,
                    Magnitude = magnitude,
                    Group = group,
                    Stacking = stacking,
                    Radius = 4,
                    RequiredWorkers = workers,
                    Building = target
                };
                var first = builder.Allocate(ref definitions[1].Capabilities.Effects.Spatial, 3);
                first[0] = Effect(3, "a");
                first[1] = Effect(2, "add", BuildingEffectStacking.Additive);
                first[2] = Effect(4, "highest", BuildingEffectStacking.HighestOfKind);
                var second = builder.Allocate(ref definitions[2].Capabilities.Effects.Spatial, 4);
                second[0] = Effect(5, "a");
                second[1] = Effect(7, "highest", BuildingEffectStacking.HighestOfKind);
                second[2] = Effect(99, "workers", BuildingEffectStacking.Additive, 2);
                second[3] = Effect(99, "wrong", BuildingEffectStacking.Additive, 0, BuildingId.FromIndex(1));
                builder.Allocate(ref definitions[3].Capabilities.Effects.Spatial, 1)[0] = Effect(5, "a");
                buildings = builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
            }
        }

        public void Install(EntityManager em, Entity root)
        {
            em.AddComponentData(root, new ItemCatalog { Value = items });
            em.AddComponentData(root, new BuildingCatalog { Value = buildings });
            em.AddBuffer<OwnedBuff>(root);
            em.AddBuffer<PolicyChoice>(root);
        }

        public void Dispose()
        {
            items.Dispose();
            buildings.Dispose();
        }
    }
}
#endif
