using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct QuestBuildingObjective
    {
        public int Order;
        public FixedString64Bytes Key;
        public BuildingId Building;
        public int Count;
        public int MinimumLevel;
        public bool CompletedOnly;
    }
}
