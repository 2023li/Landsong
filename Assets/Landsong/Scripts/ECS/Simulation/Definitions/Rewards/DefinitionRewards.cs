using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct DefinitionRewards
    {
        public BlobArray<ItemAmount> Items;
        public BlobArray<BlueprintReward> Blueprints;
        public BlobArray<BuffReward> Buffs;
        public BlobArray<FeatureReward> Features;
    }
}
