using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum Phase : byte { Day, Settlement, Deployment, Night, Retreat, Celebration, Report, GameOver, Ended }
    public enum PersonGender : byte { Unspecified, Male, Female }
    public enum NightKind : byte { Peaceful, Invasion, Boss }
    public enum LifeStage : byte { Construction, Operational, Ruined, Repairing }
    public enum ContentKind : byte { Item, ItemGroup, SlotType, Building, Soldier, Enemy, Hero, Technology, Quest, Buff, Policy, Expedition, Talent, TalentSlot, RoyalTrait, Feature, Crop, Projectile, Opportunity, Loot }
    public enum RuleKind : byte
    {
        PlacementCost, ConstructionCost, ConstructionOutput, UpgradeCost, RepairCost,
        Workforce, Production, ProductionTier, Input, Maintenance, Warehouse, Population, Residence,
        Food, Environment, SpatialEffect, Experience, RareOutput, Market, Harvest, Crop,
        Garrison, Bell, Intelligence, Sanctum, QuestCapacity, QuestSource, ExpeditionSite,
        Prerequisite, RewardItem, RewardBlueprint, RewardBuff, RewardFeature,
        RequireBuilding, RequireCrop, RequireItem, SubmitItem, RequireCameraMove, RequireCameraZoom,
        RequireTechnology, RequireTurn, FailureItem, LossModifier, ProductionBonus,
        AttackBonus, HealthBonus, PublicOpinion, Wage, TalentEffect, Trait, ItemGroup, SlotLoss,
        RequiredTerrain, AnyTerrain, ResearchCost, RecruitCost, WakeCost, Supply, RoyalEffect,
        ResearchOutput, Attraction, Tax, Provider, StorageCondition, QuestRecruitCost, TalentRefresh, FlatProductionBonus, ProcessingTier,
        UpgradeWorkers, UpgradePopulation, UpgradeMaintained, SlotAccept, VisiblePrerequisite,
        SoldierAttackBonus, SoldierSpeedBonus, ActionPowerBonus, CropHarvestBonus, PlotRisk, NaturalDeathRisk,
        GeneConflict, GeneRequired, SocialTask,
        RangeBonus, AttackSpeedBonus, SpeedBonus, ArmorBonus, DamageReductionBonus, PenetrationBonus, ProjectileSpeedBonus, BlastRadiusBonus, SpecialDrop
    }
    public enum CommandKind : byte
    {
        Advance, Build, Demolish, Repair, Upgrade, Rename, Workers, Subsidy, Plant,
        RecruitSoldier, AssignSoldier, UnassignSoldier, SwapSoldiers, RecruitHero, Offering, WakeHero,
        Bell, Recall, SelectHero, MoveHero, FocusHero, Research, CancelResearch, SelectPolicy,
        AcceptQuest, RejectQuest, SubmitQuest, ClaimQuest, AbandonQuest, RecruitQuest,
        StartExpedition, ClaimExpedition, AbandonExpedition, RecruitTalent, AssignTalent, DismissTalent,
        RefreshTalents, Abdicate, PickUp, StorePending, Discard, CameraMoved, CameraZoomed,
        Pause, RetryDay, RetryDusk, Save, Load, EndDynasty, Harvest, AutoHarvest,
        MoveBuilding, BuildRoad, ClearCrop, ChangeBuildingSkin, ForecastEconomy,
        MoveInventory, SortInventory, StorePendingSlot, DiscardSlot, DiscardPending, WorkforceTarget, PlanResearch, TrackQuest,
        GiftPerson, CompleteSocialTask, ProposeMarriage, DesignateHeir, ExecuteHeir, RoyalVisit, CancelPolicy, NightSpeed,
        RenameSoldier, DismissSoldier, FillGarrison, RecallGarrison, IntelligenceMode, ReadIntelligence, ResolveMarriage, PrepareMarriage, ArrangeMarriage, RefusePersonRequest, WorkforceBudget, CustomizePortrait
    }
    public enum ResultCode : byte { Success, WrongPhase, InvalidTarget, InvalidContent, InsufficientResources, InsufficientPopulation, NoCapacity, Unavailable, InvalidPlacement, Busy, MissingResearch, QuestOverflow, ConfirmationRequired, PreparationFailed }
    public enum OrderKind : byte { Automatic, Move, Focus, Rally, Recall, Capture }
    public enum QuestStatus : byte { Offered, Active, Completed, Claimed }
    public enum ExpeditionStatus : byte { Travelling, Success, Failure }
    public enum EventKind : byte { Message, CommandResult, Damage, Ruin, Death, Reward, Theft, Save, Load, Retry, EndDynasty, DayCheckpoint, DuskCheckpoint, InventoryLost, ResidentsLost, JobsLost, RewardOverflow, HeroExperience, SoldierExperience, HeroWakeCost, TheftPrevented, VisitorEscaped, FairyCaught, VisitorCancelled, BossKilled, BossRetreated, HeroEffectiveTime, NightClosure, HeroOfferingCost, HeroOfferingExperience }

    [System.Flags] public enum BuildingCategory { None = 0, Population = 1, Agriculture = 2, Industry = 4, Economy = 8, Research = 16, Civic = 32, Military = 64, Road = 128, Decoration = 256, Wonder = 512 }
    public struct BuildingPolicy
    {
        public BuildingCategory Category;
        public int MenuOrder, ProviderPriority, RepairTurns, SoldierRecruitLimit;
        public float MoveMaterialRatio, MoveExperienceRatio, RuinMovementCost;
        public byte CanMove, CanRotate;
    }

    public struct Session : IComponentData
    {
        public int Turn, Stage, BasePopulation, PublicOpinion;
        public int ExpeditionPenaltyStacks, ExpeditionPenaltyUntil;
        public Phase Phase;
        public NightKind NightKind;
        public uint RandomState, NightSeed;
        public ulong NextId;
        public int RetryCount, Threat, StartCombatStrength, LastSettledTurn, ResearchPoints, BossReturnTurn, IntelAtNight;
        public float Time, PhaseTime, NightDuration, DeploymentTime;
        public byte Paused, Initialized, BossEscaped, CheckpointPending, NightSpeed, IntelligenceMode;
        public Entity SelectedHero;
        public ulong ActiveBell;
        public FixedString128Bytes DynastyName;
    }
    [System.Serializable] public struct GameSettings : IComponentData
    {
        public float PeacefulSeconds, BattleSeconds, DeployInterval, RetreatSeconds;
        public float InvasionChance, StrengthRatio, RetryStep, RetryCap;
        public int FirstInvasion, FirstBoss, BossInterval, ThreatPerTurn, Gold;
        public int LowIntel, MediumIntel, HighIntel;
        public float MediumIntelLead, HighIntelLead;
    }
    [System.Serializable] public struct DynastySettings : IComponentData
    {
        public int MaxChildren, TalentCapacity, TalentRecruitCost, TalentExperience;
        public float BirthChance, MutationChance;
    }
    public struct ContentCatalog : IComponentData { public BlobAssetReference<ContentBlob> Value; }
    public struct MapIdentity : IComponentData { public FixedString128Bytes Id; }
    [System.Serializable] public struct QuestGenerationSettings
    {
        public int StrengthStep, MarketValuePerStrength;
        public int4 Low, Medium, High, Maximum;
        public static QuestGenerationSettings Default => new QuestGenerationSettings { StrengthStep = 10, MarketValuePerStrength = 100, Low = new int4(100,35,8,1), Medium = new int4(45,100,35,8), High = new int4(12,45,100,35), Maximum = new int4(4,15,55,100) };
    }
    [System.Serializable] public struct ExpeditionSettings { public int PenaltyTurns; public float AttractionPerStack; }
    public struct ContentBlob { public BlobArray<ContentDefinition> Definitions; public BlobArray<Rule> Rules; public QuestGenerationSettings Quests; public ExpeditionSettings Expeditions; public CourtSettings Court; public NightRules Night; public PeacefulRules Peaceful; public BlobArray<NightEventDefinition> NightEvents; public BlobArray<NightEnemyChoice> NightEnemies; public BlobArray<Rule> NightConditions; }
    [System.Serializable] public struct SoldierGrowth
    {
        public int MaxLevel, FirstLevelExperience, ExperienceStep, BattleExperience;
        public float HealthPerLevel, DamagePerLevel;
        public static SoldierGrowth Default => new SoldierGrowth { MaxLevel = 10, FirstLevelExperience = 20, ExperienceStep = 10, BattleExperience = 10, HealthPerLevel = .05f, DamagePerLevel = .05f };
    }
    [System.Serializable] public struct HeroGrowth
    {
        public int MaxLevel, FirstLevelExperience, ExperienceStep;
        public float HealthPerLevel, DamagePerLevel;
        public SoldierGrowth Progression => new SoldierGrowth { MaxLevel = MaxLevel, FirstLevelExperience = FirstLevelExperience, ExperienceStep = ExperienceStep, HealthPerLevel = HealthPerLevel, DamagePerLevel = DamagePerLevel };
        public int OfferingExperience;
        public float ContactSeconds, ExperiencePerSecond, ThreatReference, MaximumThreatMultiplier;
        public static HeroGrowth Default => new HeroGrowth { MaxLevel = 10, FirstLevelExperience = 20, ExperienceStep = 10, HealthPerLevel = .05f, DamagePerLevel = .05f, OfferingExperience = 1, ContactSeconds = 3, ExperiencePerSecond = 1, ThreatReference = 100, MaximumThreatMultiplier = 3 };
    }
    // Runtime-only contact intervals; no mid-battle saves. Rebuilt at each awakening/restore.
    public struct HeroCombat : IComponentData { public int Turn; public float EffectiveSeconds, ContactUntil, CountedUntil; }
    public struct ContentDefinition
    {
        public FixedString128Bytes Id, Name;
        public ContentKind Kind;
        public int RuleStart, RuleCount, Level, Group, Capacity, Duration, Value, Limit;
        public int2 Size;
        public float Health, Damage, Range, Interval, Speed, ProjectileSpeed, Chance, Loss;
        public int Population, Cost, Flags;
        public BuildingCategory TargetCategory;
        public int QuestIntensity;
        public float QuestWeight, ItemQuantityScale;
        public BuildingPolicy BuildingPolicy;
        public SoldierGrowth SoldierGrowth;
        public HeroGrowth HeroGrowth;
        public CombatProfile Combat;
        public OpportunityProfile Opportunity;
        public TheftProfile Theft;
        public FixedString64Bytes DefaultSkin;
    }
    // Flat immutable rules are baked once; every term has explicit content indices, never scene objects.
    public struct Rule
    {
        public RuleKind Kind;
        public int Target, Secondary, Level, Amount, B, C;
        public float Value, Extra;
        public FixedString64Bytes Key;
    }
    [InternalBufferCapacity(0)] public struct ContentPrefab : IBufferElementData { public int Definition; public Entity Prefab; }
    [InternalBufferCapacity(0)] public struct Command : IBufferElementData
    {
        public ulong RequestId, Target, Other;
        public CommandKind Kind;
        public int Definition, Amount, Argument, SourceSlot, DestinationSlot;
        public float3 Position, EndPosition;
        public FixedString128Bytes Text;
    }
    [InternalBufferCapacity(0)] public struct GameEvent : IBufferElementData
    {
        public EventKind Kind;
        public ResultCode Result;
        public ulong RequestId, Target;
        public int Definition, Amount;
        public float3 Position;
        public FixedString128Bytes Message;
    }
    public struct Identity : IComponentData { public ulong Id; public int Definition; public FixedString128Bytes Name; }
    public struct Persistent : IComponentData { }
    public struct SimulationOwner : IComponentData { public Entity Root; }
    public struct NightTransient : IComponentData { }
    public struct SimulationReady : IComponentData { }
    public struct VisualState : IComponentData { public byte Visible, Ruined, Selected, Celebrating; }
    public struct Dead : IComponentData, IEnableableComponent { }
    public struct Health : IComponentData { public float Current, Maximum; }
    public struct Building : IComponentData
    {
        public LifeStage Stage;
        public int Level, Progress, Workers, StableWorkers, Experience, Population;
        public int Growth, FoodFailures, TaxProgress, ProductionProgress, Crop, CropProgress, WorkerTarget, ProtectionUntil;
        public int PaidOfferingTurn, WokenTurn, HarvestRemaining;
        public int PaidSubsidy, PaidSubsidyTurn, SubsidyBudget;
        public int SoldierRecruitTurn, SoldiersRecruited;
        public uint CropSeed;
        public FixedString64Bytes Skin;
        public int RepairDuration, RepairCompletedTurn, DeferredResidents;
        public byte RuinPending;
        public long MarketValue, MarketLifetimeValue;
        public byte Offering, Subsidy, Maintained, CropFullCycle, AutoHarvest;
        public int2 Cell, Size;
        public int Rotation, Elevation, Surface;
    }
    public struct BuildingStats : IComponentData
    {
        public int Capacity, JobCapacity, Garrison, QuestCapacity, Intelligence;
        public int MaxPopulation, BasePopulation, HeroDefinition, RequiredWorkers, BatchSize, ActionPower;
        public float BellRadius, MovementCost;
        public byte IsCore, IsProvider;
    }
    public enum EconomyReason : byte { Construction, Repair, Maintenance, Workforce, Production, Crop, Food, Tax, Offering, Market, NaturalLoss, CapacityTransfer, Research, TalentWage, TalentBenefit, Expedition, QuestPenalty, NightDiscard }
    public struct EconomyJournalState : IComponentData { public int Turn; public byte Recording, Forecast; public ulong Source; public EconomyReason Reason; public FixedString128Bytes SourceName; }
    [InternalBufferCapacity(0)] public struct EconomyEntry : IBufferElementData
    { public int Turn, Item, Delta; public ulong Source; public EconomyReason Reason; public byte Pending; public FixedString128Bytes SourceName, Note; }
    public struct EconomyForecastState : IComponentData { public int Turn; public FixedString128Bytes Fingerprint; }
    [InternalBufferCapacity(0)] public struct EconomyForecastEntry : IBufferElementData { public EconomyEntry Value; }
    [InternalBufferCapacity(0)] public struct BuildingInvestment : IBufferElementData { public int Item, Amount; }
    [InternalBufferCapacity(0)] public struct RepairMaterial : IBufferElementData { public int Item, Amount; }
    [InternalBufferCapacity(0)] public struct FoodSelection : IBufferElementData { public int Group, Item, Amount; }
    // Slots are 1-based; 0 means unassigned. RecallState: 0 normal, 1 returning, 2 home for this night.
    public struct Soldier : IComponentData { public ulong Garrison; public int Slot, PopulationCost, PendingSince, Experience, LastExperienceTurn; public byte RecallState; }
    public struct Hero : IComponentData { public ulong Sanctum; public int CooldownUntil, Experience, LastCombatTurn; public byte Recruited, DeathPending; }
    // IO identity and recovery metadata are intentionally outside rewindable snapshots.
    public struct RunPersistence : IComponentData { public FixedString64Bytes RunId; public byte Enabled; }
    public struct RecoveryState : IComponentData { public int Turn, LossCount, KnownIntel; public uint Seed; public byte AwaitingDecision, Extinction; public ulong IntelFingerprint, IntelReadFingerprint; }
    public struct NightResultState : IComponentData { public int Turn; public byte Committed; }
    // Ephemeral consent, not a save point. Binds the displayed loss list to an unchanged day.
    public struct NightEntryReview : IComponentData { public FixedString128Bytes Fingerprint; public ulong Token; }
    [InternalBufferCapacity(0)] public struct NightEntryLoss : IBufferElementData { public ulong Soldier; public int Item, Amount; public FixedString128Bytes Name; }
    [InternalBufferCapacity(0)] public struct NightReward : IBufferElementData { public ulong Source; public int Entry, Definition, Amount; public RuleKind Kind; public byte Collected; public FixedString128Bytes SourceName; }
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
    public struct UnitOrder : IComponentData { public OrderKind Kind; public float3 Destination; public Entity Target; public ulong Source; }
    public struct Steering : IComponentData { public float3 Destination, Direction; public byte Moving; }
    public struct NavigationState : IComponentData { public int2 Goal; public int Revision; public float NextRepath; public byte Failed; }
    public struct Perception : IComponentData { public Entity Enemy, Building; public float3 EnemyPosition, BuildingPosition; public byte EnemyInRange; }
    [InternalBufferCapacity(0)] public struct Waypoint : IBufferElementData { public float3 Position; }
    public struct Projectile : IComponentData { public Entity Source, Target; public float Damage, Speed, Lifetime, Penetration, Radius, Warning; public float3 Landing; public byte Faction; public ProjectileMode Mode; public ulong SourceId, TargetId; }
    [InternalBufferCapacity(0)] public struct DamageRequest : IBufferElementData { public Entity Source, Target; public float Amount, Penetration; public byte Faction, HasPayload; }
    [InternalBufferCapacity(0)] public struct InventorySlot : IBufferElementData
    {
        public ulong Provider;
        public int Index, SlotType, Item, Count;
        public float LossRemainder;
        public byte Unavailable;
    }
    [InternalBufferCapacity(0)] public struct PendingItem : IBufferElementData { public int Item, Amount; public float LossRemainder; }
    [InternalBufferCapacity(0)] public struct Entitlement : IBufferElementData { public int Definition, Level; }
    [InternalBufferCapacity(0)] public struct ResearchEntry : IBufferElementData { public int Definition, Progress, Completions, QueueOrder; }
    [InternalBufferCapacity(0)] public struct PolicyChoice : IBufferElementData { public int Definition; }
    [InternalBufferCapacity(0)] public struct BattleReportEntry : IBufferElementData { public EventKind Kind; public ulong Id; public int Definition, Amount; public float Value; public FixedString128Bytes SourceName; }
    // At is a fraction of the battle clock, not seconds. Spawned: 0 pending, 1 spawned, 2 cancelled.
    [InternalBufferCapacity(0)] public struct NightWave : IBufferElementData { public float At, PowerScale, WarnedAt; public int Definition, Count, Direction, Region; public float3 Position; public ulong Target; public byte Spawned, Warned, SpatiallyBlocked; }
    [InternalBufferCapacity(0)] public struct SpawnRegion : IBufferElementData { public int Direction; public float3 Center, Size; }
    public struct Opportunity : IComponentData { public ulong Provider; public byte Thief; public float Expires; public float3 Exit; public Entity Responder; public int PathIndex; public uint Random; public FixedString128Bytes SourceName; }
    public struct Loot : IComponentData { public int Item, Count, Rarity; public FixedString128Bytes SourceName; }
    // An accepted continuation waiting for extra prerequisites retains its container with StartTurn/Deadline = 0.
    public struct Quest : IComponentData { public QuestStatus Status; public int StartTurn, Deadline; public ulong Source, Container; public int Slot, ContainerSlot; public byte Mainline; }
    public struct QuestTracking : IComponentData { public ulong Target; public byte Mode; } // 0 automatic continuation/value, 1 manual, 2 explicitly unpinned.
    [InternalBufferCapacity(0)] public struct QuestProgress : IBufferElementData { public int RuleIndex, Amount; public FixedString64Bytes Key; }
    [InternalBufferCapacity(0)] public struct QuestOfferSlot : IBufferElementData { public int Type, Index, NextTurn; }
    public struct Expedition : IComponentData
    {
        public ulong Site, Captain;
        public FixedString128Bytes SourceName;
        public int Crew, Departure, Arrival, SourceLevel, Casualties, SubsidyRequired, SubsidyPaid, PenaltyStacks;
        public float RewardBonus, SuccessChance;
        public ExpeditionStatus Status;
    }
    [InternalBufferCapacity(0)] public struct ExpeditionSupply : IBufferElementData { public int Item, Amount; }
    [InternalBufferCapacity(0)] public struct ExpeditionDestinationHistory : IBufferElementData { public int Definition; }
    public struct QuestCapacityReview : IComponentData { public int Turn, Capacity, Count; }
    public struct Talent : IComponentData { public int Slot, Experience, Level, AssignedTurns, WageTurn, LastBenefitTurn; public byte Recruited, Paid; }
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
    [InternalBufferCapacity(0)] public struct TraitEntry : IBufferElementData { public int Definition; public byte Revealed, Active; }
    public struct InitialRoyal : IBufferElementData { public FixedString128Bytes Name; public int Age; public byte Role; public PersonGender Gender; public FixedList128Bytes<int> Traits; }
    public struct GridData : IComponentData { public BlobAssetReference<GridBlob> Value; public float3 Origin; public float CellSize; public int Revision; }
    public struct GridBlob { public int2 Min, Size; public BlobArray<GridCell> Cells; }
    public struct GridCell { public byte Exists, Buildable, Traversable, BlocksProjectile; public int Elevation, Surface; public float Height; public ulong Terrain; }
    [InternalBufferCapacity(0)] public struct Occupancy : IBufferElementData { public ulong Owner; public float MovementCost; }
    public struct InitialBuilding : IBufferElementData { public int Definition, Level, Rotation; public int2 Cell; public FixedString128Bytes Name; }
    public struct StartingGrant : IBufferElementData { public Rule Rule; }
}
