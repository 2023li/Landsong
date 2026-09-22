using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentBuffIncome
    {
        public int Order;
        public BuffId Buff;
        public float BaseLevel;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
