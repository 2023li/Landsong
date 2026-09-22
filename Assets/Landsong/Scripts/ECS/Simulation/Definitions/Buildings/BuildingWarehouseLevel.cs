using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingWarehouseLevel
    {
        public int Level;
        public StorageSlotId SlotType;
        public int Slots;
        public int RequiredWorkers;
    }
}
