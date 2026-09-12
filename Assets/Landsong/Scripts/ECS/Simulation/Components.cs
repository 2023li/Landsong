using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum Phase : byte { Day, Settlement, Deployment, Night, Retreat, Celebration, Report, GameOver, Ended }
    public enum PersonGender : byte {
        [LabelText("未指定")] Unspecified,
        [LabelText("男性")] Male,
        [LabelText("女性")] Female
    }
    public enum NightKind : byte {
        [LabelText("平安夜")] Peaceful,
        [LabelText("入侵之夜")] Invasion,
        [LabelText("首领之夜")] Boss
    }
    public enum LifeStage : byte {
        [LabelText("施工中")] Construction,
        [LabelText("运营中")] Operational,
        [LabelText("荒废")] Ruined,
        [LabelText("修复中")] Repairing
    }
    public enum ContentKind : byte {
        [LabelText("物品")] Item,
        [LabelText("物品组")] ItemGroup,
        [LabelText("库存槽类型")] SlotType,
        [LabelText("建筑")] Building,
        [LabelText("士兵")] Soldier,
        [LabelText("敌人")] Enemy,
        [LabelText("英雄")] Hero,
        [LabelText("科技")] Technology,
        [LabelText("任务")] Quest,
        [LabelText("增益")] Buff,
        [LabelText("政策")] Policy,
        [LabelText("远征")] Expedition,
        [LabelText("人才")] Talent,
        [LabelText("人才职位")] TalentSlot,
        [LabelText("王室特性")] RoyalTrait,
        [LabelText("功能许可")] Feature,
        [LabelText("作物")] Crop,
        [LabelText("弹体")] Projectile,
        [LabelText("平安夜访客")] Opportunity,
        [LabelText("掉落物")] Loot
    }
    public enum RuleKind : byte
    {
        [LabelText("放置材料")] PlacementCost,
        [LabelText("施工材料")] ConstructionCost,
        [LabelText("施工产物")] ConstructionOutput,
        [LabelText("升级材料")] UpgradeCost,
        [LabelText("修复材料")] RepairCost,
        [LabelText("岗位配置")] Workforce,
        [LabelText("基础生产周期")] Production,
        [LabelText("工人产出档位")] ProductionTier,
        [LabelText("生产投入")] Input,
        [LabelText("维护材料")] Maintenance,
        [LabelText("库存容量")] Warehouse,
        [LabelText("基础人口")] Population,
        [LabelText("住宅")] Residence,
        [LabelText("食谱")] Food,
        [LabelText("环境需求")] Environment,
        [LabelText("范围效果")] SpatialEffect,
        [LabelText("建筑经验")] Experience,
        [LabelText("随机产出")] RareOutput,
        [LabelText("市场收入")] Market,
        [LabelText("采集")] Harvest,
        [LabelText("可种作物")] Crop,
        [LabelText("驻军容量")] Garrison,
        [LabelText("警铃")] Bell,
        [LabelText("情报")] Intelligence,
        [LabelText("神殿供奉")] Sanctum,
        [LabelText("任务容量")] QuestCapacity,
        [LabelText("邀约来源")] QuestSource,
        [LabelText("远征所")] ExpeditionSite,
        [LabelText("前置条件")] Prerequisite,
        [LabelText("物品奖励")] RewardItem,
        [LabelText("蓝图奖励")] RewardBlueprint,
        [LabelText("增益奖励")] RewardBuff,
        [LabelText("功能许可奖励")] RewardFeature,
        [LabelText("建筑要求")] RequireBuilding,
        [LabelText("作物要求")] RequireCrop,
        [LabelText("持有物品要求")] RequireItem,
        [LabelText("提交物品")] SubmitItem,
        [LabelText("移动镜头要求")] RequireCameraMove,
        [LabelText("缩放镜头要求")] RequireCameraZoom,
        [LabelText("科技要求")] RequireTechnology,
        [LabelText("回合要求")] RequireTurn,
        [LabelText("失败物品惩罚")] FailureItem,
        [LabelText("损耗倍率")] LossModifier,
        [LabelText("生产倍率加成")] ProductionBonus,
        [LabelText("攻击加成")] AttackBonus,
        [LabelText("生命加成")] HealthBonus,
        [LabelText("民意")] PublicOpinion,
        [LabelText("工资")] Wage,
        [LabelText("人才效果")] TalentEffect,
        [LabelText("人物特性")] Trait,
        [LabelText("物品分组")] ItemGroup,
        [LabelText("库存损耗")] SlotLoss,
        [LabelText("必需地形")] RequiredTerrain,
        [LabelText("任选地形")] AnyTerrain,
        [LabelText("研究费用")] ResearchCost,
        [LabelText("招募费用")] RecruitCost,
        [LabelText("唤醒费用")] WakeCost,
        [LabelText("远征补给")] Supply,
        [LabelText("王室效果")] RoyalEffect,
        [LabelText("科研产出")] ResearchOutput,
        [LabelText("附近居民吸引力")] Attraction,
        [LabelText("税收")] Tax,
        [LabelText("资源提供点")] Provider,
        [LabelText("仓储运行条件")] StorageCondition,
        [LabelText("刷新邀约费用")] QuestRecruitCost,
        [LabelText("人才刷新")] TalentRefresh,
        [LabelText("固定生产加成")] FlatProductionBonus,
        [LabelText("加工周期档位")] ProcessingTier,
        [LabelText("升级工人要求")] UpgradeWorkers,
        [LabelText("升级居民要求")] UpgradePopulation,
        [LabelText("升级维护要求")] UpgradeMaintained,
        [LabelText("库存收纳限制")] SlotAccept,
        [LabelText("显示前置条件")] VisiblePrerequisite,
        [LabelText("士兵攻击加成")] SoldierAttackBonus,
        [LabelText("士兵敏捷加成")] SoldierSpeedBonus,
        [LabelText("行动力加成")] ActionPowerBonus,
        [LabelText("作物收获加成")] CropHarvestBonus,
        [LabelText("阴谋风险")] PlotRisk,
        [LabelText("自然死亡风险")] NaturalDeathRisk,
        [LabelText("基因冲突")] GeneConflict,
        [LabelText("基因依赖")] GeneRequired,
        [LabelText("人物委托")] SocialTask,
        [LabelText("射程加成")] RangeBonus,
        [LabelText("攻速加成")] AttackSpeedBonus,
        [LabelText("移速加成")] SpeedBonus,
        [LabelText("护甲加成")] ArmorBonus,
        [LabelText("减伤加成")] DamageReductionBonus,
        [LabelText("穿甲加成")] PenetrationBonus,
        [LabelText("弹速加成")] ProjectileSpeedBonus,
        [LabelText("爆炸半径加成")] BlastRadiusBonus,
        [LabelText("特殊掉落")] SpecialDrop,
        [LabelText("开局驻军")] InitialGarrison
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
        RenameSoldier, DismissSoldier, FillGarrison, RecallGarrison, IntelligenceMode, ReadIntelligence, ResolveMarriage, PrepareMarriage, ArrangeMarriage, RefusePersonRequest, WorkforceBudget, CustomizePortrait, SetSoldierAttention,
        RecruitWorkerAtQuotedCost
    }
    public enum ResultCode : byte { Success, WrongPhase, InvalidTarget, InvalidContent, InsufficientResources, InsufficientPopulation, NoCapacity, Unavailable, InvalidPlacement, Busy, MissingResearch, QuestOverflow, ConfirmationRequired, PreparationFailed }
    public enum OrderKind : byte { Automatic, Move, Focus, Rally, Recall, Capture }
    public enum QuestStatus : byte { Offered, Active, Completed, Claimed }
    public enum ExpeditionStatus : byte { Travelling, Success, Failure }
    public enum EventKind : byte { Message, CommandResult, Damage, Ruin, Death, Reward, Theft, Save, Load, Retry, EndDynasty, DayCheckpoint, DuskCheckpoint, InventoryLost, ResidentsLost, JobsLost, RewardOverflow, HeroExperience, SoldierExperience, HeroWakeCost, TheftPrevented, VisitorEscaped, FairyCaught, VisitorCancelled, BossKilled, BossRetreated, HeroEffectiveTime, NightClosure, HeroOfferingCost, HeroOfferingExperience }

    [System.Flags] public enum BuildingCategory {
        [LabelText("无")] None = 0,
        [LabelText("人口")] Population = 1,
        [LabelText("农业")] Agriculture = 2,
        [LabelText("工业")] Industry = 4,
        [LabelText("经济")] Economy = 8,
        [LabelText("科研")] Research = 16,
        [LabelText("民政")] Civic = 32,
        [LabelText("军事")] Military = 64,
        [LabelText("道路")] Road = 128,
        [LabelText("装饰")] Decoration = 256,
        [LabelText("奇观")] Wonder = 512
    }
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
        [LabelText("平安夜时长（秒）")] public float PeacefulSeconds;
        [LabelText("战斗夜时长（秒）")] public float BattleSeconds;
        [LabelText("出勤批次间隔（秒）")] public float DeployInterval;
        [LabelText("撤退时长（秒）")] public float RetreatSeconds;
        [LabelText("入侵概率")] public float InvasionChance;
        [LabelText("战力预算系数")] public float StrengthRatio;
        [LabelText("每次重试预算降幅")] public float RetryStep;
        [LabelText("重试预算降幅上限")] public float RetryCap;
        [LabelText("首次入侵回合")] public int FirstInvasion;
        [LabelText("首次首领回合")] public int FirstBoss;
        [LabelText("首领间隔回合")] public int BossInterval;
        [LabelText("每回合威胁预算")] public int ThreatPerTurn;
        [LabelText("金币内容索引")] public int Gold;
        [LabelText("低级情报阈值")] public int LowIntel;
        [LabelText("中级情报阈值")] public int MediumIntel;
        [LabelText("高级情报阈值")] public int HighIntel;
        [LabelText("中级情报提前时间（秒）")] public float MediumIntelLead;
        [LabelText("高级情报提前时间（秒）")] public float HighIntelLead;

    }
    [System.Serializable] public struct DynastySettings : IComponentData
    {
        [LabelText("子嗣数量上限")] public int MaxChildren;
        [LabelText("人才容量")] public int TalentCapacity;
        [LabelText("人才招募金币")] public int TalentRecruitCost;
        [LabelText("人才升级经验")] public int TalentExperience;
        [LabelText("生育概率")] public float BirthChance;
        [LabelText("遗传变异概率")] public float MutationChance;

    }
    public struct ContentCatalog : IComponentData { public BlobAssetReference<ContentBlob> Value; }
    public struct MapIdentity : IComponentData { public FixedString128Bytes Id; }
    [System.Serializable] public struct QuestGenerationSettings
    {
        [LabelText("任务强度步长")] public int StrengthStep;
        [LabelText("每点强度物品价值")] public int MarketValuePerStrength;
        [LabelText("低强度抽取权重")] public int4 Low;
        [LabelText("中强度抽取权重")] public int4 Medium;
        [LabelText("高强度抽取权重")] public int4 High;
        [LabelText("最高强度抽取权重")] public int4 Maximum;
        public static QuestGenerationSettings Default => new QuestGenerationSettings { StrengthStep = 10, MarketValuePerStrength = 100, Low = new int4(100,35,8,1), Medium = new int4(45,100,35,8), High = new int4(12,45,100,35), Maximum = new int4(4,15,55,100) };

    }
    [System.Serializable] public struct ExpeditionSettings {
        [LabelText("抚恤不足惩罚回合")] public int PenaltyTurns;
        [LabelText("每层岗位吸引力惩罚")] public float AttractionPerStack;
    }
    // Explicit authored display ranges; they do not change settlement rules or save signatures.
    public struct WorkerEfficiencyTier { public int Definition, Level, MinimumWorkers, MaximumWorkers; }
    public struct ContentBlob { public BlobArray<ContentDefinition> Definitions; public BlobArray<Rule> Rules; public BlobArray<WorkerEfficiencyTier> WorkerTiers; public QuestGenerationSettings Quests; public ExpeditionSettings Expeditions; public CourtSettings Court; public NightRules Night; public PeacefulRules Peaceful; public BlobArray<NightEventDefinition> NightEvents; public BlobArray<NightEnemyChoice> NightEnemies; public BlobArray<Rule> NightConditions; }
    [System.Serializable] public struct SoldierGrowth
    {
        [LabelText("最高等级")] public int MaxLevel;
        [LabelText("首次升级所需经验")] public int FirstLevelExperience;
        [LabelText("每级经验增量")] public int ExperienceStep;
        [LabelText("有效参战经验")] public int BattleExperience;
        [LabelText("每级生命加成")] public float HealthPerLevel;
        [LabelText("每级攻击加成")] public float DamagePerLevel;
        public static SoldierGrowth Default => new SoldierGrowth { MaxLevel = 10, FirstLevelExperience = 20, ExperienceStep = 10, BattleExperience = 10, HealthPerLevel = .05f, DamagePerLevel = .05f };
    }
    [System.Serializable] public struct HeroGrowth
    {
        [LabelText("最高等级")] public int MaxLevel;
        [LabelText("首次升级所需经验")] public int FirstLevelExperience;
        [LabelText("每级经验增量")] public int ExperienceStep;
        [LabelText("每级生命加成")] public float HealthPerLevel;
        [LabelText("每级攻击加成")] public float DamagePerLevel;
        public SoldierGrowth Progression => new SoldierGrowth { MaxLevel = MaxLevel, FirstLevelExperience = FirstLevelExperience, ExperienceStep = ExperienceStep, HealthPerLevel = HealthPerLevel, DamagePerLevel = DamagePerLevel };
        [LabelText("供奉经验")] public int OfferingExperience;
        [LabelText("有效交战持续时间（秒）")] public float ContactSeconds;
        [LabelText("每秒交战经验")] public float ExperiencePerSecond;
        [LabelText("威胁参考值")] public float ThreatReference;
        [LabelText("威胁倍率上限")] public float MaximumThreatMultiplier;
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
        public HistoryCategory Category;
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
