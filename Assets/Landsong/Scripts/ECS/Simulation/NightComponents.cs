using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable] public struct NightRules
    {
        public float EntryLeadSeconds, WarningSeconds, ProtectionSeconds, SpawnSafety, BorderBuffer, HeroWeight, FacilityWeight, TargetRadius;
        public float ThreatFloor, ThreatPerStrengthCap;
        public static NightRules Default => new NightRules { EntryLeadSeconds = 2, WarningSeconds = 1, ProtectionSeconds = 1, SpawnSafety = 4, BorderBuffer = 2, HeroWeight = .35f, FacilityWeight = 1, TargetRadius = 24, ThreatFloor = 36, ThreatPerStrengthCap = 1.5f };
    }
    public struct NightEventDefinition
    {
        public FixedString64Bytes Id, FollowUp;
        public NightKind Kind;
        public int Priority, MinTurn, MaxTurn, Interval, Cooldown, WaveCount, ReturnDelay, PoolStart, PoolCount, ConditionStart, ConditionCount;
        public float Weight, BudgetScale, Duration;
        public byte Once, ReturnOnly, Forced;
        public FixedList512Bytes<float> WaveTimes;
    }
    public struct NightEnemyChoice { public int Definition; public float Weight; }
    public struct NightPlanState : IComponentData
    {
        public FixedString64Bytes Event;
        public int Turn, BaseThreat, PreparedTurn, BossDefinition;
        public float CombatElapsed, FirstActionAt;
        public byte ClockStarted, Committed, AnySpawned, BossKilled, BossEscaped;
    }
    [InternalBufferCapacity(0)] public struct NightEventHistory : IBufferElementData { public FixedString64Bytes Event; public int LastTurn, Count; }
    [InternalBufferCapacity(0)] public struct UnresolvedBoss : IBufferElementData { public FixedString64Bytes Event; public int Definition, DueTurn; }
    [InternalBufferCapacity(0)] public struct NightPreparation : IBufferElementData { public int Definition; public float Health, Damage, Speed, Range, Interval, ProjectileSpeed; public CombatProfile Combat; }
}
