#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS.Editor
{
    internal sealed class RewardTestFixture : IDisposable
    {
        readonly List<Action> releases = new List<Action>();
        BlobAssetReference<StartingRewardsBlob> rewards;
        internal static ItemId Item => ItemId.FromIndex(0);
        internal static BuildingId Building => BuildingId.FromIndex(0);
        internal static BuffId Buff => BuffId.FromIndex(0);
        internal static FeatureId Feature => FeatureId.FromIndex(0);

        internal BlobAssetReference<T> Keep<T>(BlobAssetReference<T> value)
            where T : unmanaged
        {
            releases.Add(() => value.Dispose());
            return value;
        }

        internal RewardTestFixture(EntityManager em, Entity root, bool invalidLast = false, bool storageOrder = false)
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<ItemCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "coin",
                    Name = "coin"
                };
                definitions[0].MaximumStack = 10;
                definitions[0].NaturalLossRate = storageOrder ? .1f : 0;
                em.AddComponentData(root, new ItemCatalog { Value = Keep(builder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<BuildingCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "building",
                    Name = "building"
                };
                definitions[0].MaximumLevel = 3;
                em.AddComponentData(root, new BuildingCatalog { Value = Keep(builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<BuffCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "buff",
                    Name = "buff"
                };
                if (storageOrder)
                {
                    var effects = builder.Allocate(ref definitions[0].Effects.Items, 1);
                    effects[0] = new ItemNumericEffect
                    {
                        Effect = NumericEffectKind.LossMultiplier,
                        Magnitude = 1
                    };
                }

                em.AddComponentData(root, new BuffCatalog { Value = Keep(builder.CreateBlobAssetReference<BuffCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<FeatureCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "feature.Test",
                    Name = "feature.Test"
                };
                em.AddComponentData(root, new FeatureCatalog { Value = Keep(builder.CreateBlobAssetReference<FeatureCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<StorageSlotCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 2);
                definitions[0].Metadata = new DefinitionMetadata
                {
                    Id = "normal"
                };
                definitions[0].DefaultLossMultiplier = 1;
                definitions[1].Metadata = new DefinitionMetadata
                {
                    Id = "preserved"
                };
                definitions[1].DefaultLossMultiplier = .1f;
                em.AddComponentData(root, new StorageSlotCatalog { Value = Keep(builder.CreateBlobAssetReference<StorageSlotCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var rootRewards = ref builder.ConstructRoot<StartingRewardsBlob>();
                var items = builder.Allocate(ref rootRewards.Rewards.Items, 1);
                items[0] = new ItemAmount
                {
                    Order = 1,
                    Item = Item,
                    Quantity = storageOrder ? 1 : 3
                };
                var buffs = builder.Allocate(ref rootRewards.Rewards.Buffs, 1);
                buffs[0] = new BuffReward
                {
                    Order = storageOrder ? 0 : 2,
                    Buff = Buff,
                    GrantedLevel = 1
                };
                if (!storageOrder)
                {
                    var blueprints = builder.Allocate(ref rootRewards.Rewards.Blueprints, 1);
                    blueprints[0] = new BlueprintReward
                    {
                        Building = Building,
                        GrantedLevel = 2
                    };
                    var features = builder.Allocate(ref rootRewards.Rewards.Features, 1);
                    features[0] = new FeatureReward
                    {
                        Order = 3,
                        Feature = invalidLast ? FeatureId.FromIndex(1) : Feature,
                        GrantedLevel = 1
                    };
                }

                rewards = Keep(builder.CreateBlobAssetReference<StartingRewardsBlob>(Allocator.Persistent));
            }
        }

        internal bool Apply(EntityManager em, Entity root, float multiplier = 1, Action<int> probe = null) => RewardDelivery.Apply(em, root, ref rewards.Value.Rewards, multiplier: multiplier, probe: probe);
        public void Dispose()
        {
            for (int i = releases.Count - 1; i >= 0; i--)
                releases[i]();
        }
    }
}
#endif
