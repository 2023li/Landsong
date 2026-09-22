using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class StartingRewardsAuthoring : MonoBehaviour
    {
        [LabelText("开局奖励")]
        public DefinitionRewardsSource Rewards = new DefinitionRewardsSource();
        public ItemCatalogAsset Items => GameContentSetAuthoring.Resolve<ItemCatalogAsset>(this);
        public BuildingCatalogAsset Buildings => GameContentSetAuthoring.Resolve<BuildingCatalogAsset>(this);
        public BuffCatalogAsset Buffs => GameContentSetAuthoring.Resolve<BuffCatalogAsset>(this);
        public FeatureCatalogAsset Features => GameContentSetAuthoring.Resolve<FeatureCatalogAsset>(this);
        public static BlobAssetReference<StartingRewardsBlob> Build(StartingRewardsAuthoring source)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<StartingRewardsBlob>();
                DefinitionRewardsCompiler.Compile(ref builder, source.Rewards, ref root.Rewards, new BuffCatalogIndex(source.Buffs), new BuildingCatalogIndex(source.Buildings), new FeatureCatalogIndex(source.Features), new ItemCatalogIndex(source.Items));
                return builder.CreateBlobAssetReference<StartingRewardsBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public sealed class Baker : Baker<StartingRewardsAuthoring>
        {
            public override void Bake(StartingRewardsAuthoring authoring)
            {
                DependsOn(authoring.Items);
                DependsOn(authoring.Buildings);
                DependsOn(authoring.Buffs);
                DependsOn(authoring.Features);
                foreach (var asset in authoring.Items.Definitions)
                    DependsOn(asset);
                foreach (var asset in authoring.Buildings.Definitions)
                    DependsOn(asset);
                foreach (var asset in authoring.Buffs.Definitions)
                    DependsOn(asset);
                foreach (var asset in authoring.Features.Definitions)
                    DependsOn(asset);
                var blob = Build(authoring);
                AddBlobAsset(ref blob, out _);
                AddComponent(GetEntity(TransformUsageFlags.None), new StartingRewards { Value = blob });
            }
        }
    }
}
