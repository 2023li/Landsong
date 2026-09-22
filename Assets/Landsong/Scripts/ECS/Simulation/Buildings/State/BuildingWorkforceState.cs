using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingWorkforceState : IComponentData
    {
        public int Workers;
        public int StableWorkers;
        public int WorkerTarget;
        public int ProtectionUntil;
        public int PaidSubsidy;
        public int PaidSubsidyTurn;
        public int SubsidyBudget;
        public byte Subsidy;
    }
}
