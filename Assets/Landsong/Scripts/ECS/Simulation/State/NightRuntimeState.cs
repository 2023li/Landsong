using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct NightRuntimeState : IComponentData
    {
        public NightKind Kind;
        public uint Seed;
        public float Duration;
        public byte Speed;
        public int Threat;
        public int StartCombatStrength;
        public float DeploymentTime;
        public byte BossEscaped;
        public int BossReturnTurn;
        public int Intelligence;
    }
}
