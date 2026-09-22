using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct ExpeditionRequirement
    {
        public int Order;
        public ExpeditionId Expedition;
        public int Required;
    }
}
