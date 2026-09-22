using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentEffectScaling
    {
        public TalentScalingKind Kind;
        public ItemId SourceItem;
        public BuildingId SourceBuilding;
    }
}
