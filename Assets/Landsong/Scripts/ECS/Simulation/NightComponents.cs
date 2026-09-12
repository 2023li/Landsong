using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable] public struct NightRules
    {
        [LabelText("入场提前时间（秒）")] public float EntryLeadSeconds;
        [LabelText("入场警告时间（秒）")] public float WarningSeconds;
        [LabelText("出生保护时间（秒）")] public float ProtectionSeconds;
        [LabelText("出生安全距离")] public float SpawnSafety;
        [LabelText("边界缓冲距离")] public float BorderBuffer;
        [LabelText("英雄战力权重")] public float HeroWeight;
        [LabelText("设施战力权重")] public float FacilityWeight;
        [LabelText("目标搜索半径")] public float TargetRadius;
        [LabelText("最低威胁预算")] public float ThreatFloor;
        [LabelText("每点战力预算上限")] public float ThreatPerStrengthCap;
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
