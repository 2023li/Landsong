using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingWorkforceLevel
    {
        public int Level;
        public ItemId Currency;
        public int Capacity;
        public int InitialWorkers;
        public bool InitialSubsidy;
        public float BaseAttraction;
        public float RecruitmentCost;
    }
}
