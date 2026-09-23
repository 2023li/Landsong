using Unity.Entities;

namespace Landsong.ECS
{
    public struct BuildingFireState : IComponentData
    {
        public byte Burning;
        public byte StartStrike;
        public int StartedTurn;
        // The first major phase index at which an unrescued building becomes ruined.
        public int DeadlinePhase;
        public ulong FailedStation;
    }
}
