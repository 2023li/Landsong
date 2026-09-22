using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentBlueprintIncome
    {
        public int Order;
        public BuildingId Building;
        public float BaseLevel;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
