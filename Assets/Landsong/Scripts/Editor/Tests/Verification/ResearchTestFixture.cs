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
    // The fixture has one research graph and four explicit reward recipients.
    // It never offers an untyped definition lookup to the tests.
    internal sealed class ResearchTestFixture : IDisposable
    {
        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        readonly List<Action> releases = new List<Action>();
        internal readonly TechnologyCatalogAsset Technologies;
        internal readonly ItemCatalogAsset Items;
        internal readonly BuildingCatalogAsset Buildings;
        internal readonly BuffCatalogAsset Buffs;
        internal readonly FeatureCatalogAsset Features;
        internal readonly TechnologyDefinitionAsset First, Target, Middle, Repeat;
        internal readonly ItemDefinitionAsset Coin;
        internal readonly BuildingDefinitionAsset Building;
        internal readonly BuffDefinitionAsset Buff;
        internal readonly FeatureDefinitionAsset ResearchAccess, RewardFeature;
        internal static readonly ItemId CoinId = ItemId.FromIndex(0);
        internal static readonly BuildingId BuildingId = Definitions.BuildingId.FromIndex(0);
        internal static readonly BuffId BuffId = Definitions.BuffId.FromIndex(0);
        internal static readonly FeatureId AccessId = FeatureId.FromIndex(0), RewardFeatureId = FeatureId.FromIndex(1);
        internal readonly TechnologyId FirstId, TargetId, MiddleId, RepeatId;
        T Asset<T>()
            where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            assets.Add(value);
            return value;
        }

        T Own<T>(T value)
            where T : ScriptableObject
        {
            assets.Add(value);
            return value;
        }

        BlobAssetReference<T> Keep<T>(BlobAssetReference<T> blob)
            where T : unmanaged
        {
            releases.Add(() => blob.Dispose());
            return blob;
        }

        static DefinitionMetadataSource Metadata(string id) => new DefinitionMetadataSource
        {
            Id = id,
            Name = id
        };
        TechnologyDefinitionAsset Technology(string id, int cost, bool repeat = false)
        {
            var asset = Asset<TechnologyDefinitionAsset>();
            asset.Metadata = Metadata(id);
            asset.ResearchPointCost = cost;
            asset.Repeatable = repeat;
            return asset;
        }

        internal ResearchTestFixture(bool authority = false)
        {
            Items = Asset<ItemCatalogAsset>();
            Coin = Asset<ItemDefinitionAsset>();
            Coin.Metadata = Metadata("coin");
            Coin.MaximumStack = 10;
            Items.Definitions = new[]
            {
                Coin
            };
            Buildings = Asset<BuildingCatalogAsset>();
            Building = Asset<BuildingDefinitionAsset>();
            Building.Metadata = Metadata("building");
            Building.MaximumLevel = 2;
            Buildings.Definitions = new[]
            {
                Building
            };
            Buffs = Asset<BuffCatalogAsset>();
            Buff = Asset<BuffDefinitionAsset>();
            Buff.Metadata = Metadata("buff");
            Buffs.Definitions = new[]
            {
                Buff
            };
            Features = Asset<FeatureCatalogAsset>();
            ResearchAccess = Asset<FeatureDefinitionAsset>();
            RewardFeature = Asset<FeatureDefinitionAsset>();
            ResearchAccess.Metadata = Metadata(ResearchOps.FeatureId);
            RewardFeature.Metadata = Metadata("feature.other");
            Features.Definitions = new[]
            {
                ResearchAccess,
                RewardFeature
            };
            Technologies = Asset<TechnologyCatalogAsset>();
            First = Technology("root", 5);
            Target = Technology(authority ? "dependent" : "leaf", authority ? 2 : 8);
            Repeat = Technology("repeat", authority ? 3 : 0, true);
            if (authority)
            {
                Technologies.Definitions = new[]
                {
                    First,
                    Repeat,
                    Target
                };
                FirstId = TechnologyId.FromIndex(0);
                RepeatId = TechnologyId.FromIndex(1);
                TargetId = TechnologyId.FromIndex(2);
                Parent(Target, First);
            }
            else
            {
                Middle = Technology("middle", 3);
                Technologies.Definitions = new[]
                {
                    First,
                    Target,
                    Middle,
                    Repeat
                };
                FirstId = TechnologyId.FromIndex(0);
                TargetId = TechnologyId.FromIndex(1);
                MiddleId = TechnologyId.FromIndex(2);
                RepeatId = TechnologyId.FromIndex(3);
                Parent(Target, Middle);
                Parent(Middle, First);
            }

            (authority ? First : Target).Rewards = new DefinitionRewardsSource
            {
                Items = new[]
                {
                    new ItemAmountSource
                    {
                        Order = 1,
                        Item = Coin,
                        Quantity = 2
                    }
                },
                Blueprints = new[]
                {
                    new BlueprintRewardSource
                    {
                        Order = 2,
                        Building = Building,
                        GrantedLevel = 2
                    }
                },
                Buffs = new[]
                {
                    new BuffRewardSource
                    {
                        Order = 3,
                        Buff = Buff,
                        GrantedLevel = 1
                    }
                },
                Features = new[]
                {
                    new FeatureRewardSource
                    {
                        Order = 4,
                        Feature = RewardFeature
                    }
                }
            };
            Repeat.Rewards.Items = new[]
            {
                new ItemAmountSource
                {
                    Item = Coin,
                    Quantity = 1
                }
            };
        }

        internal static void Parent(TechnologyDefinitionAsset child, TechnologyDefinitionAsset parent) => child.Prerequisites.TechnologyRequirements = new[]
        {
            new TechnologyRequirementSource
            {
                Technology = parent,
                Required = 1
            }
        };
        internal BlobAssetReference<TechnologyCatalogBlob> Compile() => TechnologyCatalogCompiler.Build(Technologies, new BuffCatalogIndex(Buffs), new BuildingCatalogIndex(Buildings), null, new FeatureCatalogIndex(Features), null, new ItemCatalogIndex(Items), null, null, null);
        internal void Install(EntityManager em, Entity root)
        {
            em.AddComponentData(root, new TechnologyCatalog { Value = Keep(Compile()) });
            var groups = Asset<ItemGroupCatalogAsset>();
            em.AddComponentData(root, new ItemCatalog { Value = Keep(ItemCatalogCompiler.Build(Items, new ItemGroupCatalogIndex(groups))) });
            em.AddComponentData(root, new ItemGroupCatalog { Value = Keep(ItemGroupCatalogCompiler.Build(groups)) });
            em.AddComponentData(root, new FeatureCatalog { Value = Keep(FeatureCatalogCompiler.Build(Features)) });
            // Only blueprint bounds and buff existence are needed by these research tests.
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<BuildingCatalogBlob>();
                var values = builder.Allocate(ref catalog.Definitions, 1);
                values[0].Metadata = new DefinitionMetadata
                {
                    Id = "building",
                    Name = "building"
                };
                values[0].MaximumLevel = 2;
                em.AddComponentData(root, new BuildingCatalog { Value = Keep(builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<BuffCatalogBlob>();
                var values = builder.Allocate(ref catalog.Definitions, 1);
                values[0].Metadata = new DefinitionMetadata
                {
                    Id = "buff",
                    Name = "buff"
                };
                em.AddComponentData(root, new BuffCatalog { Value = Keep(builder.CreateBlobAssetReference<BuffCatalogBlob>(Allocator.Persistent)) });
            }
        }

        internal static void AddFacts(EntityManager em, Entity root)
        {
            em.AddBuffer<BlueprintUnlock>(root);
            em.AddBuffer<OwnedBuff>(root);
            em.AddBuffer<UnlockedFeature>(root);
            em.AddBuffer<ClaimedQuest>(root);
            em.AddBuffer<CompletedExpedition>(root);
            em.AddBuffer<PolicyChoice>(root);
            em.AddBuffer<ResearchCompletedEvent>(root);
        }

        public void Dispose()
        {
            for (int i = releases.Count - 1; i >= 0; i--)
                releases[i]();
            foreach (var asset in assets)
                UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
#endif
