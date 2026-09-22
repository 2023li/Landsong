using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentFeatureIncome
    {
        public int Order;
        public FeatureId Feature;
        public float BaseLevel;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
