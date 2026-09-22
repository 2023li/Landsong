using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class SoldierRecruitQuote
    {
        public ResultCode Code;
        public int Quantity, EmptySlots, RemainingLimit, FreePopulation;
        public List<BuildingCost> Costs = new List<BuildingCost>();
    }
}
