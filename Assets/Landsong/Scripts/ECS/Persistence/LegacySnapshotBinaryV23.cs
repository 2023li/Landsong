using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    // Frozen legacy ABI, copied from the pre-refactor v23 schema. Never add fields here.
    // v22 uses the same layout; SnapshotCodec applies its documented soldier-name migration.
    public static class LegacySnapshotBinaryV23
    {
        static LegacySnapshotBinaryV23()
        {
            // These library containers are the only remaining external ABI inside frozen v23.
            // A package layout change must be handled explicitly, never misread old save bytes.
            Layout<FixedString64Bytes>(64, 2); Layout<FixedString128Bytes>(128, 2);
            Layout<FixedList64Bytes<int>>(64, 8); Layout<FixedList128Bytes<int>>(128, 8);
            Layout<FixedList512Bytes<float>>(512, 8);
        }
        static void Layout<T>(int size, int alignment) where T : unmanaged
        {
            if (UnsafeUtility.SizeOf<T>() != size || UnsafeUtility.AlignOf<T>() != alignment)
                throw new InvalidOperationException("Legacy container ABI changed; provide an explicit v23 adapter: " + typeof(T));
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_BattleHistoryEntry
        {
            public int Turn;
            public V23_BattleReportEntry Entry;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_BattleReportEntry
        {
            public byte Kind;
            public ulong Id;
            public int Definition;
            public int Amount;
            public float Value;
            public FixedString128Bytes SourceName;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Building
        {
            public byte Stage;
            public int Level;
            public int Progress;
            public int Workers;
            public int StableWorkers;
            public int Experience;
            public int Population;
            public int Growth;
            public int FoodFailures;
            public int TaxProgress;
            public int ProductionProgress;
            public int Crop;
            public int CropProgress;
            public int WorkerTarget;
            public int ProtectionUntil;
            public int PaidOfferingTurn;
            public int WokenTurn;
            public int HarvestRemaining;
            public int PaidSubsidy;
            public int PaidSubsidyTurn;
            public int SubsidyBudget;
            public int SoldierRecruitTurn;
            public int SoldiersRecruited;
            public uint CropSeed;
            public FixedString64Bytes Skin;
            public int RepairDuration;
            public int RepairCompletedTurn;
            public int DeferredResidents;
            public byte RuinPending;
            public long MarketValue;
            public long MarketLifetimeValue;
            public byte Offering;
            public byte Subsidy;
            public byte Maintained;
            public byte CropFullCycle;
            public byte AutoHarvest;
            public V23_int2 Cell;
            public V23_int2 Size;
            public int Rotation;
            public int Elevation;
            public int Surface;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_BuildingInvestment
        {
            public int Item;
            public int Amount;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_BuildingPolicy
        {
            public int Category;
            public int MenuOrder;
            public int ProviderPriority;
            public int RepairTurns;
            public int SoldierRecruitLimit;
            public float MoveMaterialRatio;
            public float MoveExperienceRatio;
            public float RuinMovementCost;
            public byte CanMove;
            public byte CanRotate;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Color32
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_CombatProfile
        {
            public float DetectionRadius;
            public float ChaseRadius;
            public float ChaseSeconds;
            public float BodyRadius;
            public float Armor;
            public float Reduction;
            public float Penetration;
            public float BlastRadius;
            public float WarningSeconds;
            public float ProjectileLifetime;
            public byte ProjectileMode;
            public byte Traits;
            public bool BlocksProjectile;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_ContentDefinition
        {
            public FixedString128Bytes Id;
            public FixedString128Bytes Name;
            public byte Kind;
            public int RuleStart;
            public int RuleCount;
            public int Level;
            public int Group;
            public int Capacity;
            public int Duration;
            public int Value;
            public int Limit;
            public V23_int2 Size;
            public float Health;
            public float Damage;
            public float Range;
            public float Interval;
            public float Speed;
            public float ProjectileSpeed;
            public float Chance;
            public float Loss;
            public int Population;
            public int Cost;
            public int Flags;
            public int TargetCategory;
            public int QuestIntensity;
            public float QuestWeight;
            public float ItemQuantityScale;
            public V23_BuildingPolicy BuildingPolicy;
            public V23_SoldierGrowth SoldierGrowth;
            public V23_HeroGrowth HeroGrowth;
            public V23_CombatProfile Combat;
            public V23_OpportunityProfile Opportunity;
            public V23_TheftProfile Theft;
            public FixedString64Bytes DefaultSkin;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_CourtLogEntry
        {
            public int Turn;
            public ulong Person;
            public FixedString128Bytes Message;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_CourtSettings
        {
            public int MarriageAge;
            public int CaptainAge;
            public int GiftCost;
            public int GiftAffection;
            public int RecruitAffection;
            public int MarriageAffection;
            public int StableDesignationTurns;
            public int MinimumReign;
            public int TemporaryTurns;
            public int ElectionTurns;
            public int DisorderTurns;
            public int VisitInterval;
            public int VisitDuration;
            public int VisitCost;
            public int MarriageRequestCooldown;
            public int ExpeditionRequestCooldown;
            public float ExpeditionRequestChance;
            public float MarriageRequestChance;
            public float MarriageRefusalGrievanceChance;
            public float MarriageRefusalGrievance;
            public int InitialOpinion;
            public int OpinionRecovery;
            public int DisorderOpinionCost;
            public float PrinceGrowth;
            public float StrongInfluence;
            public float UsurpGap;
            public float UsurpChance;
            public float RegicideGap;
            public float RegicideChance;
            public float PrinceRisk;
            public float StableProduction;
            public float StableAttack;
            public float WeakProduction;
            public float WeakAttack;
            public float ElectionProduction;
            public float ElectionAttack;
            public float UsurpProduction;
            public float UsurpAttack;
            public float RegicideProduction;
            public float RegicideAttack;
            public float DisorderPerStack;
            public float DisorderCap;
            public float DeathYoung;
            public float DeathAdult;
            public float DeathMature;
            public float DeathOld;
            public float DeathAncient;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_CourtState
        {
            public ulong Crown;
            public ulong LegacyFounder;
            public int CrownSince;
            public int LastSettledTurn;
            public int TemporaryUntil;
            public int DisorderUntil;
            public int LegacyGeneration;
            public int VisitOfferTurn;
            public float TemporaryProduction;
            public float TemporaryAttack;
            public float LegacyProduction;
            public float LegacyAttack;
            public float Disorder;
            public byte Extinction;
            public byte LegacySeverity;
            public byte VisitResolved;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_DynastySettings
        {
            public int MaxChildren;
            public int TalentCapacity;
            public int TalentRecruitCost;
            public int TalentExperience;
            public float BirthChance;
            public float MutationChance;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_EconomyEntry
        {
            public int Turn;
            public int Item;
            public int Delta;
            public ulong Source;
            public byte Reason;
            public byte Pending;
            public FixedString128Bytes SourceName;
            public FixedString128Bytes Note;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Entitlement
        {
            public int Definition;
            public int Level;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Entity
        {
            public int Index;
            public int Version;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Expedition
        {
            public ulong Site;
            public ulong Captain;
            public FixedString128Bytes SourceName;
            public int Crew;
            public int Departure;
            public int Arrival;
            public int SourceLevel;
            public int Casualties;
            public int SubsidyRequired;
            public int SubsidyPaid;
            public int PenaltyStacks;
            public float RewardBonus;
            public float SuccessChance;
            public byte Status;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_ExpeditionDestinationHistory
        {
            public int Definition;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_ExpeditionSettings
        {
            public int PenaltyTurns;
            public float AttractionPerStack;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_ExpeditionSupply
        {
            public int Item;
            public int Amount;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_FoodSelection
        {
            public int Group;
            public int Item;
            public int Amount;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_GameSettings
        {
            public float PeacefulSeconds;
            public float BattleSeconds;
            public float DeployInterval;
            public float RetreatSeconds;
            public float InvasionChance;
            public float StrengthRatio;
            public float RetryStep;
            public float RetryCap;
            public int FirstInvasion;
            public int FirstBoss;
            public int BossInterval;
            public int ThreatPerTurn;
            public int Gold;
            public int LowIntel;
            public int MediumIntel;
            public int HighIntel;
            public float MediumIntelLead;
            public float HighIntelLead;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_GridCell
        {
            public byte Exists;
            public byte Buildable;
            public byte Traversable;
            public byte BlocksProjectile;
            public int Elevation;
            public int Surface;
            public float Height;
            public ulong Terrain;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Health
        {
            public float Current;
            public float Maximum;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Hero
        {
            public ulong Sanctum;
            public int CooldownUntil;
            public int Experience;
            public int LastCombatTurn;
            public byte Recruited;
            public byte DeathPending;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_HeroGrowth
        {
            public int MaxLevel;
            public int FirstLevelExperience;
            public int ExperienceStep;
            public float HealthPerLevel;
            public float DamagePerLevel;
            public int OfferingExperience;
            public float ContactSeconds;
            public float ExperiencePerSecond;
            public float ThreatReference;
            public float MaximumThreatMultiplier;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_HistoryEntry
        {
            public int Turn;
            public int Item;
            public int Delta;
            public int Count;
            public ulong Source;
            public byte Pending;
            public byte HasPosition;
            public byte Transfer;
            public byte Category;
            public V23_float3 Position;
            public FixedString128Bytes SourceName;
            public FixedString128Bytes Text;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Identity
        {
            public ulong Id;
            public int Definition;
            public FixedString128Bytes Name;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_InitialBuilding
        {
            public int Definition;
            public int Level;
            public int Rotation;
            public V23_int2 Cell;
            public FixedString128Bytes Name;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_InitialRoyal
        {
            public FixedString128Bytes Name;
            public int Age;
            public byte Role;
            public byte Gender;
            public FixedList128Bytes<int> Traits;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_InventorySlot
        {
            public ulong Provider;
            public int Index;
            public int SlotType;
            public int Item;
            public int Count;
            public float LossRemainder;
            public byte Unavailable;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_LocalTransform
        {
            public V23_float3 Position;
            public float Scale;
            public V23_quaternion Rotation;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_NightEnemyChoice
        {
            public int Definition;
            public float Weight;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_NightEventDefinition
        {
            public FixedString64Bytes Id;
            public FixedString64Bytes FollowUp;
            public byte Kind;
            public int Priority;
            public int MinTurn;
            public int MaxTurn;
            public int Interval;
            public int Cooldown;
            public int WaveCount;
            public int ReturnDelay;
            public int PoolStart;
            public int PoolCount;
            public int ConditionStart;
            public int ConditionCount;
            public float Weight;
            public float BudgetScale;
            public float Duration;
            public byte Once;
            public byte ReturnOnly;
            public byte Forced;
            public FixedList512Bytes<float> WaveTimes;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_NightEventHistory
        {
            public FixedString64Bytes Event;
            public int LastTurn;
            public int Count;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_NightPlanState
        {
            public FixedString64Bytes Event;
            public int Turn;
            public int BaseThreat;
            public int PreparedTurn;
            public int BossDefinition;
            public float CombatElapsed;
            public float FirstActionAt;
            public byte ClockStarted;
            public byte Committed;
            public byte AnySpawned;
            public byte BossKilled;
            public byte BossEscaped;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_NightPreparation
        {
            public int Definition;
            public float Health;
            public float Damage;
            public float Speed;
            public float Range;
            public float Interval;
            public float ProjectileSpeed;
            public V23_CombatProfile Combat;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_NightRules
        {
            public float EntryLeadSeconds;
            public float WarningSeconds;
            public float ProtectionSeconds;
            public float SpawnSafety;
            public float BorderBuffer;
            public float HeroWeight;
            public float FacilityWeight;
            public float TargetRadius;
            public float ThreatFloor;
            public float ThreatPerStrengthCap;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_NightWave
        {
            public float At;
            public float PowerScale;
            public float WarnedAt;
            public int Definition;
            public int Count;
            public int Direction;
            public int Region;
            public V23_float3 Position;
            public ulong Target;
            public byte Spawned;
            public byte Warned;
            public byte SpatiallyBlocked;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_OpportunityProfile
        {
            public byte Kind;
            public int Weight;
            public int MaximumPerNight;
            public float StartFraction;
            public float EndFraction;
            public float Speed;
            public float MinimumResponse;
            public float CaptureRadius;
            public float ResponseRadius;
            public float RouteLength;
            public bool Soldiers;
            public bool Heroes;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_PeacefulRules
        {
            public int MaximumPerNight;
            public int MaximumConcurrent;
            public int TheftValueBudget;
            public float FirstOpportunity;
            public float Interval;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_PendingItem
        {
            public int Item;
            public int Amount;
            public float LossRemainder;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_PersonRequestEntry
        {
            public byte Kind;
            public byte Status;
            public int CreatedTurn;
            public int ResolvedTurn;
            public ulong Journey;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_PolicyChoice
        {
            public int Definition;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_PortraitDNA
        {
            public FixedList128Bytes<int> Parts;
            public FixedList64Bytes<int> SkinDetails;
            public V23_Color32 Skin;
            public V23_Color32 Hair;
            public V23_Color32 Eyes;
            public uint Seed;
            public byte Customized;
            public byte InvitationAnnounced;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_PortraitSettings
        {
            public int YouthAge;
            public int GreyAge;
            public int ElderAge;
            public int SoldierRecruitMinAge;
            public int SoldierRecruitMaxAge;
            public int SoldierLifeMin;
            public int SoldierLifeMax;
            public float ColorMutation;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Quest
        {
            public byte Status;
            public int StartTurn;
            public int Deadline;
            public ulong Source;
            public ulong Container;
            public int Slot;
            public int ContainerSlot;
            public byte Mainline;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_QuestGenerationSettings
        {
            public int StrengthStep;
            public int MarketValuePerStrength;
            public V23_int4 Low;
            public V23_int4 Medium;
            public V23_int4 High;
            public V23_int4 Maximum;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_QuestOfferSlot
        {
            public int Type;
            public int Index;
            public int NextTurn;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_QuestProgress
        {
            public int RuleIndex;
            public int Amount;
            public FixedString64Bytes Key;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_QuestTracking
        {
            public ulong Target;
            public byte Mode;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_RepairMaterial
        {
            public int Item;
            public int Amount;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_ResearchEntry
        {
            public int Definition;
            public int Progress;
            public int Completions;
            public int QueueOrder;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Royal
        {
            public byte Role;
            public byte Alive;
            public byte Retired;
            public byte FateUsed;
            public byte Evidence;
            public byte TaskClaimed;
            public byte EverMonarch;
            public byte Gender;
            public int MarriageRequestTurn;
            public int MarriageCooldownUntil;
            public ulong RequestedSpouse;
            public ulong MarriageRequestMonarch;
            public int Age;
            public int Generation;
            public int ReignSince;
            public int FateUntil;
            public int LastGiftTurn;
            public int Affection;
            public int VisitUntil;
            public ulong Parent;
            public ulong SecondParent;
            public ulong Spouse;
            public float Influence;
            public float Growth;
            public float Ambition;
            public float Grievance;
            public FixedString128Bytes InfluenceSource;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Rule
        {
            public byte Kind;
            public int Target;
            public int Secondary;
            public int Level;
            public int Amount;
            public int B;
            public int C;
            public float Value;
            public float Extra;
            public FixedString64Bytes Key;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Session
        {
            public int Turn;
            public int Stage;
            public int BasePopulation;
            public int PublicOpinion;
            public int ExpeditionPenaltyStacks;
            public int ExpeditionPenaltyUntil;
            public byte Phase;
            public byte NightKind;
            public uint RandomState;
            public uint NightSeed;
            public ulong NextId;
            public int RetryCount;
            public int Threat;
            public int StartCombatStrength;
            public int LastSettledTurn;
            public int ResearchPoints;
            public int BossReturnTurn;
            public int IntelAtNight;
            public float Time;
            public float PhaseTime;
            public float NightDuration;
            public float DeploymentTime;
            public byte Paused;
            public byte Initialized;
            public byte BossEscaped;
            public byte CheckpointPending;
            public byte NightSpeed;
            public byte IntelligenceMode;
            public V23_Entity SelectedHero;
            public ulong ActiveBell;
            public FixedString128Bytes DynastyName;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Soldier
        {
            public ulong Garrison;
            public int Slot;
            public int PopulationCost;
            public int PendingSince;
            public int Experience;
            public int LastExperienceTurn;
            public byte RecallState;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_SoldierGrowth
        {
            public int MaxLevel;
            public int FirstLevelExperience;
            public int ExperienceStep;
            public int BattleExperience;
            public float HealthPerLevel;
            public float DamagePerLevel;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_SoldierPerson
        {
            public int Age;
            public int Lifespan;
            public int LastAgeTurn;
            public int Incarnation;
            public byte Gender;
            public byte SpecialAttention;
            public byte DeathNotified;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_SpawnRegion
        {
            public int Direction;
            public V23_float3 Center;
            public V23_float3 Size;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_Talent
        {
            public int Slot;
            public int Experience;
            public int Level;
            public int AssignedTurns;
            public int WageTurn;
            public int LastBenefitTurn;
            public byte Recruited;
            public byte Paid;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_TheftProfile
        {
            public byte Protection;
            public int Weight;
            public int Maximum;
            public int UnitValue;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_TraitEntry
        {
            public int Definition;
            public byte Revealed;
            public byte Active;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_UnresolvedBoss
        {
            public FixedString64Bytes Event;
            public int Definition;
            public int DueTurn;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_float3
        {
            public float x;
            public float y;
            public float z;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_float4
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_int2
        {
            public int x;
            public int y;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_int4
        {
            public int x;
            public int y;
            public int z;
            public int w;
        }
        [StructLayout(LayoutKind.Sequential)] struct V23_quaternion
        {
            public V23_float4 value;
        }
        static V23_BattleHistoryEntry Freeze(BattleHistoryEntry value) => new V23_BattleHistoryEntry
        {
            Turn = value.Turn,
            Entry = Freeze(value.Entry),
        };
        static BattleHistoryEntry Thaw(V23_BattleHistoryEntry value) => new BattleHistoryEntry
        {
            Turn = value.Turn,
            Entry = Thaw(value.Entry),
        };
        static V23_BattleReportEntry Freeze(BattleReportEntry value) => new V23_BattleReportEntry
        {
            Kind = (byte)value.Kind,
            Id = value.Id,
            Definition = value.Definition,
            Amount = value.Amount,
            Value = value.Value,
            SourceName = value.SourceName,
        };
        static BattleReportEntry Thaw(V23_BattleReportEntry value) => new BattleReportEntry
        {
            Kind = (EventKind)value.Kind,
            Id = value.Id,
            Definition = value.Definition,
            Amount = value.Amount,
            Value = value.Value,
            SourceName = value.SourceName,
        };
        static V23_Building Freeze(Building value) => new V23_Building
        {
            Stage = (byte)value.Stage,
            Level = value.Level,
            Progress = value.Progress,
            Workers = value.Workers,
            StableWorkers = value.StableWorkers,
            Experience = value.Experience,
            Population = value.Population,
            Growth = value.Growth,
            FoodFailures = value.FoodFailures,
            TaxProgress = value.TaxProgress,
            ProductionProgress = value.ProductionProgress,
            Crop = value.Crop,
            CropProgress = value.CropProgress,
            WorkerTarget = value.WorkerTarget,
            ProtectionUntil = value.ProtectionUntil,
            PaidOfferingTurn = value.PaidOfferingTurn,
            WokenTurn = value.WokenTurn,
            HarvestRemaining = value.HarvestRemaining,
            PaidSubsidy = value.PaidSubsidy,
            PaidSubsidyTurn = value.PaidSubsidyTurn,
            SubsidyBudget = value.SubsidyBudget,
            SoldierRecruitTurn = value.SoldierRecruitTurn,
            SoldiersRecruited = value.SoldiersRecruited,
            CropSeed = value.CropSeed,
            Skin = value.Skin,
            RepairDuration = value.RepairDuration,
            RepairCompletedTurn = value.RepairCompletedTurn,
            DeferredResidents = value.DeferredResidents,
            RuinPending = value.RuinPending,
            MarketValue = value.MarketValue,
            MarketLifetimeValue = value.MarketLifetimeValue,
            Offering = value.Offering,
            Subsidy = value.Subsidy,
            Maintained = value.Maintained,
            CropFullCycle = value.CropFullCycle,
            AutoHarvest = value.AutoHarvest,
            Cell = Freeze(value.Cell),
            Size = Freeze(value.Size),
            Rotation = value.Rotation,
            Elevation = value.Elevation,
            Surface = value.Surface,
        };
        static Building Thaw(V23_Building value) => new Building
        {
            Stage = (LifeStage)value.Stage,
            Level = value.Level,
            Progress = value.Progress,
            Workers = value.Workers,
            StableWorkers = value.StableWorkers,
            Experience = value.Experience,
            Population = value.Population,
            Growth = value.Growth,
            FoodFailures = value.FoodFailures,
            TaxProgress = value.TaxProgress,
            ProductionProgress = value.ProductionProgress,
            Crop = value.Crop,
            CropProgress = value.CropProgress,
            WorkerTarget = value.WorkerTarget,
            ProtectionUntil = value.ProtectionUntil,
            PaidOfferingTurn = value.PaidOfferingTurn,
            WokenTurn = value.WokenTurn,
            HarvestRemaining = value.HarvestRemaining,
            PaidSubsidy = value.PaidSubsidy,
            PaidSubsidyTurn = value.PaidSubsidyTurn,
            SubsidyBudget = value.SubsidyBudget,
            SoldierRecruitTurn = value.SoldierRecruitTurn,
            SoldiersRecruited = value.SoldiersRecruited,
            CropSeed = value.CropSeed,
            Skin = value.Skin,
            RepairDuration = value.RepairDuration,
            RepairCompletedTurn = value.RepairCompletedTurn,
            DeferredResidents = value.DeferredResidents,
            RuinPending = value.RuinPending,
            MarketValue = value.MarketValue,
            MarketLifetimeValue = value.MarketLifetimeValue,
            Offering = value.Offering,
            Subsidy = value.Subsidy,
            Maintained = value.Maintained,
            CropFullCycle = value.CropFullCycle,
            AutoHarvest = value.AutoHarvest,
            Cell = Thaw(value.Cell),
            Size = Thaw(value.Size),
            Rotation = value.Rotation,
            Elevation = value.Elevation,
            Surface = value.Surface,
        };
        static V23_BuildingInvestment Freeze(BuildingInvestment value) => new V23_BuildingInvestment
        {
            Item = value.Item,
            Amount = value.Amount,
        };
        static BuildingInvestment Thaw(V23_BuildingInvestment value) => new BuildingInvestment
        {
            Item = value.Item,
            Amount = value.Amount,
        };
        static V23_BuildingPolicy Freeze(BuildingPolicy value) => new V23_BuildingPolicy
        {
            Category = (int)value.Category,
            MenuOrder = value.MenuOrder,
            ProviderPriority = value.ProviderPriority,
            RepairTurns = value.RepairTurns,
            SoldierRecruitLimit = value.SoldierRecruitLimit,
            MoveMaterialRatio = value.MoveMaterialRatio,
            MoveExperienceRatio = value.MoveExperienceRatio,
            RuinMovementCost = value.RuinMovementCost,
            CanMove = value.CanMove,
            CanRotate = value.CanRotate,
        };
        static BuildingPolicy Thaw(V23_BuildingPolicy value) => new BuildingPolicy
        {
            Category = (BuildingCategory)value.Category,
            MenuOrder = value.MenuOrder,
            ProviderPriority = value.ProviderPriority,
            RepairTurns = value.RepairTurns,
            SoldierRecruitLimit = value.SoldierRecruitLimit,
            MoveMaterialRatio = value.MoveMaterialRatio,
            MoveExperienceRatio = value.MoveExperienceRatio,
            RuinMovementCost = value.RuinMovementCost,
            CanMove = value.CanMove,
            CanRotate = value.CanRotate,
        };
        static V23_Color32 Freeze(Color32 value) => new V23_Color32
        {
            r = value.r,
            g = value.g,
            b = value.b,
            a = value.a,
        };
        static Color32 Thaw(V23_Color32 value) => new Color32
        {
            r = value.r,
            g = value.g,
            b = value.b,
            a = value.a,
        };
        static V23_CombatProfile Freeze(CombatProfile value) => new V23_CombatProfile
        {
            DetectionRadius = value.DetectionRadius,
            ChaseRadius = value.ChaseRadius,
            ChaseSeconds = value.ChaseSeconds,
            BodyRadius = value.BodyRadius,
            Armor = value.Armor,
            Reduction = value.Reduction,
            Penetration = value.Penetration,
            BlastRadius = value.BlastRadius,
            WarningSeconds = value.WarningSeconds,
            ProjectileLifetime = value.ProjectileLifetime,
            ProjectileMode = (byte)value.ProjectileMode,
            Traits = (byte)value.Traits,
            BlocksProjectile = value.BlocksProjectile,
        };
        static CombatProfile Thaw(V23_CombatProfile value) => new CombatProfile
        {
            DetectionRadius = value.DetectionRadius,
            ChaseRadius = value.ChaseRadius,
            ChaseSeconds = value.ChaseSeconds,
            BodyRadius = value.BodyRadius,
            Armor = value.Armor,
            Reduction = value.Reduction,
            Penetration = value.Penetration,
            BlastRadius = value.BlastRadius,
            WarningSeconds = value.WarningSeconds,
            ProjectileLifetime = value.ProjectileLifetime,
            ProjectileMode = (ProjectileMode)value.ProjectileMode,
            Traits = (TacticalTraits)value.Traits,
            BlocksProjectile = value.BlocksProjectile,
        };
        static V23_ContentDefinition Freeze(ContentDefinition value) => new V23_ContentDefinition
        {
            Id = value.Id,
            Name = value.Name,
            Kind = (byte)value.Kind,
            RuleStart = value.RuleStart,
            RuleCount = value.RuleCount,
            Level = value.Level,
            Group = value.Group,
            Capacity = value.Capacity,
            Duration = value.Duration,
            Value = value.Value,
            Limit = value.Limit,
            Size = Freeze(value.Size),
            Health = value.Health,
            Damage = value.Damage,
            Range = value.Range,
            Interval = value.Interval,
            Speed = value.Speed,
            ProjectileSpeed = value.ProjectileSpeed,
            Chance = value.Chance,
            Loss = value.Loss,
            Population = value.Population,
            Cost = value.Cost,
            Flags = value.Flags,
            TargetCategory = (int)value.TargetCategory,
            QuestIntensity = value.QuestIntensity,
            QuestWeight = value.QuestWeight,
            ItemQuantityScale = value.ItemQuantityScale,
            BuildingPolicy = Freeze(value.BuildingPolicy),
            SoldierGrowth = Freeze(value.SoldierGrowth),
            HeroGrowth = Freeze(value.HeroGrowth),
            Combat = Freeze(value.Combat),
            Opportunity = Freeze(value.Opportunity),
            Theft = Freeze(value.Theft),
            DefaultSkin = value.DefaultSkin,
        };
        static ContentDefinition Thaw(V23_ContentDefinition value) => new ContentDefinition
        {
            Id = value.Id,
            Name = value.Name,
            Kind = (ContentKind)value.Kind,
            RuleStart = value.RuleStart,
            RuleCount = value.RuleCount,
            Level = value.Level,
            Group = value.Group,
            Capacity = value.Capacity,
            Duration = value.Duration,
            Value = value.Value,
            Limit = value.Limit,
            Size = Thaw(value.Size),
            Health = value.Health,
            Damage = value.Damage,
            Range = value.Range,
            Interval = value.Interval,
            Speed = value.Speed,
            ProjectileSpeed = value.ProjectileSpeed,
            Chance = value.Chance,
            Loss = value.Loss,
            Population = value.Population,
            Cost = value.Cost,
            Flags = value.Flags,
            TargetCategory = (BuildingCategory)value.TargetCategory,
            QuestIntensity = value.QuestIntensity,
            QuestWeight = value.QuestWeight,
            ItemQuantityScale = value.ItemQuantityScale,
            BuildingPolicy = Thaw(value.BuildingPolicy),
            SoldierGrowth = Thaw(value.SoldierGrowth),
            HeroGrowth = Thaw(value.HeroGrowth),
            Combat = Thaw(value.Combat),
            Opportunity = Thaw(value.Opportunity),
            Theft = Thaw(value.Theft),
            DefaultSkin = value.DefaultSkin,
        };
        static V23_CourtLogEntry Freeze(CourtLogEntry value) => new V23_CourtLogEntry
        {
            Turn = value.Turn,
            Person = value.Person,
            Message = value.Message,
        };
        static CourtLogEntry Thaw(V23_CourtLogEntry value) => new CourtLogEntry
        {
            Turn = value.Turn,
            Person = value.Person,
            Message = value.Message,
        };
        static V23_CourtSettings Freeze(CourtSettings value) => new V23_CourtSettings
        {
            MarriageAge = value.MarriageAge,
            CaptainAge = value.CaptainAge,
            GiftCost = value.GiftCost,
            GiftAffection = value.GiftAffection,
            RecruitAffection = value.RecruitAffection,
            MarriageAffection = value.MarriageAffection,
            StableDesignationTurns = value.StableDesignationTurns,
            MinimumReign = value.MinimumReign,
            TemporaryTurns = value.TemporaryTurns,
            ElectionTurns = value.ElectionTurns,
            DisorderTurns = value.DisorderTurns,
            VisitInterval = value.VisitInterval,
            VisitDuration = value.VisitDuration,
            VisitCost = value.VisitCost,
            MarriageRequestCooldown = value.MarriageRequestCooldown,
            ExpeditionRequestCooldown = value.ExpeditionRequestCooldown,
            ExpeditionRequestChance = value.ExpeditionRequestChance,
            MarriageRequestChance = value.MarriageRequestChance,
            MarriageRefusalGrievanceChance = value.MarriageRefusalGrievanceChance,
            MarriageRefusalGrievance = value.MarriageRefusalGrievance,
            InitialOpinion = value.InitialOpinion,
            OpinionRecovery = value.OpinionRecovery,
            DisorderOpinionCost = value.DisorderOpinionCost,
            PrinceGrowth = value.PrinceGrowth,
            StrongInfluence = value.StrongInfluence,
            UsurpGap = value.UsurpGap,
            UsurpChance = value.UsurpChance,
            RegicideGap = value.RegicideGap,
            RegicideChance = value.RegicideChance,
            PrinceRisk = value.PrinceRisk,
            StableProduction = value.StableProduction,
            StableAttack = value.StableAttack,
            WeakProduction = value.WeakProduction,
            WeakAttack = value.WeakAttack,
            ElectionProduction = value.ElectionProduction,
            ElectionAttack = value.ElectionAttack,
            UsurpProduction = value.UsurpProduction,
            UsurpAttack = value.UsurpAttack,
            RegicideProduction = value.RegicideProduction,
            RegicideAttack = value.RegicideAttack,
            DisorderPerStack = value.DisorderPerStack,
            DisorderCap = value.DisorderCap,
            DeathYoung = value.DeathYoung,
            DeathAdult = value.DeathAdult,
            DeathMature = value.DeathMature,
            DeathOld = value.DeathOld,
            DeathAncient = value.DeathAncient,
        };
        static CourtSettings Thaw(V23_CourtSettings value) => new CourtSettings
        {
            MarriageAge = value.MarriageAge,
            CaptainAge = value.CaptainAge,
            GiftCost = value.GiftCost,
            GiftAffection = value.GiftAffection,
            RecruitAffection = value.RecruitAffection,
            MarriageAffection = value.MarriageAffection,
            StableDesignationTurns = value.StableDesignationTurns,
            MinimumReign = value.MinimumReign,
            TemporaryTurns = value.TemporaryTurns,
            ElectionTurns = value.ElectionTurns,
            DisorderTurns = value.DisorderTurns,
            VisitInterval = value.VisitInterval,
            VisitDuration = value.VisitDuration,
            VisitCost = value.VisitCost,
            MarriageRequestCooldown = value.MarriageRequestCooldown,
            ExpeditionRequestCooldown = value.ExpeditionRequestCooldown,
            ExpeditionRequestChance = value.ExpeditionRequestChance,
            MarriageRequestChance = value.MarriageRequestChance,
            MarriageRefusalGrievanceChance = value.MarriageRefusalGrievanceChance,
            MarriageRefusalGrievance = value.MarriageRefusalGrievance,
            InitialOpinion = value.InitialOpinion,
            OpinionRecovery = value.OpinionRecovery,
            DisorderOpinionCost = value.DisorderOpinionCost,
            PrinceGrowth = value.PrinceGrowth,
            StrongInfluence = value.StrongInfluence,
            UsurpGap = value.UsurpGap,
            UsurpChance = value.UsurpChance,
            RegicideGap = value.RegicideGap,
            RegicideChance = value.RegicideChance,
            PrinceRisk = value.PrinceRisk,
            StableProduction = value.StableProduction,
            StableAttack = value.StableAttack,
            WeakProduction = value.WeakProduction,
            WeakAttack = value.WeakAttack,
            ElectionProduction = value.ElectionProduction,
            ElectionAttack = value.ElectionAttack,
            UsurpProduction = value.UsurpProduction,
            UsurpAttack = value.UsurpAttack,
            RegicideProduction = value.RegicideProduction,
            RegicideAttack = value.RegicideAttack,
            DisorderPerStack = value.DisorderPerStack,
            DisorderCap = value.DisorderCap,
            DeathYoung = value.DeathYoung,
            DeathAdult = value.DeathAdult,
            DeathMature = value.DeathMature,
            DeathOld = value.DeathOld,
            DeathAncient = value.DeathAncient,
        };
        static V23_CourtState Freeze(CourtState value) => new V23_CourtState
        {
            Crown = value.Crown,
            LegacyFounder = value.LegacyFounder,
            CrownSince = value.CrownSince,
            LastSettledTurn = value.LastSettledTurn,
            TemporaryUntil = value.TemporaryUntil,
            DisorderUntil = value.DisorderUntil,
            LegacyGeneration = value.LegacyGeneration,
            VisitOfferTurn = value.VisitOfferTurn,
            TemporaryProduction = value.TemporaryProduction,
            TemporaryAttack = value.TemporaryAttack,
            LegacyProduction = value.LegacyProduction,
            LegacyAttack = value.LegacyAttack,
            Disorder = value.Disorder,
            Extinction = value.Extinction,
            LegacySeverity = value.LegacySeverity,
            VisitResolved = value.VisitResolved,
        };
        static CourtState Thaw(V23_CourtState value) => new CourtState
        {
            Crown = value.Crown,
            LegacyFounder = value.LegacyFounder,
            CrownSince = value.CrownSince,
            LastSettledTurn = value.LastSettledTurn,
            TemporaryUntil = value.TemporaryUntil,
            DisorderUntil = value.DisorderUntil,
            LegacyGeneration = value.LegacyGeneration,
            VisitOfferTurn = value.VisitOfferTurn,
            TemporaryProduction = value.TemporaryProduction,
            TemporaryAttack = value.TemporaryAttack,
            LegacyProduction = value.LegacyProduction,
            LegacyAttack = value.LegacyAttack,
            Disorder = value.Disorder,
            Extinction = value.Extinction,
            LegacySeverity = value.LegacySeverity,
            VisitResolved = value.VisitResolved,
        };
        static V23_DynastySettings Freeze(DynastySettings value) => new V23_DynastySettings
        {
            MaxChildren = value.MaxChildren,
            TalentCapacity = value.TalentCapacity,
            TalentRecruitCost = value.TalentRecruitCost,
            TalentExperience = value.TalentExperience,
            BirthChance = value.BirthChance,
            MutationChance = value.MutationChance,
        };
        static DynastySettings Thaw(V23_DynastySettings value) => new DynastySettings
        {
            MaxChildren = value.MaxChildren,
            TalentCapacity = value.TalentCapacity,
            TalentRecruitCost = value.TalentRecruitCost,
            TalentExperience = value.TalentExperience,
            BirthChance = value.BirthChance,
            MutationChance = value.MutationChance,
        };
        static V23_EconomyEntry Freeze(EconomyEntry value) => new V23_EconomyEntry
        {
            Turn = value.Turn,
            Item = value.Item,
            Delta = value.Delta,
            Source = value.Source,
            Reason = (byte)value.Reason,
            Pending = value.Pending,
            SourceName = value.SourceName,
            Note = value.Note,
        };
        static EconomyEntry Thaw(V23_EconomyEntry value) => new EconomyEntry
        {
            Turn = value.Turn,
            Item = value.Item,
            Delta = value.Delta,
            Source = value.Source,
            Reason = (EconomyReason)value.Reason,
            Pending = value.Pending,
            SourceName = value.SourceName,
            Note = value.Note,
        };
        static V23_Entitlement Freeze(Entitlement value) => new V23_Entitlement
        {
            Definition = value.Definition,
            Level = value.Level,
        };
        static Entitlement Thaw(V23_Entitlement value) => new Entitlement
        {
            Definition = value.Definition,
            Level = value.Level,
        };
        static V23_Entity Freeze(Entity value) => new V23_Entity
        {
            Index = value.Index,
            Version = value.Version,
        };
        static Entity Thaw(V23_Entity value) => new Entity
        {
            Index = value.Index,
            Version = value.Version,
        };
        static V23_Expedition Freeze(Expedition value) => new V23_Expedition
        {
            Site = value.Site,
            Captain = value.Captain,
            SourceName = value.SourceName,
            Crew = value.Crew,
            Departure = value.Departure,
            Arrival = value.Arrival,
            SourceLevel = value.SourceLevel,
            Casualties = value.Casualties,
            SubsidyRequired = value.SubsidyRequired,
            SubsidyPaid = value.SubsidyPaid,
            PenaltyStacks = value.PenaltyStacks,
            RewardBonus = value.RewardBonus,
            SuccessChance = value.SuccessChance,
            Status = (byte)value.Status,
        };
        static Expedition Thaw(V23_Expedition value) => new Expedition
        {
            Site = value.Site,
            Captain = value.Captain,
            SourceName = value.SourceName,
            Crew = value.Crew,
            Departure = value.Departure,
            Arrival = value.Arrival,
            SourceLevel = value.SourceLevel,
            Casualties = value.Casualties,
            SubsidyRequired = value.SubsidyRequired,
            SubsidyPaid = value.SubsidyPaid,
            PenaltyStacks = value.PenaltyStacks,
            RewardBonus = value.RewardBonus,
            SuccessChance = value.SuccessChance,
            Status = (ExpeditionStatus)value.Status,
        };
        static V23_ExpeditionDestinationHistory Freeze(ExpeditionDestinationHistory value) => new V23_ExpeditionDestinationHistory
        {
            Definition = value.Definition,
        };
        static ExpeditionDestinationHistory Thaw(V23_ExpeditionDestinationHistory value) => new ExpeditionDestinationHistory
        {
            Definition = value.Definition,
        };
        static V23_ExpeditionSettings Freeze(ExpeditionSettings value) => new V23_ExpeditionSettings
        {
            PenaltyTurns = value.PenaltyTurns,
            AttractionPerStack = value.AttractionPerStack,
        };
        static ExpeditionSettings Thaw(V23_ExpeditionSettings value) => new ExpeditionSettings
        {
            PenaltyTurns = value.PenaltyTurns,
            AttractionPerStack = value.AttractionPerStack,
        };
        static V23_ExpeditionSupply Freeze(ExpeditionSupply value) => new V23_ExpeditionSupply
        {
            Item = value.Item,
            Amount = value.Amount,
        };
        static ExpeditionSupply Thaw(V23_ExpeditionSupply value) => new ExpeditionSupply
        {
            Item = value.Item,
            Amount = value.Amount,
        };
        static V23_FoodSelection Freeze(FoodSelection value) => new V23_FoodSelection
        {
            Group = value.Group,
            Item = value.Item,
            Amount = value.Amount,
        };
        static FoodSelection Thaw(V23_FoodSelection value) => new FoodSelection
        {
            Group = value.Group,
            Item = value.Item,
            Amount = value.Amount,
        };
        static V23_GameSettings Freeze(GameSettings value) => new V23_GameSettings
        {
            PeacefulSeconds = value.PeacefulSeconds,
            BattleSeconds = value.BattleSeconds,
            DeployInterval = value.DeployInterval,
            RetreatSeconds = value.RetreatSeconds,
            InvasionChance = value.InvasionChance,
            StrengthRatio = value.StrengthRatio,
            RetryStep = value.RetryStep,
            RetryCap = value.RetryCap,
            FirstInvasion = value.FirstInvasion,
            FirstBoss = value.FirstBoss,
            BossInterval = value.BossInterval,
            ThreatPerTurn = value.ThreatPerTurn,
            Gold = value.Gold,
            LowIntel = value.LowIntel,
            MediumIntel = value.MediumIntel,
            HighIntel = value.HighIntel,
            MediumIntelLead = value.MediumIntelLead,
            HighIntelLead = value.HighIntelLead,
        };
        static GameSettings Thaw(V23_GameSettings value) => new GameSettings
        {
            PeacefulSeconds = value.PeacefulSeconds,
            BattleSeconds = value.BattleSeconds,
            DeployInterval = value.DeployInterval,
            RetreatSeconds = value.RetreatSeconds,
            InvasionChance = value.InvasionChance,
            StrengthRatio = value.StrengthRatio,
            RetryStep = value.RetryStep,
            RetryCap = value.RetryCap,
            FirstInvasion = value.FirstInvasion,
            FirstBoss = value.FirstBoss,
            BossInterval = value.BossInterval,
            ThreatPerTurn = value.ThreatPerTurn,
            Gold = value.Gold,
            LowIntel = value.LowIntel,
            MediumIntel = value.MediumIntel,
            HighIntel = value.HighIntel,
            MediumIntelLead = value.MediumIntelLead,
            HighIntelLead = value.HighIntelLead,
        };
        static V23_GridCell Freeze(GridCell value) => new V23_GridCell
        {
            Exists = value.Exists,
            Buildable = value.Buildable,
            Traversable = value.Traversable,
            BlocksProjectile = value.BlocksProjectile,
            Elevation = value.Elevation,
            Surface = value.Surface,
            Height = value.Height,
            Terrain = value.Terrain,
        };
        static GridCell Thaw(V23_GridCell value) => new GridCell
        {
            Exists = value.Exists,
            Buildable = value.Buildable,
            Traversable = value.Traversable,
            BlocksProjectile = value.BlocksProjectile,
            Elevation = value.Elevation,
            Surface = value.Surface,
            Height = value.Height,
            Terrain = value.Terrain,
        };
        static V23_Health Freeze(Health value) => new V23_Health
        {
            Current = value.Current,
            Maximum = value.Maximum,
        };
        static Health Thaw(V23_Health value) => new Health
        {
            Current = value.Current,
            Maximum = value.Maximum,
        };
        static V23_Hero Freeze(Hero value) => new V23_Hero
        {
            Sanctum = value.Sanctum,
            CooldownUntil = value.CooldownUntil,
            Experience = value.Experience,
            LastCombatTurn = value.LastCombatTurn,
            Recruited = value.Recruited,
            DeathPending = value.DeathPending,
        };
        static Hero Thaw(V23_Hero value) => new Hero
        {
            Sanctum = value.Sanctum,
            CooldownUntil = value.CooldownUntil,
            Experience = value.Experience,
            LastCombatTurn = value.LastCombatTurn,
            Recruited = value.Recruited,
            DeathPending = value.DeathPending,
        };
        static V23_HeroGrowth Freeze(HeroGrowth value) => new V23_HeroGrowth
        {
            MaxLevel = value.MaxLevel,
            FirstLevelExperience = value.FirstLevelExperience,
            ExperienceStep = value.ExperienceStep,
            HealthPerLevel = value.HealthPerLevel,
            DamagePerLevel = value.DamagePerLevel,
            OfferingExperience = value.OfferingExperience,
            ContactSeconds = value.ContactSeconds,
            ExperiencePerSecond = value.ExperiencePerSecond,
            ThreatReference = value.ThreatReference,
            MaximumThreatMultiplier = value.MaximumThreatMultiplier,
        };
        static HeroGrowth Thaw(V23_HeroGrowth value) => new HeroGrowth
        {
            MaxLevel = value.MaxLevel,
            FirstLevelExperience = value.FirstLevelExperience,
            ExperienceStep = value.ExperienceStep,
            HealthPerLevel = value.HealthPerLevel,
            DamagePerLevel = value.DamagePerLevel,
            OfferingExperience = value.OfferingExperience,
            ContactSeconds = value.ContactSeconds,
            ExperiencePerSecond = value.ExperiencePerSecond,
            ThreatReference = value.ThreatReference,
            MaximumThreatMultiplier = value.MaximumThreatMultiplier,
        };
        static V23_HistoryEntry Freeze(HistoryEntry value) => new V23_HistoryEntry
        {
            Turn = value.Turn,
            Item = value.Item,
            Delta = value.Delta,
            Count = value.Count,
            Source = value.Source,
            Pending = value.Pending,
            HasPosition = value.HasPosition,
            Transfer = value.Transfer,
            Category = (byte)value.Category,
            Position = Freeze(value.Position),
            SourceName = value.SourceName,
            Text = value.Text,
        };
        static HistoryEntry Thaw(V23_HistoryEntry value) => new HistoryEntry
        {
            Turn = value.Turn,
            Item = value.Item,
            Delta = value.Delta,
            Count = value.Count,
            Source = value.Source,
            Pending = value.Pending,
            HasPosition = value.HasPosition,
            Transfer = value.Transfer,
            Category = (HistoryCategory)value.Category,
            Position = Thaw(value.Position),
            SourceName = value.SourceName,
            Text = value.Text,
        };
        static V23_Identity Freeze(Identity value) => new V23_Identity
        {
            Id = value.Id,
            Definition = value.Definition,
            Name = value.Name,
        };
        static Identity Thaw(V23_Identity value) => new Identity
        {
            Id = value.Id,
            Definition = value.Definition,
            Name = value.Name,
        };
        static V23_InitialBuilding Freeze(InitialBuilding value) => new V23_InitialBuilding
        {
            Definition = value.Definition,
            Level = value.Level,
            Rotation = value.Rotation,
            Cell = Freeze(value.Cell),
            Name = value.Name,
        };
        static InitialBuilding Thaw(V23_InitialBuilding value) => new InitialBuilding
        {
            Definition = value.Definition,
            Level = value.Level,
            Rotation = value.Rotation,
            Cell = Thaw(value.Cell),
            Name = value.Name,
        };
        static V23_InitialRoyal Freeze(InitialRoyal value) => new V23_InitialRoyal
        {
            Name = value.Name,
            Age = value.Age,
            Role = value.Role,
            Gender = (byte)value.Gender,
            Traits = value.Traits,
        };
        static InitialRoyal Thaw(V23_InitialRoyal value) => new InitialRoyal
        {
            Name = value.Name,
            Age = value.Age,
            Role = value.Role,
            Gender = (PersonGender)value.Gender,
            Traits = value.Traits,
        };
        static V23_InventorySlot Freeze(InventorySlot value) => new V23_InventorySlot
        {
            Provider = value.Provider,
            Index = value.Index,
            SlotType = value.SlotType,
            Item = value.Item,
            Count = value.Count,
            LossRemainder = value.LossRemainder,
            Unavailable = value.Unavailable,
        };
        static InventorySlot Thaw(V23_InventorySlot value) => new InventorySlot
        {
            Provider = value.Provider,
            Index = value.Index,
            SlotType = value.SlotType,
            Item = value.Item,
            Count = value.Count,
            LossRemainder = value.LossRemainder,
            Unavailable = value.Unavailable,
        };
        static V23_LocalTransform Freeze(LocalTransform value) => new V23_LocalTransform
        {
            Position = Freeze(value.Position),
            Scale = value.Scale,
            Rotation = Freeze(value.Rotation),
        };
        static LocalTransform Thaw(V23_LocalTransform value) => new LocalTransform
        {
            Position = Thaw(value.Position),
            Scale = value.Scale,
            Rotation = Thaw(value.Rotation),
        };
        static V23_NightEnemyChoice Freeze(NightEnemyChoice value) => new V23_NightEnemyChoice
        {
            Definition = value.Definition,
            Weight = value.Weight,
        };
        static NightEnemyChoice Thaw(V23_NightEnemyChoice value) => new NightEnemyChoice
        {
            Definition = value.Definition,
            Weight = value.Weight,
        };
        static V23_NightEventDefinition Freeze(NightEventDefinition value) => new V23_NightEventDefinition
        {
            Id = value.Id,
            FollowUp = value.FollowUp,
            Kind = (byte)value.Kind,
            Priority = value.Priority,
            MinTurn = value.MinTurn,
            MaxTurn = value.MaxTurn,
            Interval = value.Interval,
            Cooldown = value.Cooldown,
            WaveCount = value.WaveCount,
            ReturnDelay = value.ReturnDelay,
            PoolStart = value.PoolStart,
            PoolCount = value.PoolCount,
            ConditionStart = value.ConditionStart,
            ConditionCount = value.ConditionCount,
            Weight = value.Weight,
            BudgetScale = value.BudgetScale,
            Duration = value.Duration,
            Once = value.Once,
            ReturnOnly = value.ReturnOnly,
            Forced = value.Forced,
            WaveTimes = value.WaveTimes,
        };
        static NightEventDefinition Thaw(V23_NightEventDefinition value) => new NightEventDefinition
        {
            Id = value.Id,
            FollowUp = value.FollowUp,
            Kind = (NightKind)value.Kind,
            Priority = value.Priority,
            MinTurn = value.MinTurn,
            MaxTurn = value.MaxTurn,
            Interval = value.Interval,
            Cooldown = value.Cooldown,
            WaveCount = value.WaveCount,
            ReturnDelay = value.ReturnDelay,
            PoolStart = value.PoolStart,
            PoolCount = value.PoolCount,
            ConditionStart = value.ConditionStart,
            ConditionCount = value.ConditionCount,
            Weight = value.Weight,
            BudgetScale = value.BudgetScale,
            Duration = value.Duration,
            Once = value.Once,
            ReturnOnly = value.ReturnOnly,
            Forced = value.Forced,
            WaveTimes = value.WaveTimes,
        };
        static V23_NightEventHistory Freeze(NightEventHistory value) => new V23_NightEventHistory
        {
            Event = value.Event,
            LastTurn = value.LastTurn,
            Count = value.Count,
        };
        static NightEventHistory Thaw(V23_NightEventHistory value) => new NightEventHistory
        {
            Event = value.Event,
            LastTurn = value.LastTurn,
            Count = value.Count,
        };
        static V23_NightPlanState Freeze(NightPlanState value) => new V23_NightPlanState
        {
            Event = value.Event,
            Turn = value.Turn,
            BaseThreat = value.BaseThreat,
            PreparedTurn = value.PreparedTurn,
            BossDefinition = value.BossDefinition,
            CombatElapsed = value.CombatElapsed,
            FirstActionAt = value.FirstActionAt,
            ClockStarted = value.ClockStarted,
            Committed = value.Committed,
            AnySpawned = value.AnySpawned,
            BossKilled = value.BossKilled,
            BossEscaped = value.BossEscaped,
        };
        static NightPlanState Thaw(V23_NightPlanState value) => new NightPlanState
        {
            Event = value.Event,
            Turn = value.Turn,
            BaseThreat = value.BaseThreat,
            PreparedTurn = value.PreparedTurn,
            BossDefinition = value.BossDefinition,
            CombatElapsed = value.CombatElapsed,
            FirstActionAt = value.FirstActionAt,
            ClockStarted = value.ClockStarted,
            Committed = value.Committed,
            AnySpawned = value.AnySpawned,
            BossKilled = value.BossKilled,
            BossEscaped = value.BossEscaped,
        };
        static V23_NightPreparation Freeze(NightPreparation value) => new V23_NightPreparation
        {
            Definition = value.Definition,
            Health = value.Health,
            Damage = value.Damage,
            Speed = value.Speed,
            Range = value.Range,
            Interval = value.Interval,
            ProjectileSpeed = value.ProjectileSpeed,
            Combat = Freeze(value.Combat),
        };
        static NightPreparation Thaw(V23_NightPreparation value) => new NightPreparation
        {
            Definition = value.Definition,
            Health = value.Health,
            Damage = value.Damage,
            Speed = value.Speed,
            Range = value.Range,
            Interval = value.Interval,
            ProjectileSpeed = value.ProjectileSpeed,
            Combat = Thaw(value.Combat),
        };
        static V23_NightRules Freeze(NightRules value) => new V23_NightRules
        {
            EntryLeadSeconds = value.EntryLeadSeconds,
            WarningSeconds = value.WarningSeconds,
            ProtectionSeconds = value.ProtectionSeconds,
            SpawnSafety = value.SpawnSafety,
            BorderBuffer = value.BorderBuffer,
            HeroWeight = value.HeroWeight,
            FacilityWeight = value.FacilityWeight,
            TargetRadius = value.TargetRadius,
            ThreatFloor = value.ThreatFloor,
            ThreatPerStrengthCap = value.ThreatPerStrengthCap,
        };
        static NightRules Thaw(V23_NightRules value) => new NightRules
        {
            EntryLeadSeconds = value.EntryLeadSeconds,
            WarningSeconds = value.WarningSeconds,
            ProtectionSeconds = value.ProtectionSeconds,
            SpawnSafety = value.SpawnSafety,
            BorderBuffer = value.BorderBuffer,
            HeroWeight = value.HeroWeight,
            FacilityWeight = value.FacilityWeight,
            TargetRadius = value.TargetRadius,
            ThreatFloor = value.ThreatFloor,
            ThreatPerStrengthCap = value.ThreatPerStrengthCap,
        };
        static V23_NightWave Freeze(NightWave value) => new V23_NightWave
        {
            At = value.At,
            PowerScale = value.PowerScale,
            WarnedAt = value.WarnedAt,
            Definition = value.Definition,
            Count = value.Count,
            Direction = value.Direction,
            Region = value.Region,
            Position = Freeze(value.Position),
            Target = value.Target,
            Spawned = value.Spawned,
            Warned = value.Warned,
            SpatiallyBlocked = value.SpatiallyBlocked,
        };
        static NightWave Thaw(V23_NightWave value) => new NightWave
        {
            At = value.At,
            PowerScale = value.PowerScale,
            WarnedAt = value.WarnedAt,
            Definition = value.Definition,
            Count = value.Count,
            Direction = value.Direction,
            Region = value.Region,
            Position = Thaw(value.Position),
            Target = value.Target,
            Spawned = value.Spawned,
            Warned = value.Warned,
            SpatiallyBlocked = value.SpatiallyBlocked,
        };
        static V23_OpportunityProfile Freeze(OpportunityProfile value) => new V23_OpportunityProfile
        {
            Kind = (byte)value.Kind,
            Weight = value.Weight,
            MaximumPerNight = value.MaximumPerNight,
            StartFraction = value.StartFraction,
            EndFraction = value.EndFraction,
            Speed = value.Speed,
            MinimumResponse = value.MinimumResponse,
            CaptureRadius = value.CaptureRadius,
            ResponseRadius = value.ResponseRadius,
            RouteLength = value.RouteLength,
            Soldiers = value.Soldiers,
            Heroes = value.Heroes,
        };
        static OpportunityProfile Thaw(V23_OpportunityProfile value) => new OpportunityProfile
        {
            Kind = (VisitorKind)value.Kind,
            Weight = value.Weight,
            MaximumPerNight = value.MaximumPerNight,
            StartFraction = value.StartFraction,
            EndFraction = value.EndFraction,
            Speed = value.Speed,
            MinimumResponse = value.MinimumResponse,
            CaptureRadius = value.CaptureRadius,
            ResponseRadius = value.ResponseRadius,
            RouteLength = value.RouteLength,
            Soldiers = value.Soldiers,
            Heroes = value.Heroes,
        };
        static V23_PeacefulRules Freeze(PeacefulRules value) => new V23_PeacefulRules
        {
            MaximumPerNight = value.MaximumPerNight,
            MaximumConcurrent = value.MaximumConcurrent,
            TheftValueBudget = value.TheftValueBudget,
            FirstOpportunity = value.FirstOpportunity,
            Interval = value.Interval,
        };
        static PeacefulRules Thaw(V23_PeacefulRules value) => new PeacefulRules
        {
            MaximumPerNight = value.MaximumPerNight,
            MaximumConcurrent = value.MaximumConcurrent,
            TheftValueBudget = value.TheftValueBudget,
            FirstOpportunity = value.FirstOpportunity,
            Interval = value.Interval,
        };
        static V23_PendingItem Freeze(PendingItem value) => new V23_PendingItem
        {
            Item = value.Item,
            Amount = value.Amount,
            LossRemainder = value.LossRemainder,
        };
        static PendingItem Thaw(V23_PendingItem value) => new PendingItem
        {
            Item = value.Item,
            Amount = value.Amount,
            LossRemainder = value.LossRemainder,
        };
        static V23_PersonRequestEntry Freeze(PersonRequestEntry value) => new V23_PersonRequestEntry
        {
            Kind = (byte)value.Kind,
            Status = (byte)value.Status,
            CreatedTurn = value.CreatedTurn,
            ResolvedTurn = value.ResolvedTurn,
            Journey = value.Journey,
        };
        static PersonRequestEntry Thaw(V23_PersonRequestEntry value) => new PersonRequestEntry
        {
            Kind = (PersonRequestKind)value.Kind,
            Status = (PersonRequestStatus)value.Status,
            CreatedTurn = value.CreatedTurn,
            ResolvedTurn = value.ResolvedTurn,
            Journey = value.Journey,
        };
        static V23_PolicyChoice Freeze(PolicyChoice value) => new V23_PolicyChoice
        {
            Definition = value.Definition,
        };
        static PolicyChoice Thaw(V23_PolicyChoice value) => new PolicyChoice
        {
            Definition = value.Definition,
        };
        static V23_PortraitDNA Freeze(PortraitDNA value) => new V23_PortraitDNA
        {
            Parts = value.Parts,
            SkinDetails = value.SkinDetails,
            Skin = Freeze(value.Skin),
            Hair = Freeze(value.Hair),
            Eyes = Freeze(value.Eyes),
            Seed = value.Seed,
            Customized = value.Customized,
            InvitationAnnounced = value.InvitationAnnounced,
        };
        static PortraitDNA Thaw(V23_PortraitDNA value) => new PortraitDNA
        {
            Parts = value.Parts,
            SkinDetails = value.SkinDetails,
            Skin = Thaw(value.Skin),
            Hair = Thaw(value.Hair),
            Eyes = Thaw(value.Eyes),
            Seed = value.Seed,
            Customized = value.Customized,
            InvitationAnnounced = value.InvitationAnnounced,
        };
        static V23_PortraitSettings Freeze(PortraitSettings value) => new V23_PortraitSettings
        {
            YouthAge = value.YouthAge,
            GreyAge = value.GreyAge,
            ElderAge = value.ElderAge,
            SoldierRecruitMinAge = value.SoldierRecruitMinAge,
            SoldierRecruitMaxAge = value.SoldierRecruitMaxAge,
            SoldierLifeMin = value.SoldierLifeMin,
            SoldierLifeMax = value.SoldierLifeMax,
            ColorMutation = value.ColorMutation,
        };
        static PortraitSettings Thaw(V23_PortraitSettings value) => new PortraitSettings
        {
            YouthAge = value.YouthAge,
            GreyAge = value.GreyAge,
            ElderAge = value.ElderAge,
            SoldierRecruitMinAge = value.SoldierRecruitMinAge,
            SoldierRecruitMaxAge = value.SoldierRecruitMaxAge,
            SoldierLifeMin = value.SoldierLifeMin,
            SoldierLifeMax = value.SoldierLifeMax,
            ColorMutation = value.ColorMutation,
        };
        static V23_Quest Freeze(Quest value) => new V23_Quest
        {
            Status = (byte)value.Status,
            StartTurn = value.StartTurn,
            Deadline = value.Deadline,
            Source = value.Source,
            Container = value.Container,
            Slot = value.Slot,
            ContainerSlot = value.ContainerSlot,
            Mainline = value.Mainline,
        };
        static Quest Thaw(V23_Quest value) => new Quest
        {
            Status = (QuestStatus)value.Status,
            StartTurn = value.StartTurn,
            Deadline = value.Deadline,
            Source = value.Source,
            Container = value.Container,
            Slot = value.Slot,
            ContainerSlot = value.ContainerSlot,
            Mainline = value.Mainline,
        };
        static V23_QuestGenerationSettings Freeze(QuestGenerationSettings value) => new V23_QuestGenerationSettings
        {
            StrengthStep = value.StrengthStep,
            MarketValuePerStrength = value.MarketValuePerStrength,
            Low = Freeze(value.Low),
            Medium = Freeze(value.Medium),
            High = Freeze(value.High),
            Maximum = Freeze(value.Maximum),
        };
        static QuestGenerationSettings Thaw(V23_QuestGenerationSettings value) => new QuestGenerationSettings
        {
            StrengthStep = value.StrengthStep,
            MarketValuePerStrength = value.MarketValuePerStrength,
            Low = Thaw(value.Low),
            Medium = Thaw(value.Medium),
            High = Thaw(value.High),
            Maximum = Thaw(value.Maximum),
        };
        static V23_QuestOfferSlot Freeze(QuestOfferSlot value) => new V23_QuestOfferSlot
        {
            Type = value.Type,
            Index = value.Index,
            NextTurn = value.NextTurn,
        };
        static QuestOfferSlot Thaw(V23_QuestOfferSlot value) => new QuestOfferSlot
        {
            Type = value.Type,
            Index = value.Index,
            NextTurn = value.NextTurn,
        };
        static V23_QuestProgress Freeze(QuestProgress value) => new V23_QuestProgress
        {
            RuleIndex = value.RuleIndex,
            Amount = value.Amount,
            Key = value.Key,
        };
        static QuestProgress Thaw(V23_QuestProgress value) => new QuestProgress
        {
            RuleIndex = value.RuleIndex,
            Amount = value.Amount,
            Key = value.Key,
        };
        static V23_QuestTracking Freeze(QuestTracking value) => new V23_QuestTracking
        {
            Target = value.Target,
            Mode = value.Mode,
        };
        static QuestTracking Thaw(V23_QuestTracking value) => new QuestTracking
        {
            Target = value.Target,
            Mode = value.Mode,
        };
        static V23_RepairMaterial Freeze(RepairMaterial value) => new V23_RepairMaterial
        {
            Item = value.Item,
            Amount = value.Amount,
        };
        static RepairMaterial Thaw(V23_RepairMaterial value) => new RepairMaterial
        {
            Item = value.Item,
            Amount = value.Amount,
        };
        static V23_ResearchEntry Freeze(ResearchEntry value) => new V23_ResearchEntry
        {
            Definition = value.Definition,
            Progress = value.Progress,
            Completions = value.Completions,
            QueueOrder = value.QueueOrder,
        };
        static ResearchEntry Thaw(V23_ResearchEntry value) => new ResearchEntry
        {
            Definition = value.Definition,
            Progress = value.Progress,
            Completions = value.Completions,
            QueueOrder = value.QueueOrder,
        };
        static V23_Royal Freeze(Royal value) => new V23_Royal
        {
            Role = value.Role,
            Alive = value.Alive,
            Retired = value.Retired,
            FateUsed = value.FateUsed,
            Evidence = value.Evidence,
            TaskClaimed = value.TaskClaimed,
            EverMonarch = value.EverMonarch,
            Gender = (byte)value.Gender,
            MarriageRequestTurn = value.MarriageRequestTurn,
            MarriageCooldownUntil = value.MarriageCooldownUntil,
            RequestedSpouse = value.RequestedSpouse,
            MarriageRequestMonarch = value.MarriageRequestMonarch,
            Age = value.Age,
            Generation = value.Generation,
            ReignSince = value.ReignSince,
            FateUntil = value.FateUntil,
            LastGiftTurn = value.LastGiftTurn,
            Affection = value.Affection,
            VisitUntil = value.VisitUntil,
            Parent = value.Parent,
            SecondParent = value.SecondParent,
            Spouse = value.Spouse,
            Influence = value.Influence,
            Growth = value.Growth,
            Ambition = value.Ambition,
            Grievance = value.Grievance,
            InfluenceSource = value.InfluenceSource,
        };
        static Royal Thaw(V23_Royal value) => new Royal
        {
            Role = value.Role,
            Alive = value.Alive,
            Retired = value.Retired,
            FateUsed = value.FateUsed,
            Evidence = value.Evidence,
            TaskClaimed = value.TaskClaimed,
            EverMonarch = value.EverMonarch,
            Gender = (PersonGender)value.Gender,
            MarriageRequestTurn = value.MarriageRequestTurn,
            MarriageCooldownUntil = value.MarriageCooldownUntil,
            RequestedSpouse = value.RequestedSpouse,
            MarriageRequestMonarch = value.MarriageRequestMonarch,
            Age = value.Age,
            Generation = value.Generation,
            ReignSince = value.ReignSince,
            FateUntil = value.FateUntil,
            LastGiftTurn = value.LastGiftTurn,
            Affection = value.Affection,
            VisitUntil = value.VisitUntil,
            Parent = value.Parent,
            SecondParent = value.SecondParent,
            Spouse = value.Spouse,
            Influence = value.Influence,
            Growth = value.Growth,
            Ambition = value.Ambition,
            Grievance = value.Grievance,
            InfluenceSource = value.InfluenceSource,
        };
        static V23_Rule Freeze(Rule value) => new V23_Rule
        {
            Kind = (byte)value.Kind,
            Target = value.Target,
            Secondary = value.Secondary,
            Level = value.Level,
            Amount = value.Amount,
            B = value.B,
            C = value.C,
            Value = value.Value,
            Extra = value.Extra,
            Key = value.Key,
        };
        static Rule Thaw(V23_Rule value) => new Rule
        {
            Kind = (RuleKind)value.Kind,
            Target = value.Target,
            Secondary = value.Secondary,
            Level = value.Level,
            Amount = value.Amount,
            B = value.B,
            C = value.C,
            Value = value.Value,
            Extra = value.Extra,
            Key = value.Key,
        };
        static V23_Session Freeze(Session value) => new V23_Session
        {
            Turn = value.Turn,
            Stage = value.Stage,
            BasePopulation = value.BasePopulation,
            PublicOpinion = value.PublicOpinion,
            ExpeditionPenaltyStacks = value.ExpeditionPenaltyStacks,
            ExpeditionPenaltyUntil = value.ExpeditionPenaltyUntil,
            Phase = (byte)value.Phase,
            NightKind = (byte)value.NightKind,
            RandomState = value.RandomState,
            NightSeed = value.NightSeed,
            NextId = value.NextId,
            RetryCount = value.RetryCount,
            Threat = value.Threat,
            StartCombatStrength = value.StartCombatStrength,
            LastSettledTurn = value.LastSettledTurn,
            ResearchPoints = value.ResearchPoints,
            BossReturnTurn = value.BossReturnTurn,
            IntelAtNight = value.IntelAtNight,
            Time = value.Time,
            PhaseTime = value.PhaseTime,
            NightDuration = value.NightDuration,
            DeploymentTime = value.DeploymentTime,
            Paused = value.Paused,
            Initialized = value.Initialized,
            BossEscaped = value.BossEscaped,
            CheckpointPending = value.CheckpointPending,
            NightSpeed = value.NightSpeed,
            IntelligenceMode = value.IntelligenceMode,
            SelectedHero = Freeze(value.SelectedHero),
            ActiveBell = value.ActiveBell,
            DynastyName = value.DynastyName,
        };
        static Session Thaw(V23_Session value) => new Session
        {
            Turn = value.Turn,
            Stage = value.Stage,
            BasePopulation = value.BasePopulation,
            PublicOpinion = value.PublicOpinion,
            ExpeditionPenaltyStacks = value.ExpeditionPenaltyStacks,
            ExpeditionPenaltyUntil = value.ExpeditionPenaltyUntil,
            Phase = (Phase)value.Phase,
            NightKind = (NightKind)value.NightKind,
            RandomState = value.RandomState,
            NightSeed = value.NightSeed,
            NextId = value.NextId,
            RetryCount = value.RetryCount,
            Threat = value.Threat,
            StartCombatStrength = value.StartCombatStrength,
            LastSettledTurn = value.LastSettledTurn,
            ResearchPoints = value.ResearchPoints,
            BossReturnTurn = value.BossReturnTurn,
            IntelAtNight = value.IntelAtNight,
            Time = value.Time,
            PhaseTime = value.PhaseTime,
            NightDuration = value.NightDuration,
            DeploymentTime = value.DeploymentTime,
            Paused = value.Paused,
            Initialized = value.Initialized,
            BossEscaped = value.BossEscaped,
            CheckpointPending = value.CheckpointPending,
            NightSpeed = value.NightSpeed,
            IntelligenceMode = value.IntelligenceMode,
            SelectedHero = Thaw(value.SelectedHero),
            ActiveBell = value.ActiveBell,
            DynastyName = value.DynastyName,
        };
        static V23_Soldier Freeze(Soldier value) => new V23_Soldier
        {
            Garrison = value.Garrison,
            Slot = value.Slot,
            PopulationCost = value.PopulationCost,
            PendingSince = value.PendingSince,
            Experience = value.Experience,
            LastExperienceTurn = value.LastExperienceTurn,
            RecallState = value.RecallState,
        };
        static Soldier Thaw(V23_Soldier value) => new Soldier
        {
            Garrison = value.Garrison,
            Slot = value.Slot,
            PopulationCost = value.PopulationCost,
            PendingSince = value.PendingSince,
            Experience = value.Experience,
            LastExperienceTurn = value.LastExperienceTurn,
            RecallState = value.RecallState,
        };
        static V23_SoldierGrowth Freeze(SoldierGrowth value) => new V23_SoldierGrowth
        {
            MaxLevel = value.MaxLevel,
            FirstLevelExperience = value.FirstLevelExperience,
            ExperienceStep = value.ExperienceStep,
            BattleExperience = value.BattleExperience,
            HealthPerLevel = value.HealthPerLevel,
            DamagePerLevel = value.DamagePerLevel,
        };
        static SoldierGrowth Thaw(V23_SoldierGrowth value) => new SoldierGrowth
        {
            MaxLevel = value.MaxLevel,
            FirstLevelExperience = value.FirstLevelExperience,
            ExperienceStep = value.ExperienceStep,
            BattleExperience = value.BattleExperience,
            HealthPerLevel = value.HealthPerLevel,
            DamagePerLevel = value.DamagePerLevel,
        };
        static V23_SoldierPerson Freeze(SoldierPerson value) => new V23_SoldierPerson
        {
            Age = value.Age,
            Lifespan = value.Lifespan,
            LastAgeTurn = value.LastAgeTurn,
            Incarnation = value.Incarnation,
            Gender = (byte)value.Gender,
            SpecialAttention = value.SpecialAttention,
            DeathNotified = value.DeathNotified,
        };
        static SoldierPerson Thaw(V23_SoldierPerson value) => new SoldierPerson
        {
            Age = value.Age,
            Lifespan = value.Lifespan,
            LastAgeTurn = value.LastAgeTurn,
            Incarnation = value.Incarnation,
            Gender = (PersonGender)value.Gender,
            SpecialAttention = value.SpecialAttention,
            DeathNotified = value.DeathNotified,
        };
        static V23_SpawnRegion Freeze(SpawnRegion value) => new V23_SpawnRegion
        {
            Direction = value.Direction,
            Center = Freeze(value.Center),
            Size = Freeze(value.Size),
        };
        static SpawnRegion Thaw(V23_SpawnRegion value) => new SpawnRegion
        {
            Direction = value.Direction,
            Center = Thaw(value.Center),
            Size = Thaw(value.Size),
        };
        static V23_Talent Freeze(Talent value) => new V23_Talent
        {
            Slot = value.Slot,
            Experience = value.Experience,
            Level = value.Level,
            AssignedTurns = value.AssignedTurns,
            WageTurn = value.WageTurn,
            LastBenefitTurn = value.LastBenefitTurn,
            Recruited = value.Recruited,
            Paid = value.Paid,
        };
        static Talent Thaw(V23_Talent value) => new Talent
        {
            Slot = value.Slot,
            Experience = value.Experience,
            Level = value.Level,
            AssignedTurns = value.AssignedTurns,
            WageTurn = value.WageTurn,
            LastBenefitTurn = value.LastBenefitTurn,
            Recruited = value.Recruited,
            Paid = value.Paid,
        };
        static V23_TheftProfile Freeze(TheftProfile value) => new V23_TheftProfile
        {
            Protection = (byte)value.Protection,
            Weight = value.Weight,
            Maximum = value.Maximum,
            UnitValue = value.UnitValue,
        };
        static TheftProfile Thaw(V23_TheftProfile value) => new TheftProfile
        {
            Protection = (ItemProtection)value.Protection,
            Weight = value.Weight,
            Maximum = value.Maximum,
            UnitValue = value.UnitValue,
        };
        static V23_TraitEntry Freeze(TraitEntry value) => new V23_TraitEntry
        {
            Definition = value.Definition,
            Revealed = value.Revealed,
            Active = value.Active,
        };
        static TraitEntry Thaw(V23_TraitEntry value) => new TraitEntry
        {
            Definition = value.Definition,
            Revealed = value.Revealed,
            Active = value.Active,
        };
        static V23_UnresolvedBoss Freeze(UnresolvedBoss value) => new V23_UnresolvedBoss
        {
            Event = value.Event,
            Definition = value.Definition,
            DueTurn = value.DueTurn,
        };
        static UnresolvedBoss Thaw(V23_UnresolvedBoss value) => new UnresolvedBoss
        {
            Event = value.Event,
            Definition = value.Definition,
            DueTurn = value.DueTurn,
        };
        static V23_float3 Freeze(float3 value) => new V23_float3
        {
            x = value.x,
            y = value.y,
            z = value.z,
        };
        static float3 Thaw(V23_float3 value) => new float3
        {
            x = value.x,
            y = value.y,
            z = value.z,
        };
        static V23_float4 Freeze(float4 value) => new V23_float4
        {
            x = value.x,
            y = value.y,
            z = value.z,
            w = value.w,
        };
        static float4 Thaw(V23_float4 value) => new float4
        {
            x = value.x,
            y = value.y,
            z = value.z,
            w = value.w,
        };
        static V23_int2 Freeze(int2 value) => new V23_int2
        {
            x = value.x,
            y = value.y,
        };
        static int2 Thaw(V23_int2 value) => new int2
        {
            x = value.x,
            y = value.y,
        };
        static V23_int4 Freeze(int4 value) => new V23_int4
        {
            x = value.x,
            y = value.y,
            z = value.z,
            w = value.w,
        };
        static int4 Thaw(V23_int4 value) => new int4
        {
            x = value.x,
            y = value.y,
            z = value.z,
            w = value.w,
        };
        static V23_quaternion Freeze(quaternion value) => new V23_quaternion
        {
            value = Freeze(value.value),
        };
        static quaternion Thaw(V23_quaternion value) => new quaternion
        {
            value = Thaw(value.value),
        };
        public static void Write<T>(BinaryWriter writer, T value) where T : unmanaged
        {
            if (typeof(T) == typeof(BattleHistoryEntry)) { RawWrite(writer, Freeze((BattleHistoryEntry)(object)value)); return; }
            if (typeof(T) == typeof(BattleReportEntry)) { RawWrite(writer, Freeze((BattleReportEntry)(object)value)); return; }
            if (typeof(T) == typeof(Building)) { RawWrite(writer, Freeze((Building)(object)value)); return; }
            if (typeof(T) == typeof(BuildingInvestment)) { RawWrite(writer, Freeze((BuildingInvestment)(object)value)); return; }
            if (typeof(T) == typeof(BuildingPolicy)) { RawWrite(writer, Freeze((BuildingPolicy)(object)value)); return; }
            if (typeof(T) == typeof(Color32)) { RawWrite(writer, Freeze((Color32)(object)value)); return; }
            if (typeof(T) == typeof(CombatProfile)) { RawWrite(writer, Freeze((CombatProfile)(object)value)); return; }
            if (typeof(T) == typeof(ContentDefinition)) { RawWrite(writer, Freeze((ContentDefinition)(object)value)); return; }
            if (typeof(T) == typeof(CourtLogEntry)) { RawWrite(writer, Freeze((CourtLogEntry)(object)value)); return; }
            if (typeof(T) == typeof(CourtSettings)) { RawWrite(writer, Freeze((CourtSettings)(object)value)); return; }
            if (typeof(T) == typeof(CourtState)) { RawWrite(writer, Freeze((CourtState)(object)value)); return; }
            if (typeof(T) == typeof(DynastySettings)) { RawWrite(writer, Freeze((DynastySettings)(object)value)); return; }
            if (typeof(T) == typeof(EconomyEntry)) { RawWrite(writer, Freeze((EconomyEntry)(object)value)); return; }
            if (typeof(T) == typeof(Entitlement)) { RawWrite(writer, Freeze((Entitlement)(object)value)); return; }
            if (typeof(T) == typeof(Entity)) { RawWrite(writer, Freeze((Entity)(object)value)); return; }
            if (typeof(T) == typeof(Expedition)) { RawWrite(writer, Freeze((Expedition)(object)value)); return; }
            if (typeof(T) == typeof(ExpeditionDestinationHistory)) { RawWrite(writer, Freeze((ExpeditionDestinationHistory)(object)value)); return; }
            if (typeof(T) == typeof(ExpeditionSettings)) { RawWrite(writer, Freeze((ExpeditionSettings)(object)value)); return; }
            if (typeof(T) == typeof(ExpeditionSupply)) { RawWrite(writer, Freeze((ExpeditionSupply)(object)value)); return; }
            if (typeof(T) == typeof(FoodSelection)) { RawWrite(writer, Freeze((FoodSelection)(object)value)); return; }
            if (typeof(T) == typeof(GameSettings)) { RawWrite(writer, Freeze((GameSettings)(object)value)); return; }
            if (typeof(T) == typeof(GridCell)) { RawWrite(writer, Freeze((GridCell)(object)value)); return; }
            if (typeof(T) == typeof(Health)) { RawWrite(writer, Freeze((Health)(object)value)); return; }
            if (typeof(T) == typeof(Hero)) { RawWrite(writer, Freeze((Hero)(object)value)); return; }
            if (typeof(T) == typeof(HeroGrowth)) { RawWrite(writer, Freeze((HeroGrowth)(object)value)); return; }
            if (typeof(T) == typeof(HistoryEntry)) { RawWrite(writer, Freeze((HistoryEntry)(object)value)); return; }
            if (typeof(T) == typeof(Identity)) { RawWrite(writer, Freeze((Identity)(object)value)); return; }
            if (typeof(T) == typeof(InitialBuilding)) { RawWrite(writer, Freeze((InitialBuilding)(object)value)); return; }
            if (typeof(T) == typeof(InitialRoyal)) { RawWrite(writer, Freeze((InitialRoyal)(object)value)); return; }
            if (typeof(T) == typeof(InventorySlot)) { RawWrite(writer, Freeze((InventorySlot)(object)value)); return; }
            if (typeof(T) == typeof(LocalTransform)) { RawWrite(writer, Freeze((LocalTransform)(object)value)); return; }
            if (typeof(T) == typeof(NightEnemyChoice)) { RawWrite(writer, Freeze((NightEnemyChoice)(object)value)); return; }
            if (typeof(T) == typeof(NightEventDefinition)) { RawWrite(writer, Freeze((NightEventDefinition)(object)value)); return; }
            if (typeof(T) == typeof(NightEventHistory)) { RawWrite(writer, Freeze((NightEventHistory)(object)value)); return; }
            if (typeof(T) == typeof(NightPlanState)) { RawWrite(writer, Freeze((NightPlanState)(object)value)); return; }
            if (typeof(T) == typeof(NightPreparation)) { RawWrite(writer, Freeze((NightPreparation)(object)value)); return; }
            if (typeof(T) == typeof(NightRules)) { RawWrite(writer, Freeze((NightRules)(object)value)); return; }
            if (typeof(T) == typeof(NightWave)) { RawWrite(writer, Freeze((NightWave)(object)value)); return; }
            if (typeof(T) == typeof(OpportunityProfile)) { RawWrite(writer, Freeze((OpportunityProfile)(object)value)); return; }
            if (typeof(T) == typeof(PeacefulRules)) { RawWrite(writer, Freeze((PeacefulRules)(object)value)); return; }
            if (typeof(T) == typeof(PendingItem)) { RawWrite(writer, Freeze((PendingItem)(object)value)); return; }
            if (typeof(T) == typeof(PersonRequestEntry)) { RawWrite(writer, Freeze((PersonRequestEntry)(object)value)); return; }
            if (typeof(T) == typeof(PolicyChoice)) { RawWrite(writer, Freeze((PolicyChoice)(object)value)); return; }
            if (typeof(T) == typeof(PortraitDNA)) { RawWrite(writer, Freeze((PortraitDNA)(object)value)); return; }
            if (typeof(T) == typeof(PortraitSettings)) { RawWrite(writer, Freeze((PortraitSettings)(object)value)); return; }
            if (typeof(T) == typeof(Quest)) { RawWrite(writer, Freeze((Quest)(object)value)); return; }
            if (typeof(T) == typeof(QuestGenerationSettings)) { RawWrite(writer, Freeze((QuestGenerationSettings)(object)value)); return; }
            if (typeof(T) == typeof(QuestOfferSlot)) { RawWrite(writer, Freeze((QuestOfferSlot)(object)value)); return; }
            if (typeof(T) == typeof(QuestProgress)) { RawWrite(writer, Freeze((QuestProgress)(object)value)); return; }
            if (typeof(T) == typeof(QuestTracking)) { RawWrite(writer, Freeze((QuestTracking)(object)value)); return; }
            if (typeof(T) == typeof(RepairMaterial)) { RawWrite(writer, Freeze((RepairMaterial)(object)value)); return; }
            if (typeof(T) == typeof(ResearchEntry)) { RawWrite(writer, Freeze((ResearchEntry)(object)value)); return; }
            if (typeof(T) == typeof(Royal)) { RawWrite(writer, Freeze((Royal)(object)value)); return; }
            if (typeof(T) == typeof(Rule)) { RawWrite(writer, Freeze((Rule)(object)value)); return; }
            if (typeof(T) == typeof(Session)) { RawWrite(writer, Freeze((Session)(object)value)); return; }
            if (typeof(T) == typeof(Soldier)) { RawWrite(writer, Freeze((Soldier)(object)value)); return; }
            if (typeof(T) == typeof(SoldierGrowth)) { RawWrite(writer, Freeze((SoldierGrowth)(object)value)); return; }
            if (typeof(T) == typeof(SoldierPerson)) { RawWrite(writer, Freeze((SoldierPerson)(object)value)); return; }
            if (typeof(T) == typeof(SpawnRegion)) { RawWrite(writer, Freeze((SpawnRegion)(object)value)); return; }
            if (typeof(T) == typeof(Talent)) { RawWrite(writer, Freeze((Talent)(object)value)); return; }
            if (typeof(T) == typeof(TheftProfile)) { RawWrite(writer, Freeze((TheftProfile)(object)value)); return; }
            if (typeof(T) == typeof(TraitEntry)) { RawWrite(writer, Freeze((TraitEntry)(object)value)); return; }
            if (typeof(T) == typeof(UnresolvedBoss)) { RawWrite(writer, Freeze((UnresolvedBoss)(object)value)); return; }
            if (typeof(T) == typeof(float3)) { RawWrite(writer, Freeze((float3)(object)value)); return; }
            if (typeof(T) == typeof(float4)) { RawWrite(writer, Freeze((float4)(object)value)); return; }
            if (typeof(T) == typeof(int2)) { RawWrite(writer, Freeze((int2)(object)value)); return; }
            if (typeof(T) == typeof(int4)) { RawWrite(writer, Freeze((int4)(object)value)); return; }
            if (typeof(T) == typeof(quaternion)) { RawWrite(writer, Freeze((quaternion)(object)value)); return; }
            if (typeof(T) == typeof(FixedList128Bytes<int>)) { RawWrite(writer, value); return; }
            if (typeof(T) == typeof(FixedList512Bytes<float>)) { RawWrite(writer, value); return; }
            if (typeof(T) == typeof(FixedList64Bytes<int>)) { RawWrite(writer, value); return; }
            if (typeof(T) == typeof(FixedString128Bytes)) { RawWrite(writer, value); return; }
            if (typeof(T) == typeof(FixedString64Bytes)) { RawWrite(writer, value); return; }
            throw new InvalidDataException("Unregistered legacy layout: " + typeof(T));
        }
        public static T Read<T>(BinaryReader reader) where T : unmanaged
        {
            if (typeof(T) == typeof(BattleHistoryEntry)) return (T)(object)Thaw(RawRead<V23_BattleHistoryEntry>(reader));
            if (typeof(T) == typeof(BattleReportEntry)) return (T)(object)Thaw(RawRead<V23_BattleReportEntry>(reader));
            if (typeof(T) == typeof(Building)) return (T)(object)Thaw(RawRead<V23_Building>(reader));
            if (typeof(T) == typeof(BuildingInvestment)) return (T)(object)Thaw(RawRead<V23_BuildingInvestment>(reader));
            if (typeof(T) == typeof(BuildingPolicy)) return (T)(object)Thaw(RawRead<V23_BuildingPolicy>(reader));
            if (typeof(T) == typeof(Color32)) return (T)(object)Thaw(RawRead<V23_Color32>(reader));
            if (typeof(T) == typeof(CombatProfile)) return (T)(object)Thaw(RawRead<V23_CombatProfile>(reader));
            if (typeof(T) == typeof(ContentDefinition)) return (T)(object)Thaw(RawRead<V23_ContentDefinition>(reader));
            if (typeof(T) == typeof(CourtLogEntry)) return (T)(object)Thaw(RawRead<V23_CourtLogEntry>(reader));
            if (typeof(T) == typeof(CourtSettings)) return (T)(object)Thaw(RawRead<V23_CourtSettings>(reader));
            if (typeof(T) == typeof(CourtState)) return (T)(object)Thaw(RawRead<V23_CourtState>(reader));
            if (typeof(T) == typeof(DynastySettings)) return (T)(object)Thaw(RawRead<V23_DynastySettings>(reader));
            if (typeof(T) == typeof(EconomyEntry)) return (T)(object)Thaw(RawRead<V23_EconomyEntry>(reader));
            if (typeof(T) == typeof(Entitlement)) return (T)(object)Thaw(RawRead<V23_Entitlement>(reader));
            if (typeof(T) == typeof(Entity)) return (T)(object)Thaw(RawRead<V23_Entity>(reader));
            if (typeof(T) == typeof(Expedition)) return (T)(object)Thaw(RawRead<V23_Expedition>(reader));
            if (typeof(T) == typeof(ExpeditionDestinationHistory)) return (T)(object)Thaw(RawRead<V23_ExpeditionDestinationHistory>(reader));
            if (typeof(T) == typeof(ExpeditionSettings)) return (T)(object)Thaw(RawRead<V23_ExpeditionSettings>(reader));
            if (typeof(T) == typeof(ExpeditionSupply)) return (T)(object)Thaw(RawRead<V23_ExpeditionSupply>(reader));
            if (typeof(T) == typeof(FoodSelection)) return (T)(object)Thaw(RawRead<V23_FoodSelection>(reader));
            if (typeof(T) == typeof(GameSettings)) return (T)(object)Thaw(RawRead<V23_GameSettings>(reader));
            if (typeof(T) == typeof(GridCell)) return (T)(object)Thaw(RawRead<V23_GridCell>(reader));
            if (typeof(T) == typeof(Health)) return (T)(object)Thaw(RawRead<V23_Health>(reader));
            if (typeof(T) == typeof(Hero)) return (T)(object)Thaw(RawRead<V23_Hero>(reader));
            if (typeof(T) == typeof(HeroGrowth)) return (T)(object)Thaw(RawRead<V23_HeroGrowth>(reader));
            if (typeof(T) == typeof(HistoryEntry)) return (T)(object)Thaw(RawRead<V23_HistoryEntry>(reader));
            if (typeof(T) == typeof(Identity)) return (T)(object)Thaw(RawRead<V23_Identity>(reader));
            if (typeof(T) == typeof(InitialBuilding)) return (T)(object)Thaw(RawRead<V23_InitialBuilding>(reader));
            if (typeof(T) == typeof(InitialRoyal)) return (T)(object)Thaw(RawRead<V23_InitialRoyal>(reader));
            if (typeof(T) == typeof(InventorySlot)) return (T)(object)Thaw(RawRead<V23_InventorySlot>(reader));
            if (typeof(T) == typeof(LocalTransform)) return (T)(object)Thaw(RawRead<V23_LocalTransform>(reader));
            if (typeof(T) == typeof(NightEnemyChoice)) return (T)(object)Thaw(RawRead<V23_NightEnemyChoice>(reader));
            if (typeof(T) == typeof(NightEventDefinition)) return (T)(object)Thaw(RawRead<V23_NightEventDefinition>(reader));
            if (typeof(T) == typeof(NightEventHistory)) return (T)(object)Thaw(RawRead<V23_NightEventHistory>(reader));
            if (typeof(T) == typeof(NightPlanState)) return (T)(object)Thaw(RawRead<V23_NightPlanState>(reader));
            if (typeof(T) == typeof(NightPreparation)) return (T)(object)Thaw(RawRead<V23_NightPreparation>(reader));
            if (typeof(T) == typeof(NightRules)) return (T)(object)Thaw(RawRead<V23_NightRules>(reader));
            if (typeof(T) == typeof(NightWave)) return (T)(object)Thaw(RawRead<V23_NightWave>(reader));
            if (typeof(T) == typeof(OpportunityProfile)) return (T)(object)Thaw(RawRead<V23_OpportunityProfile>(reader));
            if (typeof(T) == typeof(PeacefulRules)) return (T)(object)Thaw(RawRead<V23_PeacefulRules>(reader));
            if (typeof(T) == typeof(PendingItem)) return (T)(object)Thaw(RawRead<V23_PendingItem>(reader));
            if (typeof(T) == typeof(PersonRequestEntry)) return (T)(object)Thaw(RawRead<V23_PersonRequestEntry>(reader));
            if (typeof(T) == typeof(PolicyChoice)) return (T)(object)Thaw(RawRead<V23_PolicyChoice>(reader));
            if (typeof(T) == typeof(PortraitDNA)) return (T)(object)Thaw(RawRead<V23_PortraitDNA>(reader));
            if (typeof(T) == typeof(PortraitSettings)) return (T)(object)Thaw(RawRead<V23_PortraitSettings>(reader));
            if (typeof(T) == typeof(Quest)) return (T)(object)Thaw(RawRead<V23_Quest>(reader));
            if (typeof(T) == typeof(QuestGenerationSettings)) return (T)(object)Thaw(RawRead<V23_QuestGenerationSettings>(reader));
            if (typeof(T) == typeof(QuestOfferSlot)) return (T)(object)Thaw(RawRead<V23_QuestOfferSlot>(reader));
            if (typeof(T) == typeof(QuestProgress)) return (T)(object)Thaw(RawRead<V23_QuestProgress>(reader));
            if (typeof(T) == typeof(QuestTracking)) return (T)(object)Thaw(RawRead<V23_QuestTracking>(reader));
            if (typeof(T) == typeof(RepairMaterial)) return (T)(object)Thaw(RawRead<V23_RepairMaterial>(reader));
            if (typeof(T) == typeof(ResearchEntry)) return (T)(object)Thaw(RawRead<V23_ResearchEntry>(reader));
            if (typeof(T) == typeof(Royal)) return (T)(object)Thaw(RawRead<V23_Royal>(reader));
            if (typeof(T) == typeof(Rule)) return (T)(object)Thaw(RawRead<V23_Rule>(reader));
            if (typeof(T) == typeof(Session)) return (T)(object)Thaw(RawRead<V23_Session>(reader));
            if (typeof(T) == typeof(Soldier)) return (T)(object)Thaw(RawRead<V23_Soldier>(reader));
            if (typeof(T) == typeof(SoldierGrowth)) return (T)(object)Thaw(RawRead<V23_SoldierGrowth>(reader));
            if (typeof(T) == typeof(SoldierPerson)) return (T)(object)Thaw(RawRead<V23_SoldierPerson>(reader));
            if (typeof(T) == typeof(SpawnRegion)) return (T)(object)Thaw(RawRead<V23_SpawnRegion>(reader));
            if (typeof(T) == typeof(Talent)) return (T)(object)Thaw(RawRead<V23_Talent>(reader));
            if (typeof(T) == typeof(TheftProfile)) return (T)(object)Thaw(RawRead<V23_TheftProfile>(reader));
            if (typeof(T) == typeof(TraitEntry)) return (T)(object)Thaw(RawRead<V23_TraitEntry>(reader));
            if (typeof(T) == typeof(UnresolvedBoss)) return (T)(object)Thaw(RawRead<V23_UnresolvedBoss>(reader));
            if (typeof(T) == typeof(float3)) return (T)(object)Thaw(RawRead<V23_float3>(reader));
            if (typeof(T) == typeof(float4)) return (T)(object)Thaw(RawRead<V23_float4>(reader));
            if (typeof(T) == typeof(int2)) return (T)(object)Thaw(RawRead<V23_int2>(reader));
            if (typeof(T) == typeof(int4)) return (T)(object)Thaw(RawRead<V23_int4>(reader));
            if (typeof(T) == typeof(quaternion)) return (T)(object)Thaw(RawRead<V23_quaternion>(reader));
            if (typeof(T) == typeof(FixedList128Bytes<int>)) return RawRead<T>(reader);
            if (typeof(T) == typeof(FixedList512Bytes<float>)) return RawRead<T>(reader);
            if (typeof(T) == typeof(FixedList64Bytes<int>)) return RawRead<T>(reader);
            if (typeof(T) == typeof(FixedString128Bytes)) return RawRead<T>(reader);
            if (typeof(T) == typeof(FixedString64Bytes)) return RawRead<T>(reader);
            throw new InvalidDataException("Unregistered legacy layout: " + typeof(T));
        }
        static unsafe void RawWrite<T>(BinaryWriter writer, T value) where T : unmanaged
        { var bytes = new byte[UnsafeUtility.SizeOf<T>()]; fixed (byte* p = bytes) UnsafeUtility.CopyStructureToPtr(ref value, p); writer.Write(bytes); }
        static unsafe T RawRead<T>(BinaryReader reader) where T : unmanaged
        { var bytes = reader.ReadBytes(UnsafeUtility.SizeOf<T>()); if (bytes.Length != UnsafeUtility.SizeOf<T>()) throw new EndOfStreamException(); fixed (byte* p = bytes) return UnsafeUtility.ReadArrayElement<T>(p, 0); }
    }
}
