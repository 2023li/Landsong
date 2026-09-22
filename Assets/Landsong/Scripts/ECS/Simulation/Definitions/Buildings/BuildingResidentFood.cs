using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingResidentFood
    {
        public int Level;
        public ItemGroupId FoodGroup;
        public int Varieties;
        public int AmountPerResident;
    }
}
