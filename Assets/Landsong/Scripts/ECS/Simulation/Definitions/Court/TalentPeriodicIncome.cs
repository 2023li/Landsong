using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentPeriodicIncome
    {
        public BlobArray<TalentItemIncome> Items;
        public BlobArray<TalentScaledItemIncome> ScaledItems;
        public BlobArray<TalentResearchIncome> ResearchPoints;
        public BlobArray<TalentBlueprintIncome> Blueprints;
        public BlobArray<TalentBuffIncome> Buffs;
        public BlobArray<TalentFeatureIncome> Features;
    }
}
