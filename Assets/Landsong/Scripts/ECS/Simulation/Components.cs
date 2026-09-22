using Landsong.ECS.Definitions;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum Phase : byte
    {
        Day,
        Settlement,
        Deployment,
        Night,
        Retreat,
        Celebration,
        Returning, // Reserved legacy value; current flow goes from dawn directly to Day/Report.
        Report,
        GameOver,
        Ended
    }

    public enum PersonGender : byte
    {
        [LabelText("未指定")]
        Unspecified,
        [LabelText("男性")]
        Male,
        [LabelText("女性")]
        Female
    }

    public enum NightKind : byte
    {
        [LabelText("平安夜")]
        Peaceful,
        [LabelText("入侵之夜")]
        Invasion,
        [LabelText("首领之夜")]
        Boss
    }

    public enum LifeStage : byte
    {
        [LabelText("施工中")]
        Construction,
        [LabelText("运营中")]
        Operational,
        [LabelText("荒废")]
        Ruined,
        [LabelText("修复中")]
        Repairing
    }

    public enum CommandKind : byte
    {
        Advance,
        Build,
        Demolish,
        Repair,
        Upgrade,
        Rename,
        Workers,
        Subsidy,
        Plant,
        RecruitSoldier,
        AssignSoldier,
        UnassignSoldier,
        SwapSoldiers,
        RecruitHero,
        Offering,
        WakeHero,
        Bell,
        Recall,
        SelectHero,
        MoveHero,
        FocusHero,
        Research,
        CancelResearch,
        SelectPolicy,
        AcceptQuest,
        RejectQuest,
        SubmitQuest,
        ClaimQuest,
        AbandonQuest,
        RecruitQuest,
        StartExpedition,
        ClaimExpedition,
        AbandonExpedition,
        RecruitTalent,
        AssignTalent,
        DismissTalent,
        RefreshTalents,
        Abdicate,
        PickUp,
        StorePending,
        Discard,
        CameraMoved,
        CameraZoomed,
        Pause,
        RetryDay,
        RetryDusk,
        Save,
        Load,
        EndDynasty,
        Harvest,
        AutoHarvest,
        MoveBuilding,
        BuildRoad,
        ClearCrop,
        ChangeBuildingSkin,
        ForecastEconomy,
        MoveInventory,
        SortInventory,
        StorePendingSlot,
        DiscardSlot,
        DiscardPending,
        WorkforceTarget,
        PlanResearch,
        TrackQuest,
        GiftPerson,
        CompleteSocialTask,
        ProposeMarriage,
        DesignateHeir,
        ExecuteHeir,
        RoyalVisit,
        CancelPolicy,
        NightSpeed,
        RenameSoldier,
        DismissSoldier,
        FillGarrison,
        RecallGarrison,
        IntelligenceMode,
        ReadIntelligence,
        ResolveMarriage,
        PrepareMarriage,
        ArrangeMarriage,
        RefusePersonRequest,
        WorkforceBudget,
        CustomizePortrait,
        SetSoldierAttention,
        RecruitWorkerAtQuotedCost,
        MoveInventoryToPending
    }

    public enum ResultCode : byte
    {
        Success,
        WrongPhase,
        InvalidTarget,
        InvalidContent,
        InsufficientResources,
        InsufficientPopulation,
        NoCapacity,
        Unavailable,
        InvalidPlacement,
        Busy,
        MissingResearch,
        QuestOverflow,
        ConfirmationRequired,
        PreparationFailed
    }

    public enum OrderKind : byte
    {
        Automatic,
        Move,
        Focus,
        Rally,
        Recall,
        Capture
    }

    public enum QuestStatus : byte
    {
        Offered,
        Active,
        Completed,
        Claimed
    }

    public enum ExpeditionStatus : byte
    {
        Travelling,
        Success,
        Failure
    }

    public enum EventKind : byte
    {
        Message,
        CommandResult,
        Damage,
        Ruin,
        Death,
        Reward,
        Theft,
        Save,
        Load,
        Retry,
        EndDynasty,
        DayCheckpoint,
        DuskCheckpoint,
        InventoryLost,
        ResidentsLost,
        JobsLost,
        RewardOverflow,
        HeroExperience,
        SoldierExperience,
        HeroWakeCost,
        TheftPrevented,
        VisitorEscaped,
        FairyCaught,
        VisitorCancelled,
        BossKilled,
        BossRetreated,
        HeroEffectiveTime,
        NightClosure,
        HeroOfferingCost,
        HeroOfferingExperience,
        SoldierDeath,
        HeroDeath,
        EnemyDeath
    }

    [System.Flags]
    public enum BuildingCategory
    {
        [LabelText("无")]
        None = 0,
        [LabelText("人口")]
        Population = 1,
        [LabelText("农业")]
        Agriculture = 2,
        [LabelText("工业")]
        Industry = 4,
        [LabelText("经济")]
        Economy = 8,
        [LabelText("科研")]
        Research = 16,
        [LabelText("民政")]
        Civic = 32,
        [LabelText("军事")]
        Military = 64,
        [LabelText("道路")]
        Road = 128,
        [LabelText("装饰")]
        Decoration = 256,
        [LabelText("奇观")]
        Wonder = 512
    }

    public struct BuildingPolicy
    {
        public BuildingCategory Category;
        public int MenuOrder, ProviderPriority, RepairTurns, SoldierRecruitLimit, SpawnExclusionPadding;
        public float MoveMaterialRatio, MoveExperienceRatio, RuinMovementCost;
        public byte CanMove, CanRotate;
    }

    public struct MapIdentity : IComponentData
    {
        public FixedString128Bytes Id;
    }

    [System.Serializable]
    public struct QuestGenerationSettings : IComponentData
    {
        [LabelText("任务强度步长")]
        public int StrengthStep;
        [LabelText("每点强度物品价值")]
        public int MarketValuePerStrength;
        [LabelText("低强度抽取权重")]
        public int4 Low;
        [LabelText("中强度抽取权重")]
        public int4 Medium;
        [LabelText("高强度抽取权重")]
        public int4 High;
        [LabelText("最高强度抽取权重")]
        public int4 Maximum;
        public static QuestGenerationSettings Default => new QuestGenerationSettings
        {
            StrengthStep = 10,
            MarketValuePerStrength = 100,
            Low = new int4(100, 35, 8, 1),
            Medium = new int4(45, 100, 35, 8),
            High = new int4(12, 45, 100, 35),
            Maximum = new int4(4, 15, 55, 100)
        };
    }

    [System.Serializable]
    public struct ExpeditionSettings : IComponentData
    {
        [LabelText("抚恤不足惩罚回合")]
        public int PenaltyTurns;
        [LabelText("每层岗位吸引力惩罚")]
        public float AttractionPerStack;
    }

    [System.Serializable]
    public struct SoldierGrowth
    {
        [LabelText("最高等级")]
        public int MaxLevel;
        [LabelText("首次升级所需经验")]
        public int FirstLevelExperience;
        [LabelText("每级经验增量")]
        public int ExperienceStep;
        [LabelText("有效参战经验")]
        public int BattleExperience;
        [LabelText("每级生命加成")]
        public float HealthPerLevel;
        [LabelText("每级攻击加成")]
        public float DamagePerLevel;
        public static SoldierGrowth Default => new SoldierGrowth
        {
            MaxLevel = 10,
            FirstLevelExperience = 20,
            ExperienceStep = 10,
            BattleExperience = 10,
            HealthPerLevel = .05f,
            DamagePerLevel = .05f
        };
    }

    [System.Serializable]
    public struct HeroGrowth
    {
        [LabelText("最高等级")]
        public int MaxLevel;
        [LabelText("首次升级所需经验")]
        public int FirstLevelExperience;
        [LabelText("每级经验增量")]
        public int ExperienceStep;
        [LabelText("每级生命加成")]
        public float HealthPerLevel;
        [LabelText("每级攻击加成")]
        public float DamagePerLevel;
        public SoldierGrowth Progression => new SoldierGrowth
        {
            MaxLevel = MaxLevel,
            FirstLevelExperience = FirstLevelExperience,
            ExperienceStep = ExperienceStep,
            HealthPerLevel = HealthPerLevel,
            DamagePerLevel = DamagePerLevel
        };

        [LabelText("供奉经验")]
        public int OfferingExperience;
        [LabelText("有效交战持续时间（秒）")]
        public float ContactSeconds;
        [LabelText("每秒交战经验")]
        public float ExperiencePerSecond;
        [LabelText("威胁参考值")]
        public float ThreatReference;
        [LabelText("威胁倍率上限")]
        public float MaximumThreatMultiplier;
        public static HeroGrowth Default => new HeroGrowth
        {
            MaxLevel = 10,
            FirstLevelExperience = 20,
            ExperienceStep = 10,
            HealthPerLevel = .05f,
            DamagePerLevel = .05f,
            OfferingExperience = 1,
            ContactSeconds = 3,
            ExperiencePerSecond = 1,
            ThreatReference = 100,
            MaximumThreatMultiplier = 3
        };
    }

    // Runtime-only contact intervals; no mid-battle saves. Rebuilt at each awakening/restore.
    public struct HeroCombat : IComponentData
    {
        public int Turn;
        public float EffectiveSeconds, ContactUntil, CountedUntil;
    }

    [InternalBufferCapacity(0)]
    public struct GameEvent : IBufferElementData
    {
        public EventKind Kind;
        public HistoryCategory Category;
        public ResultCode Result;
        public ulong RequestId, Target;
        public int Amount;
        public float3 Position;
        public FixedString128Bytes Message;
    }

    public struct Identity : IComponentData
    {
        public ulong Id;
        public FixedString128Bytes Name;
    }

    public struct Persistent : IComponentData
    {
    }

    public struct SimulationOwner : IComponentData
    {
        public Entity Root;
    }

    public struct NightTransient : IComponentData
    {
    }

    public struct SimulationReady : IComponentData
    {
    }

    public struct VisualState : IComponentData
    {
        public byte Visible, Ruined, Selected, Celebrating;
    }

    public struct Dead : IComponentData, IEnableableComponent
    {
    }

    public struct Health : IComponentData
    {
        public float Current, Maximum;
    }

    public struct Building : IComponentData
    {
        public LifeStage Stage;
        public int Level;
        public byte RuinPending;
    }

    public struct BuildingHousingStats : IComponentData
    {
        public int MaxPopulation;
        public int BasePopulation;
        public byte IsCore;
    }

    public enum EconomyReason : byte
    {
        Construction,
        Repair,
        Maintenance,
        Workforce,
        Production,
        Crop,
        Food,
        Tax,
        Offering,
        Market,
        NaturalLoss,
        CapacityTransfer,
        Research,
        TalentWage,
        TalentBenefit,
        Expedition,
        QuestPenalty,
        NightDiscard
    }

    public struct EconomyJournalState : IComponentData
    {
        public int Turn;
        public byte Recording, Forecast;
        public ulong Source;
        public EconomyReason Reason;
        public FixedString128Bytes SourceName;
    }

    [InternalBufferCapacity(0)]
    public struct EconomyEntry : IBufferElementData
    {
        public int Turn, Delta;
        public ItemId Item;
        public ulong Source;
        public EconomyReason Reason;
        public byte Pending;
        public FixedString128Bytes SourceName, Note;
    }

    public struct EconomyForecastState : IComponentData
    {
        public int Turn;
        public FixedString128Bytes Fingerprint;
    }

    [InternalBufferCapacity(0)]
    public struct EconomyForecastEntry : IBufferElementData
    {
        public EconomyEntry Value;
    }

    [InternalBufferCapacity(0)]
    public struct BuildingInvestment : IBufferElementData
    {
        public ItemId Item;
        public int Amount;
    }

    [InternalBufferCapacity(0)]
    public struct RepairMaterial : IBufferElementData
    {
        public ItemId Item;
        public int Amount;
    }

    [InternalBufferCapacity(0)]
    public struct FoodSelection : IBufferElementData
    {
        public ItemGroupId Group;
        public ItemId Item;
        public int Amount;
    }

    // Slots are 1-based; 0 means unassigned. RecallState: 0 normal, 1 returning, 2 home for this night.
    public struct Soldier : IComponentData
    {
        public ulong Garrison;
        public int Slot, PopulationCost, PendingSince, Experience, LastExperienceTurn;
        public byte RecallState;
    }

    public struct Hero : IComponentData
    {
        public ulong Sanctum;
        public int CooldownUntil, Experience, LastCombatTurn;
        public byte Recruited, DeathPending;
    }

    // IO identity and recovery metadata are intentionally outside rewindable snapshots.
    public struct RunPersistence : IComponentData
    {
        public FixedString64Bytes RunId;
        public byte Enabled;
    }

    public struct RecoveryState : IComponentData
    {
        public int Turn, LossCount, KnownIntel;
        public uint Seed;
        public byte AwaitingDecision, Extinction;
        public ulong IntelFingerprint, IntelReadFingerprint;
    }

    public struct NightResultState : IComponentData
    {
        public int Turn;
        public byte Committed;
    }

    // Ephemeral consent, not a save point. Binds the displayed loss list to an unchanged day.
    public struct NightEntryReview : IComponentData
    {
        public FixedString128Bytes Fingerprint;
        public ulong Token;
    }

    [InternalBufferCapacity(0)]
    public struct NightEntryLoss : IBufferElementData
    {
        public ulong Soldier;
        public ItemId Item;
        public int Amount;
        public FixedString128Bytes Name;
    }

    [InternalBufferCapacity(0)]
    public struct NightItemReward : IBufferElementData
    {
        public ulong Source;
        public int Entry, Sequence, Quantity;
        public ItemId Item;
        public FixedString128Bytes SourceName;
    }

    [InternalBufferCapacity(0)]
    public struct NightBlueprintReward : IBufferElementData
    {
        public ulong Source;
        public int Entry, Sequence, Level;
        public BuildingId Building;
        public FixedString128Bytes SourceName;
    }

    [InternalBufferCapacity(0)]
    public struct NightBuffReward : IBufferElementData
    {
        public ulong Source;
        public int Entry, Sequence, Level;
        public BuffId Buff;
        public FixedString128Bytes SourceName;
    }

    [InternalBufferCapacity(0)]
    public struct NightFeatureReward : IBufferElementData
    {
        public ulong Source;
        public int Entry, Sequence;
        public FeatureId Feature;
        public FixedString128Bytes SourceName;
    }

    public struct Combatant : IComponentData
    {
        public byte Faction, IsHero, IsBoss, Deployed, Participated, TargetMode;
        public float Damage, Range, Interval, Speed, ProjectileSpeed, NextAttack, DeployAt, DecisionAt, ProtectedUntil;
        public float3 Home;
        public float3 TargetAnchor;
        public int TargetRevision;
        public Entity Target;
        public ulong HomeId;
        public int Threat;
        public CombatProfile Profile;
    }

    public struct UnitOrder : IComponentData
    {
        public OrderKind Kind;
        public float3 Destination;
        public Entity Target;
        public ulong Source;
    }

    public struct Steering : IComponentData
    {
        public float3 Destination, Direction;
        public byte Moving;
    }

    public struct NavigationState : IComponentData
    {
        public int2 Goal;
        public int Revision;
        public float NextRepath, GoalHeight;
        public byte Failed;
    }

    public struct Perception : IComponentData
    {
        public Entity Enemy, Building;
        public float3 EnemyPosition, BuildingPosition;
        public byte EnemyInRange;
    }

    [InternalBufferCapacity(0)]
    public struct Waypoint : IBufferElementData
    {
        public float3 Position;
    }

    public struct Projectile : IComponentData
    {
        public Entity Source, Target;
        public float Damage, Speed, Lifetime, Penetration, Radius, Warning;
        public float3 Landing;
        public byte Faction;
        public ProjectileMode Mode;
        public ulong SourceId, TargetId;
    }

    [InternalBufferCapacity(0)]
    public struct DamageRequest : IBufferElementData
    {
        public Entity Source, Target;
        public float Amount, Penetration;
        public byte Faction, HasPayload;
    }

    [InternalBufferCapacity(0)]
    public struct InventorySlot : IBufferElementData
    {
        public ulong Provider;
        public int Index, Count;
        public StorageSlotId SlotType;
        public ItemId Item;
        public float LossRemainder;
        public byte Unavailable;
    }

    [InternalBufferCapacity(0)]
    public struct PendingItem : IBufferElementData
    {
        public ItemId Item;
        public int Amount;
        public float LossRemainder;
    }

    [InternalBufferCapacity(0)]
    public struct PolicyChoice : IBufferElementData
    {
        public PolicyId Definition;
    }

    [InternalBufferCapacity(0)]
    public struct BattleReportEntry : IBufferElementData
    {
        public EventKind Kind;
        public ulong Id;
        public BuildingId Building;
        public ItemId Item;
        public int Amount;
        public float Value;
        public FixedString128Bytes SourceName;
    }

    // At is a fraction of the battle clock, not seconds. Spawned: 0 pending, 1 spawned, 2 cancelled.
    [InternalBufferCapacity(0)]
    public struct NightWave : IBufferElementData
    {
        public float At, PowerScale, WarnedAt;
        public EnemyId Definition;
        public int Count, Direction, Region;
        public float3 Position;
        public ulong Target;
        public byte Spawned, Warned, SpatiallyBlocked;
    }

    [InternalBufferCapacity(0)]
    public struct SpawnRegion : IBufferElementData
    {
        public int Direction;
        public float3 Center, Size;
        public byte EdgeOnly;
    }

    public struct Opportunity : IComponentData
    {
        public ulong Provider;
        public byte Thief;
        public float Expires;
        public float3 Exit;
        public Entity Responder;
        public int PathIndex;
        public uint Random;
        public FixedString128Bytes SourceName;
    }

    public struct Loot : IComponentData
    {
        public ItemId Item;
        public int Count, Rarity;
        public FixedString128Bytes SourceName;
    }

    // An accepted continuation waiting for extra prerequisites retains its container with StartTurn/Deadline = 0.
    public struct Quest : IComponentData
    {
        public QuestStatus Status;
        public int StartTurn, Deadline;
        public ulong Source, Container;
        public int Slot, ContainerSlot;
        public byte Mainline;
    }

    public struct QuestTracking : IComponentData
    {
        public ulong Target;
        public byte Mode;
    } // 0 automatic continuation/value, 1 manual, 2 explicitly unpinned.

    [InternalBufferCapacity(0)]
    public struct QuestProgress : IBufferElementData
    {
        public int Amount;
        public FixedString64Bytes Key;
    }

    [InternalBufferCapacity(0)]
    public struct QuestOfferSlot : IBufferElementData
    {
        public int Type, Index, NextTurn;
    }

    public struct Expedition : IComponentData
    {
        public ulong Site, Captain;
        public FixedString128Bytes SourceName;
        public int Crew, Departure, Arrival, SourceLevel, Casualties, SubsidyRequired, SubsidyPaid, PenaltyStacks;
        public float RewardBonus, SuccessChance;
        public ExpeditionStatus Status;
    }

    [InternalBufferCapacity(0)]
    public struct ExpeditionSupply : IBufferElementData
    {
        public ItemId Item;
        public int Amount;
    }

    [InternalBufferCapacity(0)]
    public struct ExpeditionDestinationHistory : IBufferElementData
    {
        public ExpeditionId Definition;
    }

    public struct QuestCapacityReview : IComponentData
    {
        public int Turn, Capacity, Count;
    }

    public struct Talent : IComponentData
    {
        public TalentSlotId Slot;
        public int Experience, Level, AssignedTurns, WageTurn, LastBenefitTurn;
        public byte Recruited, Paid;
    }

    // Role: 0 monarch, 1 consort, 2 dynasty descendant, 3 retired, 4 social contact.
    // A talent and their royal/social identity live on the SAME persistent entity.
    public struct Royal : IComponentData
    {
        public byte Role, Alive, Retired, FateUsed, Evidence, TaskClaimed, EverMonarch;
        public PersonGender Gender;
        public int MarriageRequestTurn, MarriageCooldownUntil;
        public ulong RequestedSpouse, MarriageRequestMonarch;
        public int Age, Generation, ReignSince, FateUntil, LastGiftTurn, Affection, VisitUntil;
        public ulong Parent, SecondParent, Spouse;
        public float Influence, Growth, Ambition, Grievance;
        public FixedString128Bytes InfluenceSource;
    }

    [InternalBufferCapacity(0)]
    public struct TraitEntry : IBufferElementData
    {
        public RoyalTraitId Definition;
        public byte Revealed, Active;
    }

    public struct InitialRoyal : IBufferElementData
    {
        public FixedString128Bytes Name;
        public int Age;
        public byte Role;
        public PersonGender Gender;
        public FixedList128Bytes<RoyalTraitId> Traits;
    }

    public struct GridData : IComponentData
    {
        public BlobAssetReference<GridBlob> Value;
        public float3 Origin;
        public float CellSize;
        public int Revision;
    }

    public struct GridBlob
    {
        public int2 Min, Size;
        public BlobArray<GridCell> Cells;
        public float ElevationStep;
        public BlobArray<NavigationSurface> NavigationSurfaces;
        public BlobArray<AuthoredConnection> Connections;
    }

    public struct GridCell
    {
        public byte Exists, Buildable, Traversable, BlocksProjectile, EdgeZone;
        public int Elevation, Surface;
        public float Height;
        public ulong Terrain;
    }

    [InternalBufferCapacity(0)]
    public struct Occupancy : IBufferElementData
    {
        public ulong Owner;
        public float MovementCost;
    }

    public struct InitialBuilding : IBufferElementData
    {
        public BuildingId Definition;
        public int Level, Rotation;
        public int2 Cell;
        public FixedString128Bytes Name;
    }
}
