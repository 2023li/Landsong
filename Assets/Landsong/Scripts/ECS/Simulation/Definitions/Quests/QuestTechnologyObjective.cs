using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct QuestTechnologyObjective
    {
        public int Order;
        public FixedString64Bytes Key;
        public TechnologyId Technology;
        public int Count;
    }
}
