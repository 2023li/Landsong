using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentResearchIncome
    {
        public int Order;
        public float BasePoints;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
