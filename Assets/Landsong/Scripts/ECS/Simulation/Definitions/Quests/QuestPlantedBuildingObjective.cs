using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct QuestPlantedBuildingObjective
    {
        public int Order;
        public FixedString64Bytes Key;
        public BuildingId Building;
        public int Count;
    }
}
