using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct NightRules : IComponentData
    {
        [LabelText("入场警告时间（秒）")]
        public float WarningSeconds;
        [LabelText("出生保护时间（秒）")]
        public float ProtectionSeconds;
        [LabelText("每夜最少入场区域"), MinValue(1)]
        public int MinSpawnRegions;
        [LabelText("每夜最多入场区域"), MinValue(1)]
        public int MaxSpawnRegions;
        [LabelText("入场区域宽度（格）"), MinValue(1)]
        public int SpawnRegionSize;
        [LabelText("区域间隔（格）"), MinValue(0)]
        public int SpawnRegionGap;
        [LabelText("英雄战力权重")]
        public float HeroWeight;
        [LabelText("设施战力权重")]
        public float FacilityWeight;
        [LabelText("目标搜索半径")]
        public float TargetRadius;
        [LabelText("最低威胁预算")]
        public float ThreatFloor;
        [LabelText("每点战力预算上限")]
        public float ThreatPerStrengthCap;
        public static NightRules Default => new NightRules
        {
            WarningSeconds = 1,
            ProtectionSeconds = 1,
            MinSpawnRegions = 2,
            MaxSpawnRegions = 4,
            SpawnRegionSize = 5,
            SpawnRegionGap = 4,
            HeroWeight = .35f,
            FacilityWeight = 1,
            TargetRadius = 24,
            ThreatFloor = 36,
            ThreatPerStrengthCap = 1.5f
        };
    }

    public struct NightEventCatalog : IComponentData
    {
        public BlobAssetReference<NightEventCatalogBlob> Value;
    }

    public struct NightEventCatalogBlob
    {
        public BlobArray<NightEventDefinition> Events;
    }

    public struct NightEventDefinition
    {
        public FixedString64Bytes Id, FollowUp;
        public NightKind Kind;
        public int Priority, MinTurn, MaxTurn, Interval, Cooldown, WaveCount, ReturnDelay;
        public float Weight, BudgetScale;
        public NightWaveGeneratorSettings WaveGenerator;
        public byte Once, ReturnOnly, Forced;
        public FixedList512Bytes<float> WaveTimes;
        public BlobArray<NightEnemyChoice> Enemies;
        public NightEventConditions Conditions;
    }

    public enum NightWaveGeneratorKind : byte
    {
        Budget,
        FixedCount
    }

    public struct NightWaveGeneratorSettings
    {
        public NightWaveGeneratorKind Kind;
        public float MinimumCountScale, MaximumCountScale;
        public int FixedCount;
    }

    public struct NightEnemyChoice
    {
        public EnemyId Definition;
        public float Weight;
    }

    public struct NightBuildingCondition
    {
        public BuildingId Building;
        public int Count, MinimumLevel;
    }

    public struct NightItemCondition
    {
        public ItemId Item;
        public int Quantity;
    }

    public struct NightTechnologyCondition
    {
        public TechnologyId Technology;
        public int Count;
    }

    public struct NightEventConditions
    {
        public int MinimumTurn;
        public BlobArray<NightBuildingCondition> Buildings;
        public BlobArray<NightItemCondition> Items;
        public BlobArray<NightTechnologyCondition> Technologies;
        public DefinitionPrerequisites Completions;
    }

    public struct NightPlanState : IComponentData
    {
        public FixedString64Bytes Event;
        public int Turn, BaseThreat, PreparedTurn;
        public EnemyId BossDefinition;
        public float CombatElapsed, FirstActionAt;
        public byte ClockStarted, Committed, AnySpawned, BossKilled, BossEscaped;
    }

    [InternalBufferCapacity(0)]
    public struct NightEventHistory : IBufferElementData
    {
        public FixedString64Bytes Event;
        public int LastTurn, Count;
    }

    [InternalBufferCapacity(0)]
    public struct UnresolvedBoss : IBufferElementData
    {
        public FixedString64Bytes Event;
        public EnemyId Definition;
        public int DueTurn;
    }

    [InternalBufferCapacity(0)]
    public struct PreparedBuildingDefense : IBufferElementData
    {
        public BuildingId Definition;
        public CombatProfile Profile;
    }

    public struct CombatStatsSnapshot
    {
        public float Health, Damage, Speed, Range, Interval, ProjectileSpeed;
        public CombatProfile Combat;
    }

    [InternalBufferCapacity(0)]
    public struct PreparedSoldier : IBufferElementData
    {
        public SoldierId Definition;
        public CombatStatsSnapshot Stats;
    }

    [InternalBufferCapacity(0)]
    public struct PreparedHero : IBufferElementData
    {
        public HeroId Definition;
        public CombatStatsSnapshot Stats;
    }
}
