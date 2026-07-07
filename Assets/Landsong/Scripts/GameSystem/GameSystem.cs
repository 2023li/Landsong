using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.ExpeditionSystem;
using Landsong.GameEventSystem;
using Landsong.InheritanceSystem;
using Landsong.InventorySystem;
using Landsong.TalentSystem;
using Landsong.TechnologySystem;
using Landsong.TurnSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong
{
    public enum QuestObjectiveType
    {
        BuildBuildings = 1,
        SubmitResources = 2
    }

    public enum QuestStatus
    {
        Active = 0,
        Completed = 1,
        Failed = 2
    }

    [Serializable]
    public sealed class GameQuestDefinition
    {
        [SerializeField, LabelText("任务ID")] private string questId;
        [SerializeField, LabelText("任务名称")] private string displayName;
        [SerializeField, LabelText("任务描述"), TextArea] private string description;
        [SerializeField, LabelText("任务类型")] private QuestObjectiveType objectiveType = QuestObjectiveType.BuildBuildings;
        [SerializeField, LabelText("期限回合数"), Min(1)] private int turnLimit = 10;

        [SerializeField, LabelText("目标建筑")] private BuildingBase targetBuilding;
        [SerializeField, LabelText("目标建筑数量"), Min(1)] private int targetBuildingCount = 1;

        [SerializeField, LabelText("提交资源")] private ItemAmount[] requiredResources = Array.Empty<ItemAmount>();

        public string QuestId => NormalizeQuestId(questId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(QuestId) ? "未命名任务" : QuestId)
            : displayName.Trim();
        public string Description => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        public QuestObjectiveType ObjectiveType => objectiveType;
        public int TurnLimit => Mathf.Max(1, turnLimit);
        public BuildingBase TargetBuilding => targetBuilding;
        public string TargetBuildingId => targetBuilding == null || !targetBuilding.HasDefinition
            ? string.Empty
            : targetBuilding.Definition.BuildingId;
        public string TargetBuildingDisplayName => targetBuilding == null || !targetBuilding.HasDefinition
            ? string.Empty
            : targetBuilding.Definition.DisplayName;
        public Sprite TargetBuildingIcon => targetBuilding == null || !targetBuilding.HasDefinition
            ? null
            : targetBuilding.Definition.Icon;
        public int TargetBuildingCount => Mathf.Max(1, targetBuildingCount);
        public IReadOnlyList<ItemAmount> RequiredResources => requiredResources ?? Array.Empty<ItemAmount>();
        public bool IsValid => !string.IsNullOrWhiteSpace(QuestId) && HasValidObjective();

        public void Normalize()
        {
            questId = NormalizeQuestId(questId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            turnLimit = Mathf.Max(1, turnLimit);
            targetBuildingCount = Mathf.Max(1, targetBuildingCount);

            if (requiredResources == null)
            {
                requiredResources = Array.Empty<ItemAmount>();
                return;
            }

            for (var i = 0; i < requiredResources.Length; i++)
            {
                requiredResources[i] = requiredResources[i].Normalized();
            }
        }

        public static string NormalizeQuestId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private bool HasValidObjective()
        {
            return objectiveType switch
            {
                QuestObjectiveType.BuildBuildings => !string.IsNullOrWhiteSpace(TargetBuildingId) && TargetBuildingCount > 0,
                QuestObjectiveType.SubmitResources => HasAnyRequiredResource(),
                _ => false
            };
        }

        private bool HasAnyRequiredResource()
        {
            if (requiredResources == null)
            {
                return false;
            }

            for (var i = 0; i < requiredResources.Length; i++)
            {
                if (requiredResources[i].Normalized().IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class GameQuestResourceProgress
    {
        public string ItemId { get; internal set; } = string.Empty;
        public ItemDefinition ItemDefinition { get; internal set; }
        public int RequiredAmount { get; internal set; }
        public int SubmittedAmount { get; internal set; }
        public int InventoryAmount { get; internal set; }
        public string DisplayName => ItemDefinition == null ? ItemId : ItemDefinition.DisplayName;
        public int RemainingAmount => Mathf.Max(0, RequiredAmount - SubmittedAmount);
        public bool IsComplete => RequiredAmount <= 0 || SubmittedAmount >= RequiredAmount;
    }

    [Serializable]
    public sealed class GameQuestState
    {
        private readonly List<GameQuestResourceProgress> resourceProgresses =
            new List<GameQuestResourceProgress>();

        public GameQuestState(GameQuestDefinition definition, int startedTurn)
        {
            Definition = definition;
            QuestId = definition == null ? string.Empty : definition.QuestId;
            StartedTurn = Mathf.Max(1, startedTurn);
            DeadlineTurn = StartedTurn + Mathf.Max(1, definition == null ? 1 : definition.TurnLimit) - 1;
            Status = QuestStatus.Active;
        }

        public GameQuestDefinition Definition { get; }
        public string QuestId { get; }
        public QuestStatus Status { get; internal set; }
        public int StartedTurn { get; internal set; }
        public int DeadlineTurn { get; internal set; }
        public int CurrentAmount { get; internal set; }
        public int TargetAmount { get; internal set; }
        public string TargetDisplayName { get; internal set; } = string.Empty;
        public Sprite Icon { get; internal set; }
        public IReadOnlyList<GameQuestResourceProgress> ResourceProgresses => resourceProgresses;
        public bool IsActive => Status == QuestStatus.Active;
        public bool IsCompleted => Status == QuestStatus.Completed;
        public bool IsFailed => Status == QuestStatus.Failed;
        public bool IsResourceSubmission => Definition != null && Definition.ObjectiveType == QuestObjectiveType.SubmitResources;
        public int TotalRequiredAmount => TargetAmount;
        public int TotalSubmittedAmount => CurrentAmount;
        public float Progress01 => TargetAmount <= 0 ? 1f : Mathf.Clamp01(CurrentAmount / (float)TargetAmount);

        public int GetRemainingTurns(int currentTurn)
        {
            return Mathf.Max(0, DeadlineTurn - Mathf.Max(1, currentTurn) + 1);
        }

        public bool CanSubmitResources()
        {
            if (!IsActive || !IsResourceSubmission)
            {
                return false;
            }

            for (var i = 0; i < resourceProgresses.Count; i++)
            {
                var progress = resourceProgresses[i];
                if (progress != null && progress.RemainingAmount > 0 && progress.InventoryAmount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal void ClearResourceProgresses()
        {
            resourceProgresses.Clear();
        }

        internal void AddResourceProgress(GameQuestResourceProgress progress)
        {
            if (progress != null)
            {
                resourceProgresses.Add(progress);
            }
        }
    }

    [Serializable]
    public sealed class QuestSaveData
    {
        public List<QuestStateSaveData> Quests = new List<QuestStateSaveData>();

        public void Validate()
        {
            Quests ??= new List<QuestStateSaveData>();
            var seenQuestIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = Quests.Count - 1; i >= 0; i--)
            {
                var quest = Quests[i];
                if (quest == null)
                {
                    Quests.RemoveAt(i);
                    continue;
                }

                quest.Validate();
                if (string.IsNullOrWhiteSpace(quest.QuestId) || !seenQuestIds.Add(quest.QuestId))
                {
                    Quests.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class QuestStateSaveData
    {
        public string QuestId = string.Empty;
        public QuestStatus Status = QuestStatus.Active;
        public int StartedTurn = 1;
        public int DeadlineTurn = 1;
        public List<QuestResourceSubmissionSaveData> SubmittedResources =
            new List<QuestResourceSubmissionSaveData>();

        public void Validate()
        {
            QuestId = GameQuestDefinition.NormalizeQuestId(QuestId);
            StartedTurn = Mathf.Max(1, StartedTurn);
            DeadlineTurn = Mathf.Max(StartedTurn, DeadlineTurn);
            SubmittedResources ??= new List<QuestResourceSubmissionSaveData>();

            var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = SubmittedResources.Count - 1; i >= 0; i--)
            {
                var resource = SubmittedResources[i];
                if (resource == null)
                {
                    SubmittedResources.RemoveAt(i);
                    continue;
                }

                resource.Validate();
                if (string.IsNullOrWhiteSpace(resource.ItemId) || !seenItemIds.Add(resource.ItemId))
                {
                    SubmittedResources.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class QuestResourceSubmissionSaveData
    {
        public string ItemId = string.Empty;
        public int SubmittedAmount;

        public void Validate()
        {
            ItemId = string.IsNullOrWhiteSpace(ItemId) ? string.Empty : ItemId.Trim();
            SubmittedAmount = Mathf.Max(0, SubmittedAmount);
        }
    }

    public enum GameOverReason
    {
        None = 0,
        NoPalace = 1
    }

    [DisallowMultipleComponent]
    public sealed class GameSystem : MonoSingleton<GameSystem>, IBuildingJobAttractionModifierProvider
    {
        private static readonly IReadOnlyList<BuildingJobAttractionModifier> EmptyJobAttractionModifiers =
            Array.Empty<BuildingJobAttractionModifier>();

        private readonly List<BM_库存格容量> inventorySlotCapacityModules =
            new List<BM_库存格容量>();
        private readonly HashSet<string> unlockedTechnologies =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unlockedBuildingBlueprintIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<BuildingJobAttractionModifier> activeJobAttractionModifiers =
            new List<BuildingJobAttractionModifier>();
        private readonly List<GameQuestState> quests = new List<GameQuestState>();
        private int pendingTechnologyPointsThisTurn;
        private int turnWithMissingResearchWarning = -1;
        private InventoryService subscribedQuestInventory;
        private BuildingService subscribedQuestBuildings;
        private TurnService subscribedQuestTurn;
        private bool questsInitialized;
        private bool suppressQuestInventoryRefresh;
        private bool suppressQuestRuntimeMessages;

        [Header("Inventory")]
        [SerializeField, LabelText("物品目录")] private ItemCatalog itemCatalog;
        [SerializeField, LabelText("库存格子数量"), Min(0)] private int inventorySlotCount = 24;
        [SerializeField, LabelText("初始物品")] private ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        [Header("Dynasty")]
        [SerializeField, LabelText("初始王朝名称")] private string startingDynastyName = DynastyService.DefaultDynastyName;
        [SerializeField, LabelText("初始人口"), Min(0)] private int startingPopulation;
        [SerializeField, LabelText("回合结束时无王宫则结束游戏")] private bool endGameWhenNoPalaceAtTurnEnd = true;

        [Header("Technology")]
        [SerializeField, LabelText("科技目录")] private TechnologyCatalog technologyCatalog;
        [SerializeField, LabelText("初始当前研究科技")] private TechnologyDefinition startingResearchTechnology;
        [SerializeField, LabelText("初始当前研究进度"), Min(0)] private int startingResearchProgress;
        [SerializeField, LabelText("初始已解锁科技")] private string[] startingUnlockedTechnologies = Array.Empty<string>();

        [Header("Quest")]
        [SerializeField, LabelText("初始任务")] private GameQuestDefinition[] startingQuests = Array.Empty<GameQuestDefinition>();

        [Header("Expedition")]
        [SerializeField, LabelText("远征目的地目录")] private ExpeditionDestinationCatalog expeditionDestinationCatalog;
        [SerializeField, LabelText("远征补贴金币物品")] private ItemDefinition expeditionSubsidyGoldItemDefinition;
        [SerializeField, LabelText("初始已解锁奇迹蓝图")] private string[] startingUnlockedBuildingBlueprintIds = Array.Empty<string>();
        [SerializeField, LabelText("补贴不足惩罚持续回合"), Min(1)] private int expeditionSubsidyPenaltyDurationTurns = 5;
        [SerializeField, LabelText("每层补贴不足惩罚岗位吸引力"), Min(0f)] private float expeditionSubsidyPenaltyAttractionPerStack = 5f;

        [Header("Talent")]
        [SerializeField, LabelText("人才目录")] private TalentCatalog talentCatalog;
        [SerializeField, LabelText("人才金币物品")] private ItemDefinition talentGoldItemDefinition;
        [SerializeField, LabelText("刷新费用"), Min(0)] private int talentRefreshGoldCost = 20;
        [SerializeField, LabelText("每次刷新卡数"), Min(1)] private int talentRefreshCardCount = 3;
        [SerializeField, LabelText("初始人才槽")] private TalentSlotDefinition[] startingTalentSlots = Array.Empty<TalentSlotDefinition>();

        [Header("Inheritance")]
        [SerializeField, LabelText("王族特性目录")] private RoyalTraitCatalog royalTraitCatalog;
        [SerializeField, LabelText("继承系统配置")] private RoyalInheritanceConfig royalInheritanceConfig = new RoyalInheritanceConfig();

        [Header("Scene Systems")]
        [SerializeField, LabelText("建筑目录")] private BuildingCatalog buildingCatalog;
        [SerializeField, LabelText("建筑选择控制器")] private BuildingSelectionController buildingSelection;

        [ShowInInspector, ReadOnly, LabelText("库存服务")]
        public InventoryService Inventory { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("回合服务")]
        public TurnService Turn { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("王朝服务")]
        public DynastyService Dynasty { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("建筑服务")]
        public BuildingService Buildings { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("事件服务")]
        public GameEventService Events { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("科技服务")]
        public TechnologyService Technology { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("远征服务")]
        public ExpeditionService Expeditions { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("人才服务")]
        public TalentService Talents { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("继承服务")]
        public RoyalInheritanceService Inheritance { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("当前建筑选择控制器")]
        public BuildingSelectionController BuildingSelection => buildingSelection;

        [ShowInInspector, ReadOnly, LabelText("建筑目录")]
        public BuildingCatalog BuildingCatalog => buildingCatalog;

        [ShowInInspector, ReadOnly, LabelText("科技目录")]
        public TechnologyCatalog TechnologyCatalog => technologyCatalog;

        [ShowInInspector, ReadOnly, LabelText("当前人口")]
        public int Population => Dynasty == null ? startingPopulation : Dynasty.Population;

        [ShowInInspector, ReadOnly, LabelText("王朝名称")]
        public string DynastyName => Dynasty == null ? startingDynastyName : Dynasty.DynastyName;

        [ShowInInspector, ReadOnly, LabelText("拥有王宫")]
        public bool HasPalace => Dynasty != null && Dynasty.HasPalace;

        [ShowInInspector, ReadOnly, LabelText("游戏已结束")]
        public bool IsGameOver { get; private set; }

        public event Action<GameSystem, GameOverReason> GameEnded;
        public event Action<GameSystem> QuestsChanged;
        public event Action<GameSystem> ExpeditionsChanged;
        public event Action<GameSystem> TalentsChanged;
        public event Action<GameSystem> InheritanceChanged;
        public event Action<GameSystem> BuildingBlueprintsChanged;

        public string CurrentResearchTechnologyId =>
            Technology == null
                ? (startingResearchTechnology == null ? string.Empty : startingResearchTechnology.TechnologyId)
                : Technology.CurrentResearchTechnologyId;

        public int CurrentResearchProgress =>
            Technology == null ? startingResearchProgress : Technology.CurrentResearchProgress;

        public int CurrentResearchRequiredPoints =>
            Technology == null || !Technology.HasCurrentResearch ? 0 : Technology.CurrentResearchRequiredPoints;

        public IReadOnlyCollection<string> UnlockedTechnologies =>
            Technology == null ? unlockedTechnologies : Technology.UnlockedTechnologyIds;

        public IReadOnlyList<GameQuestState> Quests => quests;
        public IReadOnlyList<ExpeditionState> ExpeditionStates =>
            Expeditions == null ? Array.Empty<ExpeditionState>() : Expeditions.Expeditions;
        public ExpeditionDestinationCatalog ExpeditionDestinationCatalog => expeditionDestinationCatalog;
        public IReadOnlyList<TalentState> TalentPool =>
            Talents == null ? Array.Empty<TalentState>() : Talents.OwnedTalents;
        public IReadOnlyList<TalentOfferState> TalentOffers =>
            Talents == null ? Array.Empty<TalentOfferState>() : Talents.CurrentOffers;
        public IReadOnlyList<TalentSlotRuntimeState> TalentSlots =>
            Talents == null ? Array.Empty<TalentSlotRuntimeState>() : Talents.SlotStates;
        public TalentCatalog TalentCatalog => talentCatalog;
        public RoyalTraitCatalog RoyalTraitCatalog => royalTraitCatalog;
        public RoyalCharacterState CurrentKing => Inheritance == null ? null : Inheritance.CurrentKing;
        public RoyalCharacterState CurrentQueen => Inheritance == null ? null : Inheritance.CurrentQueen;
        public IReadOnlyList<RoyalCharacterState> RoyalCharacters =>
            Inheritance == null ? Array.Empty<RoyalCharacterState>() : Inheritance.Characters;
        public IReadOnlyCollection<string> UnlockedBuildingBlueprintIds => unlockedBuildingBlueprintIds;
        public bool HasActiveExpeditionSubsidyPenalty =>
            Expeditions != null && Expeditions.IsSubsidyPenaltyActive;
        public int ExpeditionSubsidyPenaltyStacks =>
            Expeditions == null ? 0 : Expeditions.SubsidyPenaltyStacks;
        public int ExpeditionSubsidyPenaltyActiveUntilTurn =>
            Expeditions == null ? 0 : Expeditions.SubsidyPenaltyActiveUntilTurn;

        public IReadOnlyList<BuildingJobAttractionModifier> GetJobAttractionModifiers(BuildingBase building)
        {
            activeJobAttractionModifiers.Clear();
            if (HasActiveExpeditionSubsidyPenalty && ExpeditionSubsidyPenaltyStacks > 0)
            {
                var value = -Mathf.Max(0f, expeditionSubsidyPenaltyAttractionPerStack)
                            * ExpeditionSubsidyPenaltyStacks;
                activeJobAttractionModifiers.Add(
                    new BuildingJobAttractionModifier(
                        "expedition_subsidy_penalty",
                        "远征补贴不足",
                        value,
                        "Expedition",
                        "远征失败补贴不足造成的全局岗位吸引力惩罚。"));
            }

            return activeJobAttractionModifiers.Count == 0
                ? EmptyJobAttractionModifiers
                : activeJobAttractionModifiers.ToArray();
        }

        protected override void Init()
        {
            CreateTechnologyService();
            CreateInventoryService();
            CreateDynastyService();
            CreateTurnService();
            CreateBuildingService();
            CreateGameEventService();
            InitializeUnlockedBuildingBlueprints();
            CreateExpeditionService();
            CreateTalentService();
            CreateInheritanceService();
            CreateQuestService();
            ResolveBuildingSelectionController();
        }

        private void Update()
        {
            UpdateTurnInput();
        }

        private void OnDestroy()
        {
            UnsubscribeQuestRuntimeServices();
            UnsubscribeExpeditionService();
            UnsubscribeTalentService();
            UnsubscribeInheritanceService();
            buildingSelection = null;
        }

        private void OnValidate()
        {
            startingDynastyName = DynastyService.NormalizeDynastyName(startingDynastyName);
            inventorySlotCount = Mathf.Max(0, inventorySlotCount);
            startingPopulation = Mathf.Max(0, startingPopulation);
            startingResearchProgress = Mathf.Max(0, startingResearchProgress);
            startingTurn = Mathf.Max(1, startingTurn);
            turnBuildingsPerFrame = Mathf.Max(1, turnBuildingsPerFrame);
            expeditionSubsidyPenaltyDurationTurns = Mathf.Max(1, expeditionSubsidyPenaltyDurationTurns);
            expeditionSubsidyPenaltyAttractionPerStack = Mathf.Max(0f, expeditionSubsidyPenaltyAttractionPerStack);
            talentRefreshGoldCost = Mathf.Max(0, talentRefreshGoldCost);
            talentRefreshCardCount = Mathf.Max(1, talentRefreshCardCount);
            royalInheritanceConfig ??= new RoyalInheritanceConfig();
            royalInheritanceConfig.Normalize();

            if (startingItems == null)
            {
                startingItems = Array.Empty<ItemAmount>();
            }
            else
            {
                for (var i = 0; i < startingItems.Length; i++)
                {
                    startingItems[i] = startingItems[i].Normalized();
                }
            }

            if (startingUnlockedTechnologies == null)
            {
                startingUnlockedTechnologies = Array.Empty<string>();
            }
            else
            {
                for (var i = 0; i < startingUnlockedTechnologies.Length; i++)
                {
                    startingUnlockedTechnologies[i] = string.IsNullOrWhiteSpace(startingUnlockedTechnologies[i])
                        ? string.Empty
                    : startingUnlockedTechnologies[i].Trim();
                }
            }

            if (startingUnlockedBuildingBlueprintIds == null)
            {
                startingUnlockedBuildingBlueprintIds = Array.Empty<string>();
            }
            else
            {
                for (var i = 0; i < startingUnlockedBuildingBlueprintIds.Length; i++)
                {
                    startingUnlockedBuildingBlueprintIds[i] = string.IsNullOrWhiteSpace(startingUnlockedBuildingBlueprintIds[i])
                        ? string.Empty
                        : startingUnlockedBuildingBlueprintIds[i].Trim();
                }
            }

            if (startingTalentSlots == null)
            {
                startingTalentSlots = Array.Empty<TalentSlotDefinition>();
            }
            else
            {
                for (var i = 0; i < startingTalentSlots.Length; i++)
                {
                    startingTalentSlots[i]?.Normalize();
                }
            }

            NormalizeStartingQuests();
        }

        [Button("重新初始化库存")]
        public void ReinitializeInventory()
        {
            CreateInventoryService();
        }

        public void SetBuildingCatalog(BuildingCatalog newBuildingCatalog)
        {
            buildingCatalog = newBuildingCatalog;
        }

        public void SetTechnologyCatalog(TechnologyCatalog newTechnologyCatalog)
        {
            technologyCatalog = newTechnologyCatalog;
            Technology?.SetCatalog(technologyCatalog);
        }

        public bool IsTechnologyUnlocked(string technologyId)
        {
            if (Technology != null)
            {
                return Technology.IsUnlocked(technologyId);
            }

            return !string.IsNullOrWhiteSpace(technologyId)
                   && unlockedTechnologies.Contains(technologyId.Trim());
        }

        public bool UnlockTechnology(string technologyId)
        {
            if (Technology != null)
            {
                var unlocked = Technology.UnlockForFree(technologyId);
                MirrorUnlockedTechnologiesFromService();
                SyncStartingResearchFromService();
                return unlocked;
            }

            if (string.IsNullOrWhiteSpace(technologyId))
            {
                return false;
            }

            return unlockedTechnologies.Add(technologyId.Trim());
        }

        public bool TryStartTechnologyResearch(string technologyId)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            var started = Technology != null && Technology.TryStartResearch(technologyId);
            if (started)
            {
                ClearMissingResearchWarning();
            }

            MirrorUnlockedTechnologiesFromService();
            SyncStartingResearchFromService();
            return started;
        }

        public bool TryStartTechnologyResearch(TechnologyDefinition definition)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            var started = Technology != null && Technology.TryStartResearch(definition);
            if (started)
            {
                ClearMissingResearchWarning();
            }

            MirrorUnlockedTechnologiesFromService();
            SyncStartingResearchFromService();
            return started;
        }

        public List<string> CaptureUnlockedTechnologies()
        {
            var source = Technology == null ? unlockedTechnologies : Technology.UnlockedTechnologyIds;
            var captured = new List<string>(source);
            captured.Sort(StringComparer.Ordinal);
            return captured;
        }

        public TechnologySaveData CaptureTechnologyData()
        {
            if (Technology != null)
            {
                return Technology.CaptureSaveData();
            }

            return new TechnologySaveData
            {
                CurrentResearchTechnologyId = startingResearchTechnology == null
                    ? string.Empty
                    : startingResearchTechnology.TechnologyId,
                CurrentResearchProgress = Mathf.Max(0, startingResearchProgress),
                UnlockedTechnologyIds = CaptureUnlockedTechnologies()
            };
        }

        public void RestoreUnlockedTechnologies(IReadOnlyList<string> technologies)
        {
            if (Technology != null)
            {
                if (technologies == null)
                {
                    RestoreTechnologyData(null, null);
                    return;
                }

                RestoreTechnologyData(
                    new TechnologySaveData
                    {
                        UnlockedTechnologyIds = new List<string>(technologies)
                    },
                    null);
                return;
            }

            unlockedTechnologies.Clear();
            if (technologies == null)
            {
                InitializeUnlockedTechnologies();
                return;
            }

            for (var i = 0; i < technologies.Count; i++)
            {
                UnlockTechnology(technologies[i]);
            }
        }

        public void RestoreTechnologyData(TechnologySaveData technologyData, IReadOnlyList<string> fallbackUnlockedTechnologies)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            if (technologyData == null)
            {
                technologyData = new TechnologySaveData
                {
                    CurrentResearchTechnologyId = startingResearchTechnology == null
                        ? string.Empty
                        : startingResearchTechnology.TechnologyId,
                    CurrentResearchProgress = Mathf.Max(0, startingResearchProgress),
                    UnlockedTechnologyIds = fallbackUnlockedTechnologies == null
                        ? new List<string>(startingUnlockedTechnologies ?? Array.Empty<string>())
                        : new List<string>(fallbackUnlockedTechnologies)
                };
            }

            Technology?.RestoreSaveData(technologyData, null);
            SyncStartingResearchFromService();
            MirrorUnlockedTechnologiesFromService();
        }

        public List<string> CaptureUnlockedBuildingBlueprints()
        {
            var captured = new List<string>(unlockedBuildingBlueprintIds);
            captured.Sort(StringComparer.Ordinal);
            return captured;
        }

        public void RestoreBuildingBlueprintData(IReadOnlyList<string> buildingBlueprintIds)
        {
            unlockedBuildingBlueprintIds.Clear();
            if (buildingBlueprintIds == null)
            {
                InitializeUnlockedBuildingBlueprints();
                NotifyBuildingBlueprintsChanged();
                return;
            }

            for (var i = 0; i < buildingBlueprintIds.Count; i++)
            {
                var buildingId = NormalizeBuildingBlueprintId(buildingBlueprintIds[i]);
                if (!string.IsNullOrWhiteSpace(buildingId))
                {
                    unlockedBuildingBlueprintIds.Add(buildingId);
                }
            }

            NotifyBuildingBlueprintsChanged();
        }

        public bool IsBuildingBlueprintUnlocked(string buildingId)
        {
            buildingId = NormalizeBuildingBlueprintId(buildingId);
            return !string.IsNullOrWhiteSpace(buildingId)
                   && unlockedBuildingBlueprintIds.Contains(buildingId);
        }

        public bool UnlockBuildingBlueprint(string buildingId)
        {
            buildingId = NormalizeBuildingBlueprintId(buildingId);
            if (string.IsNullOrWhiteSpace(buildingId) || !unlockedBuildingBlueprintIds.Add(buildingId))
            {
                return false;
            }

            NotifyBuildingBlueprintsChanged();
            return true;
        }

        public void SetExpeditionDestinationCatalog(ExpeditionDestinationCatalog newCatalog)
        {
            expeditionDestinationCatalog = newCatalog;
            Expeditions?.SetCatalog(expeditionDestinationCatalog);
        }

        public ExpeditionSaveData CaptureExpeditionData()
        {
            return Expeditions == null ? new ExpeditionSaveData() : Expeditions.CaptureSaveData();
        }

        public void RestoreExpeditionData(ExpeditionSaveData expeditionData)
        {
            if (Expeditions == null)
            {
                CreateExpeditionService(expeditionData);
                return;
            }

            Expeditions.RestoreSaveData(expeditionData);
            SyncExpeditionPopulationEmployment();
            NotifyExpeditionsChanged();
        }

        public IReadOnlyList<ExpeditionDestinationAvailability> GetExpeditionDestinations(bool includeUnavailable = true)
        {
            if (Expeditions == null)
            {
                CreateExpeditionService();
            }

            return Expeditions == null
                ? Array.Empty<ExpeditionDestinationAvailability>()
                : Expeditions.GetDestinationAvailabilities(includeUnavailable);
        }

        public bool TryStartExpedition(
            ExpeditionDestinationDefinition destination,
            int population,
            IEnumerable<ItemAmount> assignedSupplies,
            out ExpeditionStartResult result)
        {
            if (Expeditions == null)
            {
                CreateExpeditionService();
            }

            if (Expeditions == null)
            {
                result = new ExpeditionStartResult(
                    false,
                    ExpeditionStartFailureReason.InvalidDestination,
                    null,
                    0f,
                    "远征服务未初始化。");
                return false;
            }

            var started = Expeditions.TryStartExpedition(destination, population, assignedSupplies, out result);
            SyncExpeditionPopulationEmployment();
            if (started && result.Expedition != null)
            {
                AddExpeditionMessage(
                    GameEventCatalog.GE_远征出发,
                    $"远征出发：{destination.DisplayName}，预计第 {result.Expedition.ReturnTurn} 回合归来，成功率 {FormatPercent(result.SuccessChance)}。");
            }

            NotifyExpeditionsChanged();
            return started;
        }

        public bool TryClaimExpeditionRewards(string expeditionId, out ExpeditionClaimResult result)
        {
            if (Expeditions == null)
            {
                CreateExpeditionService();
            }

            if (Expeditions == null)
            {
                result = new ExpeditionClaimResult(
                    false,
                    ExpeditionClaimFailureReason.InvalidExpedition,
                    null,
                    "远征服务未初始化。");
                return false;
            }

            var claimed = Expeditions.TryClaimRewards(expeditionId, out result);
            if (claimed && result.Expedition != null)
            {
                AddExpeditionMessage(
                    GameEventCatalog.GE_远征奖励领取,
                    $"领取远征奖励：{FormatExpeditionDestinationName(result.Expedition)}。");
            }

            NotifyExpeditionsChanged();
            return claimed;
        }

        public void SetTalentCatalog(TalentCatalog newCatalog)
        {
            talentCatalog = newCatalog;
            Talents?.SetCatalog(talentCatalog);
        }

        public TalentSaveData CaptureTalentData()
        {
            return Talents == null ? new TalentSaveData() : Talents.CaptureSaveData();
        }

        public void RestoreTalentData(TalentSaveData talentData)
        {
            if (Talents == null)
            {
                CreateTalentService(talentData);
                return;
            }

            Talents.RestoreSaveData(talentData);
            NotifyTalentsChanged();
        }

        public bool TryRefreshTalents(out TalentRefreshResult result)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                result = new TalentRefreshResult(false, talentRefreshGoldCost, Array.Empty<TalentOfferState>(), "人才服务未初始化。");
                return false;
            }

            var refreshed = Talents.TryRefreshOffers(out result);
            if (refreshed)
            {
                AddTalentMessage(
                    GameEventCatalog.GE_人才刷新,
                    $"刷新人才：消耗 {result.CostGold} 金币，获得 {result.Offers.Count} 张候选卡。");
            }

            NotifyTalentsChanged();
            return refreshed;
        }

        public bool TryRecruitTalentOffer(string offerId, out TalentRecruitResult result)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                result = new TalentRecruitResult(false, null, null, "人才服务未初始化。");
                return false;
            }

            var recruited = Talents.TryRecruitOffer(offerId, out result);
            if (recruited && result.Talent != null)
            {
                AddTalentMessage(
                    GameEventCatalog.GE_人才招募,
                    $"招募人才：{result.Talent.DisplayName}。");
            }

            NotifyTalentsChanged();
            return recruited;
        }

        public bool TryAssignTalent(string talentInstanceId, string slotId, out TalentAssignResult result)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                result = new TalentAssignResult(false, null, null, "人才服务未初始化。");
                return false;
            }

            var assigned = Talents.TryAssignTalentToSlot(talentInstanceId, slotId, out result);
            if (assigned && result.Talent != null && result.Slot != null)
            {
                AddTalentMessage(
                    GameEventCatalog.GE_人才任命,
                    $"任命人才：{result.Talent.DisplayName} -> {result.Slot.DisplayName}。");
            }

            NotifyTalentsChanged();
            return assigned;
        }

        public bool TryUnassignTalentSlot(string slotId, out TalentAssignResult result)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                result = new TalentAssignResult(false, null, null, "人才服务未初始化。");
                return false;
            }

            var unassigned = Talents.TryUnassignSlot(slotId, out result);
            if (unassigned && result.Talent != null)
            {
                AddTalentMessage(
                    GameEventCatalog.GE_人才卸任,
                    $"卸任人才：{result.Talent.DisplayName}。");
            }

            NotifyTalentsChanged();
            return unassigned;
        }

        public bool TryUnassignTalent(string talentInstanceId, out TalentAssignResult result)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                result = new TalentAssignResult(false, null, null, "人才服务未初始化。");
                return false;
            }

            var unassigned = Talents.TryUnassignTalent(talentInstanceId, out result);
            if (unassigned && result.Talent != null)
            {
                AddTalentMessage(
                    GameEventCatalog.GE_人才卸任,
                    $"卸任人才：{result.Talent.DisplayName}。");
            }

            NotifyTalentsChanged();
            return unassigned;
        }

        public bool TryUpgradeTalent(string talentInstanceId, out TalentUpgradeResult result)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                result = new TalentUpgradeResult(false, null, 0, null, "人才服务未初始化。");
                return false;
            }

            var upgraded = Talents.TryUpgradeTalent(talentInstanceId, out result);
            if (upgraded && result.Talent != null)
            {
                AddTalentMessage(
                    GameEventCatalog.GE_人才升级,
                    $"人才升级：{result.Talent.DisplayName} {result.PreviousLevel}->{result.Talent.Level}。");
                AddTalentTraitTransitionMessages(result.TraitTransitions, CurrentTurn);
            }

            NotifyTalentsChanged();
            return upgraded;
        }

        public bool AddTalentExperience(string talentInstanceId, int amount)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            return Talents != null && Talents.AddTalentExperience(talentInstanceId, amount);
        }

        public void SetRoyalTraitCatalog(RoyalTraitCatalog newCatalog)
        {
            royalTraitCatalog = newCatalog;
            Inheritance?.SetTraitCatalog(royalTraitCatalog);
        }

        public RoyalInheritanceSaveData CaptureInheritanceData()
        {
            return Inheritance == null ? new RoyalInheritanceSaveData() : Inheritance.CaptureSaveData();
        }

        public void RestoreInheritanceData(RoyalInheritanceSaveData inheritanceData)
        {
            if (Inheritance == null)
            {
                CreateInheritanceService(inheritanceData);
                return;
            }

            Inheritance.RestoreSaveData(inheritanceData);
            NotifyInheritanceChanged();
        }

        public bool TryBirthPrince(string displayName, out RoyalCharacterState prince)
        {
            if (Inheritance == null)
            {
                CreateInheritanceService();
            }

            if (Inheritance == null)
            {
                prince = null;
                return false;
            }

            var effects = new List<RoyalEffectApplicationResult>();
            var born = Inheritance.TryBirthPrince(displayName, CurrentTurn, out prince, effects);
            if (born && prince != null)
            {
                AddInheritanceMessage(
                    GameEventCatalog.GE_王子出生,
                    $"王子出生：{prince.DisplayName}。");
                for (var i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    if (effect.HasMessage)
                    {
                        AddInheritanceMessage(
                            GameEventCatalog.GE_国王特性效果触发,
                            $"国王特性效果：{effect.Message}。");
                    }
                }
            }

            NotifyInheritanceChanged();
            return born;
        }

        public bool TryAbdicateCurrentKing(out RoyalSuccessionResult succession)
        {
            if (Inheritance == null)
            {
                CreateInheritanceService();
            }

            if (Inheritance == null)
            {
                succession = new RoyalSuccessionResult(RoyalSuccessionReason.None, null, null, false);
                return false;
            }

            var abdicated = Inheritance.TryAbdicateCurrentKing(CurrentTurn, out succession);
            if (abdicated)
            {
                AddInheritanceSuccessionMessage(succession, CurrentTurn);
            }

            NotifyInheritanceChanged();
            return abdicated;
        }

        public bool TryAddRoyalAcquiredTrait(string characterId, RoyalTraitDefinition trait)
        {
            if (Inheritance == null)
            {
                CreateInheritanceService();
            }

            if (Inheritance == null || trait == null)
            {
                return false;
            }

            var added = Inheritance.TryAddAcquiredTrait(characterId, trait, CurrentTurn);
            if (added)
            {
                var character = Inheritance.FindCharacter(characterId);
                AddInheritanceMessage(
                    GameEventCatalog.GE_王族后天特性获得,
                    $"后天特性获得：{(character == null ? characterId : character.DisplayName)} 获得 {trait.TraitName}。");
            }

            NotifyInheritanceChanged();
            return added;
        }

        internal void RestoreCurrentTurn(int currentTurn)
        {
            if (Turn == null)
            {
                CreateTurnService();
            }

            Turn.SetCurrentTurn(currentTurn);
            startingTurn = Turn.CurrentTurn;
        }

        internal void RestoreDynastyData(string dynastyName, string stageName)
        {
            RestoreDynastyData(dynastyName, stageName, -1);
        }

        internal void RestoreDynastyData(string dynastyName, string stageName, int basePopulation)
        {
            if (Dynasty == null)
            {
                CreateDynastyService();
            }

            Dynasty.SetDynastyName(dynastyName);
            startingDynastyName = Dynasty.DynastyName;
            if (basePopulation >= 0)
            {
                Dynasty.SetBasePopulation(basePopulation);
                startingPopulation = Dynasty.BasePopulation;
            }

            if (TryParseDynastyStage(stageName, out var restoredStage))
            {
                Dynasty.SetStage(restoredStage);
            }

            SyncExpeditionPopulationEmployment();
        }

        private static bool TryParseDynastyStage(string stageName, out DynastyStage stage)
        {
            if (string.IsNullOrWhiteSpace(stageName))
            {
                stage = DynastyStage.营地;
                return false;
            }

            return Enum.TryParse(stageName.Trim(), out stage);
        }

        [Button("重新初始化任务")]
        public void ReinitializeQuests()
        {
            CreateQuestService();
        }

        public QuestSaveData CaptureQuestData()
        {
            var saveData = new QuestSaveData
            {
                Quests = new List<QuestStateSaveData>()
            };

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    continue;
                }

                var questData = new QuestStateSaveData
                {
                    QuestId = quest.QuestId,
                    Status = quest.Status,
                    StartedTurn = quest.StartedTurn,
                    DeadlineTurn = Mathf.Max(quest.StartedTurn, quest.DeadlineTurn),
                    SubmittedResources = new List<QuestResourceSubmissionSaveData>()
                };

                var resourceProgresses = quest.ResourceProgresses;
                for (var j = 0; j < resourceProgresses.Count; j++)
                {
                    var progress = resourceProgresses[j];
                    if (progress == null
                        || string.IsNullOrWhiteSpace(progress.ItemId)
                        || progress.SubmittedAmount <= 0)
                    {
                        continue;
                    }

                    questData.SubmittedResources.Add(
                        new QuestResourceSubmissionSaveData
                        {
                            ItemId = progress.ItemId,
                            SubmittedAmount = Mathf.Max(0, progress.SubmittedAmount)
                        });
                }

                saveData.Quests.Add(questData);
            }

            saveData.Validate();
            return saveData;
        }

        public void RestoreQuestData(QuestSaveData questData)
        {
            questData?.Validate();
            CreateQuestService(questData, false);
        }

        internal void BeginQuestRuntimeRestore()
        {
            suppressQuestRuntimeMessages = true;
        }

        internal void EndQuestRuntimeRestore()
        {
            suppressQuestRuntimeMessages = false;
            RefreshAllQuestProgress(false);
        }

        public bool TrySubmitQuestResources(string questId)
        {
            var quest = FindQuest(questId);
            return TrySubmitQuestResources(quest);
        }

        public bool TrySubmitQuestResources(GameQuestState quest)
        {
            if (quest == null || !quest.IsActive || !quest.IsResourceSubmission)
            {
                return false;
            }

            if (Inventory == null)
            {
                CreateInventoryService();
            }

            if (Inventory == null)
            {
                return false;
            }

            var submittedLines = new List<string>();
            var totalSubmitted = 0;
            suppressQuestInventoryRefresh = true;
            try
            {
                var resourceProgresses = quest.ResourceProgresses;
                for (var i = 0; i < resourceProgresses.Count; i++)
                {
                    var progress = resourceProgresses[i];
                    if (progress == null || progress.RemainingAmount <= 0)
                    {
                        continue;
                    }

                    var available = Inventory.GetQuantity(progress.ItemId);
                    var submitAmount = Mathf.Min(progress.RemainingAmount, available);
                    if (submitAmount <= 0)
                    {
                        continue;
                    }

                    var removed = Inventory.RemoveItem(progress.ItemId, submitAmount);
                    if (removed <= 0)
                    {
                        continue;
                    }

                    progress.SubmittedAmount = Mathf.Min(progress.RequiredAmount, progress.SubmittedAmount + removed);
                    totalSubmitted += removed;
                    submittedLines.Add($"{progress.DisplayName} x{removed}");
                }
            }
            finally
            {
                suppressQuestInventoryRefresh = false;
            }

            if (totalSubmitted <= 0)
            {
                RefreshQuestProgress(quest, false);
                NotifyQuestsChanged();
                return false;
            }

            AddQuestMessage(
                GameEventCatalog.GE_任务提交资源,
                $"任务提交资源：{quest.Definition.DisplayName}（{string.Join("，", submittedLines)}）");
            RefreshQuestProgress(quest, true);
            NotifyQuestsChanged();
            return true;
        }

        private void CreateQuestService()
        {
            CreateQuestService(null, false);
        }

        private void CreateQuestService(QuestSaveData saveData, bool emitMessages)
        {
            UnsubscribeQuestRuntimeServices();
            questsInitialized = true;
            quests.Clear();
            NormalizeStartingQuests();

            var savedQuests = BuildSavedQuestLookup(saveData);
            var usedQuestIds = new HashSet<string>(StringComparer.Ordinal);
            var definitions = startingQuests ?? Array.Empty<GameQuestDefinition>();
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                definition.Normalize();
                if (!definition.IsValid)
                {
                    Debug.LogWarning($"任务配置无效，已跳过：{definition.DisplayName}", this);
                    continue;
                }

                if (!usedQuestIds.Add(definition.QuestId))
                {
                    Debug.LogWarning($"任务ID重复，已跳过后续配置：{definition.QuestId}", this);
                    continue;
                }

                var quest = new GameQuestState(definition, CurrentTurn);
                if (savedQuests.TryGetValue(definition.QuestId, out var savedQuest))
                {
                    ApplyQuestSaveData(quest, savedQuest);
                }

                RebuildQuestResourceProgresses(quest, savedQuest);
                RefreshQuestProgress(quest, emitMessages);
                quests.Add(quest);
            }

            SubscribeQuestRuntimeServices();
            NotifyQuestsChanged();
        }

        private void NormalizeStartingQuests()
        {
            if (startingQuests == null)
            {
                startingQuests = Array.Empty<GameQuestDefinition>();
                return;
            }

            for (var i = 0; i < startingQuests.Length; i++)
            {
                startingQuests[i]?.Normalize();
            }
        }

        private static Dictionary<string, QuestStateSaveData> BuildSavedQuestLookup(QuestSaveData saveData)
        {
            var result = new Dictionary<string, QuestStateSaveData>(StringComparer.Ordinal);
            if (saveData == null || saveData.Quests == null)
            {
                return result;
            }

            for (var i = 0; i < saveData.Quests.Count; i++)
            {
                var questData = saveData.Quests[i];
                if (questData == null)
                {
                    continue;
                }

                questData.Validate();
                if (!string.IsNullOrWhiteSpace(questData.QuestId))
                {
                    result[questData.QuestId] = questData;
                }
            }

            return result;
        }

        private static void ApplyQuestSaveData(GameQuestState quest, QuestStateSaveData saveData)
        {
            if (quest == null || saveData == null)
            {
                return;
            }

            saveData.Validate();
            quest.Status = saveData.Status;
            quest.StartedTurn = Mathf.Max(1, saveData.StartedTurn);
            quest.DeadlineTurn = Mathf.Max(quest.StartedTurn, saveData.DeadlineTurn);
        }

        private void RebuildQuestResourceProgresses(GameQuestState quest, QuestStateSaveData saveData)
        {
            if (quest == null)
            {
                return;
            }

            quest.ClearResourceProgresses();
            if (!quest.IsResourceSubmission || quest.Definition == null)
            {
                return;
            }

            var requiredAmounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var orderedItemIds = new List<string>();
            var requiredResources = quest.Definition.RequiredResources;
            for (var i = 0; i < requiredResources.Count; i++)
            {
                var requirement = requiredResources[i].Normalized();
                if (!requirement.IsValid)
                {
                    continue;
                }

                var itemId = requirement.ItemId.Trim();
                if (!requiredAmounts.ContainsKey(itemId))
                {
                    requiredAmounts.Add(itemId, 0);
                    orderedItemIds.Add(itemId);
                }

                requiredAmounts[itemId] += requirement.Amount;
            }

            var submittedAmounts = BuildSubmittedResourceLookup(saveData);
            for (var i = 0; i < orderedItemIds.Count; i++)
            {
                var itemId = orderedItemIds[i];
                var requiredAmount = Mathf.Max(0, requiredAmounts[itemId]);
                submittedAmounts.TryGetValue(itemId, out var submittedAmount);

                quest.AddResourceProgress(
                    new GameQuestResourceProgress
                    {
                        ItemId = itemId,
                        ItemDefinition = FindItemDefinition(itemId),
                        RequiredAmount = requiredAmount,
                        SubmittedAmount = Mathf.Clamp(submittedAmount, 0, requiredAmount),
                        InventoryAmount = Inventory == null ? 0 : Inventory.GetQuantity(itemId)
                    });
            }
        }

        private static Dictionary<string, int> BuildSubmittedResourceLookup(QuestStateSaveData saveData)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (saveData == null || saveData.SubmittedResources == null)
            {
                return result;
            }

            for (var i = 0; i < saveData.SubmittedResources.Count; i++)
            {
                var submittedResource = saveData.SubmittedResources[i];
                if (submittedResource == null)
                {
                    continue;
                }

                submittedResource.Validate();
                if (string.IsNullOrWhiteSpace(submittedResource.ItemId))
                {
                    continue;
                }

                result[submittedResource.ItemId] = Mathf.Max(0, submittedResource.SubmittedAmount);
            }

            return result;
        }

        private GameQuestState FindQuest(string questId)
        {
            questId = GameQuestDefinition.NormalizeQuestId(questId);
            if (string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest != null && string.Equals(quest.QuestId, questId, StringComparison.Ordinal))
                {
                    return quest;
                }
            }

            return null;
        }

        private void RefreshAllQuestProgress(bool emitMessages)
        {
            var shouldEmitMessages = emitMessages && !suppressQuestRuntimeMessages;
            for (var i = 0; i < quests.Count; i++)
            {
                RefreshQuestProgress(quests[i], shouldEmitMessages);
            }

            NotifyQuestsChanged();
        }

        private void RefreshQuestProgress(GameQuestState quest, bool emitMessages)
        {
            if (quest == null || quest.Definition == null)
            {
                return;
            }

            switch (quest.Definition.ObjectiveType)
            {
                case QuestObjectiveType.BuildBuildings:
                    RefreshBuildQuestProgress(quest);
                    break;
                case QuestObjectiveType.SubmitResources:
                    RefreshResourceQuestProgress(quest);
                    break;
            }

            if (!quest.IsActive)
            {
                return;
            }

            if (IsQuestSatisfied(quest))
            {
                CompleteQuest(quest, emitMessages);
                return;
            }

            if (CurrentTurn > quest.DeadlineTurn)
            {
                FailQuest(quest, emitMessages);
            }
        }

        private void RefreshBuildQuestProgress(GameQuestState quest)
        {
            var definition = quest.Definition;
            quest.TargetAmount = Mathf.Max(1, definition.TargetBuildingCount);
            quest.CurrentAmount = Mathf.Clamp(CountBuildings(definition.TargetBuildingId), 0, quest.TargetAmount);
            quest.TargetDisplayName = definition.TargetBuildingDisplayName;
            quest.Icon = definition.TargetBuildingIcon;
        }

        private void RefreshResourceQuestProgress(GameQuestState quest)
        {
            var totalRequired = 0;
            var totalSubmitted = 0;
            var firstIcon = (Sprite)null;
            var firstDisplayName = string.Empty;

            var resources = quest.ResourceProgresses;
            for (var i = 0; i < resources.Count; i++)
            {
                var progress = resources[i];
                if (progress == null)
                {
                    continue;
                }

                progress.InventoryAmount = Inventory == null ? 0 : Inventory.GetQuantity(progress.ItemId);
                totalRequired += Mathf.Max(0, progress.RequiredAmount);
                totalSubmitted += Mathf.Clamp(progress.SubmittedAmount, 0, progress.RequiredAmount);
                if (firstIcon == null && progress.ItemDefinition != null)
                {
                    firstIcon = progress.ItemDefinition.Icon;
                }

                if (string.IsNullOrWhiteSpace(firstDisplayName))
                {
                    firstDisplayName = progress.DisplayName;
                }
            }

            quest.TargetAmount = Mathf.Max(0, totalRequired);
            quest.CurrentAmount = Mathf.Clamp(totalSubmitted, 0, quest.TargetAmount);
            quest.TargetDisplayName = resources.Count == 1 ? firstDisplayName : "多种资源";
            quest.Icon = firstIcon;
        }

        private bool IsQuestSatisfied(GameQuestState quest)
        {
            if (quest == null || quest.Definition == null)
            {
                return false;
            }

            if (quest.Definition.ObjectiveType == QuestObjectiveType.SubmitResources)
            {
                var resources = quest.ResourceProgresses;
                if (resources.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < resources.Count; i++)
                {
                    if (resources[i] == null || !resources[i].IsComplete)
                    {
                        return false;
                    }
                }

                return true;
            }

            return quest.TargetAmount > 0 && quest.CurrentAmount >= quest.TargetAmount;
        }

        private void CompleteQuest(GameQuestState quest, bool emitMessage)
        {
            if (quest == null || !quest.IsActive)
            {
                return;
            }

            quest.Status = QuestStatus.Completed;
            if (emitMessage)
            {
                AddQuestMessage(
                    GameEventCatalog.GE_任务完成,
                    $"任务完成：{quest.Definition.DisplayName}");
            }
        }

        private void FailQuest(GameQuestState quest, bool emitMessage)
        {
            if (quest == null || !quest.IsActive)
            {
                return;
            }

            quest.Status = QuestStatus.Failed;
            if (emitMessage)
            {
                AddQuestMessage(
                    GameEventCatalog.GE_任务失败,
                    $"任务失败：{quest.Definition.DisplayName}");
            }
        }

        private int CountBuildings(string buildingId)
        {
            if (Buildings == null || string.IsNullOrWhiteSpace(buildingId))
            {
                return 0;
            }

            var count = 0;
            var buildings = Buildings.Buildings;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null
                    || !building.isActiveAndEnabled
                    || building.IsDemolishing
                    || !building.HasDefinition)
                {
                    continue;
                }

                if (string.Equals(building.Definition.BuildingId, buildingId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private ItemDefinition FindItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            var catalog = Inventory == null ? itemCatalog : Inventory.ItemCatalog;
            return catalog != null && catalog.TryGetDefinition(itemId, out var definition) ? definition : null;
        }

        private void AddQuestMessage(string eventTypeId, string message)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGame(eventTypeId, message, CurrentTurn));
        }

        private void RefreshQuestSubscriptions()
        {
            if (!questsInitialized)
            {
                return;
            }

            SubscribeQuestRuntimeServices();
        }

        private void SubscribeQuestRuntimeServices()
        {
            if (!questsInitialized)
            {
                return;
            }

            if (subscribedQuestInventory != Inventory)
            {
                if (subscribedQuestInventory != null)
                {
                    subscribedQuestInventory.InventoryChanged -= HandleQuestInventoryChanged;
                }

                subscribedQuestInventory = Inventory;
                if (subscribedQuestInventory != null)
                {
                    subscribedQuestInventory.InventoryChanged += HandleQuestInventoryChanged;
                }
            }

            if (subscribedQuestBuildings != Buildings)
            {
                if (subscribedQuestBuildings != null)
                {
                    subscribedQuestBuildings.BuildingsChanged -= HandleQuestBuildingsChanged;
                }

                subscribedQuestBuildings = Buildings;
                if (subscribedQuestBuildings != null)
                {
                    subscribedQuestBuildings.BuildingsChanged += HandleQuestBuildingsChanged;
                }
            }

            if (subscribedQuestTurn != Turn)
            {
                if (subscribedQuestTurn != null)
                {
                    subscribedQuestTurn.TurnAdvanced -= HandleQuestTurnAdvanced;
                }

                subscribedQuestTurn = Turn;
                if (subscribedQuestTurn != null)
                {
                    subscribedQuestTurn.TurnAdvanced += HandleQuestTurnAdvanced;
                }
            }
        }

        private void UnsubscribeQuestRuntimeServices()
        {
            if (subscribedQuestInventory != null)
            {
                subscribedQuestInventory.InventoryChanged -= HandleQuestInventoryChanged;
                subscribedQuestInventory = null;
            }

            if (subscribedQuestBuildings != null)
            {
                subscribedQuestBuildings.BuildingsChanged -= HandleQuestBuildingsChanged;
                subscribedQuestBuildings = null;
            }

            if (subscribedQuestTurn != null)
            {
                subscribedQuestTurn.TurnAdvanced -= HandleQuestTurnAdvanced;
                subscribedQuestTurn = null;
            }
        }

        private void HandleQuestInventoryChanged(InventoryService changedInventory)
        {
            if (suppressQuestInventoryRefresh)
            {
                return;
            }

            RefreshAllQuestProgress(false);
        }

        private void HandleQuestBuildingsChanged(BuildingService changedBuildings)
        {
            RefreshAllQuestProgress(true);
        }

        private void HandleQuestTurnAdvanced(TurnService changedTurn, TurnAdvanceSummary summary)
        {
            RefreshAllQuestProgress(true);
        }

        private void NotifyQuestsChanged()
        {
            QuestsChanged?.Invoke(this);
        }

        private void CreateExpeditionService(ExpeditionSaveData saveData = null)
        {
            UnsubscribeExpeditionService();
            Expeditions = new ExpeditionService(
                this,
                expeditionDestinationCatalog,
                expeditionSubsidyGoldItemDefinition,
                saveData);
            Expeditions.StateChanged += HandleExpeditionsChanged;
            SyncExpeditionPopulationEmployment();
            NotifyExpeditionsChanged();
        }

        private void UnsubscribeExpeditionService()
        {
            if (Expeditions != null)
            {
                Expeditions.StateChanged -= HandleExpeditionsChanged;
            }
        }

        private void HandleExpeditionsChanged(ExpeditionService changedExpeditions)
        {
            SyncExpeditionPopulationEmployment();
            NotifyExpeditionsChanged();
        }

        private void NotifyExpeditionsChanged()
        {
            ExpeditionsChanged?.Invoke(this);
        }

        private void SyncExpeditionPopulationEmployment()
        {
            if (Dynasty == null)
            {
                return;
            }

            Dynasty.SetExternalEmployedPopulation(
                "expedition",
                Expeditions == null ? 0 : Expeditions.ActiveAssignedPopulation);
        }

        private void SettleExpeditionsForTurn(int turnNumber)
        {
            if (Expeditions == null)
            {
                CreateExpeditionService();
            }

            if (Expeditions == null)
            {
                return;
            }

            var results = Expeditions.SettleDueExpeditions(turnNumber);
            if (results == null || results.Count == 0)
            {
                SyncExpeditionPopulationEmployment();
                NotifyExpeditionsChanged();
                return;
            }

            for (var i = 0; i < results.Count; i++)
            {
                AddExpeditionSettlementMessage(results[i], turnNumber);
            }

            SyncExpeditionPopulationEmployment();
            NotifyExpeditionsChanged();
        }

        private void AddExpeditionSettlementMessage(ExpeditionSettlementResult result, int turnNumber)
        {
            var expedition = result.Expedition;
            if (expedition == null)
            {
                return;
            }

            if (result.Succeeded)
            {
                var message = result.RewardsPending
                    ? $"远征归来：{FormatExpeditionDestinationName(expedition)} 成功，人口已返还，仓库空间不足，奖励待领取。"
                    : $"远征归来：{FormatExpeditionDestinationName(expedition)} 成功，人口已返还。";
                AddExpeditionMessage(
                    result.RewardsPending
                        ? GameEventCatalog.GE_远征奖励待领取
                        : GameEventCatalog.GE_远征成功,
                    message,
                    turnNumber);
                return;
            }

            var failureMessage = result.SubsidyMissing > 0
                ? $"远征失败：{FormatExpeditionDestinationName(expedition)}，损失人口 {expedition.AssignedPopulation}，补贴需 {result.SubsidyRequired} 金币，已支付 {result.SubsidyPaid}，缺口 {result.SubsidyMissing}，触发全局惩罚 {result.PenaltyStacksApplied} 层。"
                : $"远征失败：{FormatExpeditionDestinationName(expedition)}，损失人口 {expedition.AssignedPopulation}，已支付补贴 {result.SubsidyPaid} 金币。";
            AddExpeditionMessage(
                result.SubsidyMissing > 0
                    ? GameEventCatalog.GE_远征补贴不足
                    : GameEventCatalog.GE_远征失败,
                failureMessage,
                turnNumber);

            if (result.SubsidyMissing > 0)
            {
                Expeditions.ExtendSubsidyPenalty(turnNumber, expeditionSubsidyPenaltyDurationTurns);
            }
        }

        private void AddExpeditionMessage(string eventTypeId, string message)
        {
            AddExpeditionMessage(eventTypeId, message, CurrentTurn);
        }

        private void AddExpeditionMessage(string eventTypeId, string message, int turnNumber)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGame(eventTypeId, message, turnNumber));
        }

        private void CreateTalentService(TalentSaveData saveData = null)
        {
            UnsubscribeTalentService();
            Talents = new TalentService(
                this,
                talentCatalog,
                talentGoldItemDefinition,
                startingTalentSlots,
                talentRefreshGoldCost,
                talentRefreshCardCount,
                saveData);
            Talents.StateChanged += HandleTalentsChanged;
            NotifyTalentsChanged();
        }

        private void UnsubscribeTalentService()
        {
            if (Talents != null)
            {
                Talents.StateChanged -= HandleTalentsChanged;
            }
        }

        private void HandleTalentsChanged(TalentService changedTalents)
        {
            NotifyTalentsChanged();
        }

        private void NotifyTalentsChanged()
        {
            TalentsChanged?.Invoke(this);
        }

        private void SettleTalentsForTurn(int turnNumber)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                return;
            }

            var result = Talents.SettleTurn(turnNumber);
            AddTalentSettlementMessages(result);
            NotifyTalentsChanged();
        }

        private void AddTalentSettlementMessages(TalentTurnSettlementResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.HasSalary)
            {
                var eventType = result.HasMissingSalary
                    ? GameEventCatalog.GE_人才薪资不足
                    : GameEventCatalog.GE_人才薪资支付;
                var message = result.HasMissingSalary
                    ? $"人才薪资不足：需要 {result.SalaryRequired} 金币，已支付 {result.SalaryPaid}，缺口 {result.SalaryMissing}。"
                    : $"人才薪资支付：{result.SalaryPaid} 金币。";
                AddTalentMessage(eventType, message, result.TurnNumber);
            }

            AddTalentTraitTransitionMessages(result.TraitTransitions, result.TurnNumber);

            var effects = result.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect.HasMessage)
                {
                    continue;
                }

                AddTalentMessage(
                    GameEventCatalog.GE_人才效果触发,
                    $"人才效果：{effect.Message}。",
                    result.TurnNumber);
            }
        }

        private void AddTalentTraitTransitionMessages(
            IReadOnlyList<TalentHiddenTraitTransition> transitions,
            int turnNumber)
        {
            if (transitions == null)
            {
                return;
            }

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (transition.Talent == null || transition.Trait == null || transition.Trait.Definition == null)
                {
                    continue;
                }

                if (transition.NewState == TalentHiddenTraitState.Discovered)
                {
                    AddTalentMessage(
                        GameEventCatalog.GE_人才特性发现,
                        $"人才特性发现：{transition.Talent.DisplayName} 的 {transition.Trait.Definition.DisplayName}。",
                        turnNumber);
                }
                else if (transition.NewState == TalentHiddenTraitState.Active)
                {
                    AddTalentMessage(
                        GameEventCatalog.GE_人才特性激活,
                        $"人才特性激活：{transition.Talent.DisplayName} 的 {transition.Trait.Definition.DisplayName}。",
                        turnNumber);
                }
            }
        }

        private void AddTalentMessage(string eventTypeId, string message)
        {
            AddTalentMessage(eventTypeId, message, CurrentTurn);
        }

        private void AddTalentMessage(string eventTypeId, string message, int turnNumber)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGame(eventTypeId, message, turnNumber));
        }

        private void CreateInheritanceService(RoyalInheritanceSaveData saveData = null)
        {
            UnsubscribeInheritanceService();
            royalInheritanceConfig ??= new RoyalInheritanceConfig();
            royalInheritanceConfig.Normalize();
            Inheritance = new RoyalInheritanceService(
                this,
                royalTraitCatalog,
                royalInheritanceConfig,
                saveData);
            Inheritance.StateChanged += HandleInheritanceChanged;
            NotifyInheritanceChanged();
        }

        private void UnsubscribeInheritanceService()
        {
            if (Inheritance != null)
            {
                Inheritance.StateChanged -= HandleInheritanceChanged;
            }
        }

        private void HandleInheritanceChanged(RoyalInheritanceService changedInheritance)
        {
            NotifyInheritanceChanged();
        }

        private void NotifyInheritanceChanged()
        {
            InheritanceChanged?.Invoke(this);
        }

        private void SettleInheritanceForTurn(int turnNumber)
        {
            if (Inheritance == null)
            {
                CreateInheritanceService();
            }

            if (Inheritance == null)
            {
                return;
            }

            var result = Inheritance.SettleTurn(turnNumber);
            AddInheritanceSettlementMessages(result);
            NotifyInheritanceChanged();
        }

        private void AddInheritanceSettlementMessages(RoyalInheritanceTurnResult result)
        {
            if (result == null)
            {
                return;
            }

            var bornChildren = result.BornChildren;
            for (var i = 0; i < bornChildren.Count; i++)
            {
                var child = bornChildren[i];
                if (child != null)
                {
                    AddInheritanceMessage(
                        GameEventCatalog.GE_王子出生,
                        $"王子出生：{child.DisplayName}。",
                        result.TurnNumber);
                }
            }

            var transitions = result.TraitTransitions;
            for (var i = 0; i < transitions.Count; i++)
            {
                AddInheritanceTraitTransitionMessage(transitions[i], result.TurnNumber);
            }

            var effects = result.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect.HasMessage)
                {
                    continue;
                }

                AddInheritanceMessage(
                    GameEventCatalog.GE_国王特性效果触发,
                    $"国王特性效果：{effect.Message}。",
                    result.TurnNumber);
            }

            if (result.LifetimeWarningIssued && result.Succession.PreviousKing == null)
            {
                var king = Inheritance == null ? null : Inheritance.CurrentKing;
                if (king != null)
                {
                    AddInheritanceLifetimeWarning(king, result.TurnNumber);
                }
            }

            AddInheritanceSuccessionMessage(result.Succession, result.TurnNumber);
        }

        private void AddInheritanceTraitTransitionMessage(RoyalTraitTransition transition, int turnNumber)
        {
            if (transition.Character == null || transition.Trait == null || transition.Trait.Definition == null)
            {
                return;
            }

            if (transition.NewState == RoyalTraitState.Discovered)
            {
                AddInheritanceMessage(
                    GameEventCatalog.GE_王族特性显现,
                    $"王族特性显现：{transition.Character.DisplayName} 的 {transition.Trait.Definition.TraitName}。",
                    turnNumber);
            }
            else if (transition.NewState == RoyalTraitState.Active)
            {
                AddInheritanceMessage(
                    GameEventCatalog.GE_王族特性激活,
                    $"王族特性激活：{transition.Character.DisplayName} 的 {transition.Trait.Definition.TraitName}。",
                    turnNumber);
            }
        }

        private void AddInheritanceLifetimeWarning(RoyalCharacterState king, int turnNumber)
        {
            if (king == null)
            {
                return;
            }

            AddInheritanceMessage(
                GameEventCatalog.GE_国王寿命预警,
                $"寿命预警：{king.DisplayName} 预计还剩 {king.RemainingLifespan} 回合。",
                turnNumber);
        }

        private void AddInheritanceSuccessionMessage(RoyalSuccessionResult succession, int turnNumber)
        {
            if (!succession.Occurred)
            {
                return;
            }

            if (succession.Crisis || succession.NewKing == null)
            {
                AddInheritanceMessage(
                    GameEventCatalog.GE_王朝继承危机,
                    "王朝继承危机：没有可继承王位的继承人。",
                    turnNumber);
                return;
            }

            var reason = succession.Reason == RoyalSuccessionReason.Abdication ? "退位" : "死亡";
            AddInheritanceMessage(
                GameEventCatalog.GE_王位继承,
                $"王位继承：{succession.PreviousKing?.DisplayName ?? "前任国王"} 因{reason}离位，{succession.NewKing.DisplayName} 登基。",
                turnNumber);
        }

        private void AddInheritanceMessage(string eventTypeId, string message)
        {
            AddInheritanceMessage(eventTypeId, message, CurrentTurn);
        }

        private void AddInheritanceMessage(string eventTypeId, string message, int turnNumber)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGame(eventTypeId, message, turnNumber));
        }

        private void InitializeUnlockedBuildingBlueprints()
        {
            unlockedBuildingBlueprintIds.Clear();
            if (startingUnlockedBuildingBlueprintIds == null)
            {
                return;
            }

            for (var i = 0; i < startingUnlockedBuildingBlueprintIds.Length; i++)
            {
                var buildingId = NormalizeBuildingBlueprintId(startingUnlockedBuildingBlueprintIds[i]);
                if (!string.IsNullOrWhiteSpace(buildingId))
                {
                    unlockedBuildingBlueprintIds.Add(buildingId);
                }
            }
        }

        private void NotifyBuildingBlueprintsChanged()
        {
            BuildingBlueprintsChanged?.Invoke(this);
        }

        private static string NormalizeBuildingBlueprintId(string buildingId)
        {
            return string.IsNullOrWhiteSpace(buildingId) ? string.Empty : buildingId.Trim();
        }

        private static string FormatExpeditionDestinationName(ExpeditionState expedition)
        {
            if (expedition == null)
            {
                return "未知目的地";
            }

            if (expedition.Definition != null)
            {
                return expedition.Definition.DisplayName;
            }

            return string.IsNullOrWhiteSpace(expedition.DestinationId) ? "未知目的地" : expedition.DestinationId;
        }

        private static string FormatPercent(float value)
        {
            return $"{Mathf.Clamp01(value) * 100f:0.#}%";
        }

        private void InitializeUnlockedTechnologies()
        {
            unlockedTechnologies.Clear();
            if (startingUnlockedTechnologies == null)
            {
                return;
            }

            for (var i = 0; i < startingUnlockedTechnologies.Length; i++)
            {
                UnlockTechnology(startingUnlockedTechnologies[i]);
            }
        }

        private void CreateTechnologyService()
        {
            if (Technology != null)
            {
                Technology.CurrentResearchChanged -= HandleCurrentResearchChanged;
            }

            Technology = new TechnologyService(
                technologyCatalog,
                startingUnlockedTechnologies,
                startingResearchTechnology == null ? null : startingResearchTechnology.TechnologyId,
                startingResearchProgress);
            Technology.CurrentResearchChanged += HandleCurrentResearchChanged;
            MirrorUnlockedTechnologiesFromService();
            SyncStartingResearchFromService();
        }

        private void HandleCurrentResearchChanged(TechnologyService changedTechnology)
        {
            if (changedTechnology != null && changedTechnology.HasCurrentResearch)
            {
                ClearMissingResearchWarning();
            }
        }

        private void MirrorUnlockedTechnologiesFromService()
        {
            if (Technology == null)
            {
                return;
            }

            unlockedTechnologies.Clear();
            foreach (var technologyId in Technology.UnlockedTechnologyIds)
            {
                if (!string.IsNullOrWhiteSpace(technologyId))
                {
                    unlockedTechnologies.Add(technologyId.Trim());
                }
            }
        }

        private void SyncStartingResearchFromService()
        {
            if (Technology == null)
            {
                startingResearchProgress = Mathf.Max(0, startingResearchProgress);
                return;
            }

            startingResearchTechnology = Technology.CurrentResearchDefinition;
            startingResearchProgress = Technology.CurrentResearchProgress;
        }

        public void RegisterBuilding(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            if (Inventory == null)
            {
                CreateInventoryService();
            }

            if (Turn == null)
            {
                CreateTurnService();
            }

            if (Dynasty == null)
            {
                CreateDynastyService();
            }

            if (Buildings == null)
            {
                CreateBuildingService();
            }

            if (Events == null)
            {
                CreateGameEventService();
            }

            ResolveBuildingSelectionController();

            if (!building.HasDefinition)
            {
                Debug.LogWarning($"Cannot register building '{building.name}' because it has no valid BuildingDefinition data.", building);
                return;
            }

            Buildings.RegisterBuilding(building);
            building.Initialize();
            RefreshInventorySlotCapacity();
        }

        public void UnregisterBuilding(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            Dynasty?.UnregisterPalace(building);
            Dynasty?.RemovePopulationContribution(building);
            Buildings?.UnregisterBuilding(building);
            RefreshInventorySlotCapacity();

            if (building.IsRegistered)
            {
                building.NotifyUnregisteredFromGame(this);
            }
        }

        private void CreateInventoryService()
        {
            Inventory = new InventoryService(itemCatalog, CalculateTargetInventorySlotCount(), startingItems);
            RefreshQuestSubscriptions();
        }

        private void RefreshInventorySlotCapacity()
        {
            if (Inventory == null)
            {
                return;
            }

            var targetSlotCount = CalculateTargetInventorySlotCount();
            var currentSlotCount = Inventory.SlotCount;
            if (targetSlotCount == currentSlotCount)
            {
                return;
            }

            if (targetSlotCount < currentSlotCount && !CanShrinkInventoryTo(targetSlotCount))
            {
                return;
            }

            Inventory.SetSlotCount(targetSlotCount);
        }

        private int CalculateTargetInventorySlotCount()
        {
            return Mathf.Max(0, inventorySlotCount) + CalculateBuildingInventorySlotCapacity();
        }

        private int CalculateBuildingInventorySlotCapacity()
        {
            var buildings = Buildings == null ? null : Buildings.Buildings;
            if (buildings == null || buildings.Count == 0)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || !building.isActiveAndEnabled || building.IsDemolishing)
                {
                    continue;
                }

                inventorySlotCapacityModules.Clear();
                building.GetModules(inventorySlotCapacityModules);
                for (var j = 0; j < inventorySlotCapacityModules.Count; j++)
                {
                    total += inventorySlotCapacityModules[j].ProvidedSlotCount;
                }
            }

            inventorySlotCapacityModules.Clear();
            return Mathf.Max(0, total);
        }

        private bool CanShrinkInventoryTo(int targetSlotCount)
        {
            var inventory = Inventory == null ? null : Inventory.Inventory;
            if (inventory == null || targetSlotCount >= inventory.SlotCount)
            {
                return true;
            }

            var slots = inventory.Slots;
            for (var i = Mathf.Max(0, targetSlotCount); i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        private void CreateDynastyService()
        {
            Dynasty = new DynastyService(startingPopulation, DynastyStage.营地, startingDynastyName);
            IsGameOver = false;
            SyncExpeditionPopulationEmployment();
        }

        #region Turn

        [Header("Turn")]
        [SerializeField, LabelText("起始回合"), Min(1)] private int startingTurn = 1;
        [SerializeField, LabelText("允许键盘推进回合")] private bool allowKeyboardNextTurn = true;
        [SerializeField, LabelText("下一回合按键")] private Key nextTurnKey = Key.N;
        [SerializeField, LabelText("每帧处理建筑数"), Min(1)] private int turnBuildingsPerFrame = 4;
        [SerializeField, LabelText("输出回合日志")] private bool logTurnResult = true;

        public int CurrentTurn => Turn == null ? startingTurn : Turn.CurrentTurn;

        [ShowInInspector, ReadOnly, LabelText("正在推进回合")]
        public bool IsAdvancingTurn => Turn != null && Turn.IsAdvancingTurn;

        private Coroutine turnAdvanceCoroutine;

        [Button("下一回合")]
        public void NextTurn()
        {
            if (IsGameOver)
            {
                return;
            }

            if (Turn == null)
            {
                CreateTurnService();
            }

            if (Buildings == null)
            {
                CreateBuildingService();
            }

            if (turnAdvanceCoroutine != null || Turn.IsAdvancingTurn)
            {
                return;
            }

            if (Application.isPlaying && ShouldCancelNextTurnForMissingResearch())
            {
                return;
            }

            if (!Application.isPlaying)
            {
                LogTurnSummary(Turn.NextTurn(Buildings.Buildings));
                return;
            }

            turnAdvanceCoroutine = StartCoroutine(RunNextTurnRoutine(Turn));
        }

        private IEnumerator RunNextTurnRoutine(TurnService turn)
        {
            TurnAdvanceSummary summary = default;
            var completed = false;

            try
            {
                yield return turn.NextTurnRoutine(
                    Buildings == null ? null : Buildings.Buildings,
                    turnBuildingsPerFrame,
                    result =>
                    {
                        summary = result;
                        completed = true;
                    });

                if (completed)
                {
                    LogTurnSummary(summary);
                }
            }
            finally
            {
                turnAdvanceCoroutine = null;
            }
        }

        private void CreateTurnService()
        {
            if (Turn != null)
            {
                Turn.BeforeTurnAdvanced -= HandleBeforeTurnAdvanced;
                Turn.TurnAdvanced -= HandleTurnAdvanced;
                Turn.BuildingTechnologyPointsProvided -= HandleBuildingTechnologyPointsProvided;
            }

            Turn = new TurnService(startingTurn);
            pendingTechnologyPointsThisTurn = 0;
            Turn.BeforeTurnAdvanced += HandleBeforeTurnAdvanced;
            Turn.BuildingTechnologyPointsProvided += HandleBuildingTechnologyPointsProvided;
            Turn.TurnAdvanced += HandleTurnAdvanced;
            RefreshQuestSubscriptions();
        }

        private void CreateBuildingService()
        {
            Buildings = new BuildingService(this);
            RefreshQuestSubscriptions();
        }

        private void CreateGameEventService()
        {
            Events = new GameEventService();
        }

        internal void RegisterBuildingSelectionController(BuildingSelectionController controller)
        {
            if (controller == null)
            {
                return;
            }

            buildingSelection = controller;
        }

        internal void UnregisterBuildingSelectionController(BuildingSelectionController controller)
        {
            if (buildingSelection == controller)
            {
                buildingSelection = null;
            }
        }

        private void ResolveBuildingSelectionController()
        {
            if (buildingSelection != null)
            {
                return;
            }

            buildingSelection = FindFirstObjectByType<BuildingSelectionController>(FindObjectsInactive.Include);
        }

        private void UpdateTurnInput()
        {
            if (!IsGameOver && allowKeyboardNextTurn && IsNextTurnKeyPressed())
            {
                NextTurn();
            }
        }

        private bool IsNextTurnKeyPressed()
        {
            return Keyboard.current != null && Keyboard.current[nextTurnKey].wasPressedThisFrame;
        }

        private void LogTurnSummary(TurnAdvanceSummary summary)
        {
            if (!logTurnResult)
            {
                return;
            }

            Debug.Log(
               $"已推进至回合 {summary.ToTurn}。已处理：{summary.OperatingConsumed}，失败：{summary.Failed}，跳过：{summary.Skipped}。",
                this);
        }

        private void HandleBeforeTurnAdvanced(TurnService turn)
        {
            pendingTechnologyPointsThisTurn = 0;
        }

        private void HandleBuildingTechnologyPointsProvided(
            TurnService turn,
            BuildingTechnologyPointsProvidedEvent technologyPointsEvent)
        {
            if (!technologyPointsEvent.IsValid)
            {
                return;
            }

            pendingTechnologyPointsThisTurn += technologyPointsEvent.Points;
        }

        private void HandleTurnAdvanced(TurnService turn, TurnAdvanceSummary summary)
        {
            ClearMissingResearchWarning();
            ApplyTechnologyResearchPoints(summary.ToTurn);
            SettleTalentsForTurn(summary.ToTurn);
            SettleInheritanceForTurn(summary.ToTurn);
            SettleExpeditionsForTurn(summary.ToTurn);

            if (!endGameWhenNoPalaceAtTurnEnd || IsGameOver)
            {
                return;
            }

            if (Dynasty == null)
            {
                CreateDynastyService();
            }

            Dynasty.Refresh();
            if (Dynasty.HasPalace)
            {
                return;
            }

            EndGame(GameOverReason.NoPalace);
        }

        private void ApplyTechnologyResearchPoints(int turnNumber)
        {
            var points = pendingTechnologyPointsThisTurn;
            pendingTechnologyPointsThisTurn = 0;
            ApplyResearchPointsToTechnology(points, turnNumber);
        }

        public TechnologyResearchAppliedResult ApplyTalentResearchPoints(int amount, int turnNumber, string sourceName)
        {
            amount = Mathf.Max(0, amount);
            return ApplyResearchPointsToTechnology(amount, turnNumber);
        }

        public TechnologyResearchAppliedResult ApplyRoyalResearchPoints(int amount, int turnNumber, string sourceName)
        {
            amount = Mathf.Max(0, amount);
            return ApplyResearchPointsToTechnology(amount, turnNumber);
        }

        private TechnologyResearchAppliedResult ApplyResearchPointsToTechnology(int points, int turnNumber)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            if (Technology == null)
            {
                return default;
            }

            var result = Technology.ApplyResearchPoints(Mathf.Max(0, points));
            SyncStartingResearchFromService();
            MirrorUnlockedTechnologiesFromService();

            if (!result.Completed || result.Technology == null)
            {
                return result;
            }

            if (Events == null)
            {
                CreateGameEventService();
            }

            var effectMessage = ApplyTechnologyCompletionEffects(result.Technology);
            var message = string.IsNullOrWhiteSpace(effectMessage)
                ? $"科技研究完成：{result.Technology.DisplayName}"
                : $"科技研究完成：{result.Technology.DisplayName}（{effectMessage}）";

            Events?.AddMessage(
                GameEventMessage.ForGame(
                    GameEventCatalog.GE_科技研究完成,
                    message,
                    turnNumber));

            if (result.Technology.AllowRepeatResearch && Technology.IsCurrentResearch(result.Technology))
            {
                Events?.AddMessage(
                    GameEventMessage.ForGame(
                        GameEventCatalog.GE_科技自动重复研发,
                        $"科技已自动继续重复研发：{result.Technology.DisplayName}",
                        turnNumber));
            }

            return result;
        }

        private string ApplyTechnologyCompletionEffects(TechnologyDefinition technology)
        {
            if (technology == null)
            {
                return string.Empty;
            }

            var effects = technology.CompletionEffects;
            if (effects == null || effects.Count == 0)
            {
                return string.Empty;
            }

            var messages = new List<string>();
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                effect.Normalize();
                var result = effect.Apply(this, technology);
                if (result.Applied && result.HasMessage)
                {
                    messages.Add(result.Message);
                }
            }

            return messages.Count == 0 ? string.Empty : string.Join("，", messages);
        }

        private bool ShouldCancelNextTurnForMissingResearch()
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            if (Technology != null && Technology.HasResearchPlan)
            {
                ClearMissingResearchWarning();
                return false;
            }

            if (turnWithMissingResearchWarning == CurrentTurn)
            {
                return false;
            }

            turnWithMissingResearchWarning = CurrentTurn;
            AddMissingResearchWarningEvent();
            return true;
        }

        private void AddMissingResearchWarningEvent()
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(
                GameEventMessage.ForGame(
                    GameEventCatalog.GE_未选择研发节点,
                    "未选择研发节点：本次下回合已取消；再次点击下一回合将继续。",
                    CurrentTurn));
        }

        private void ClearMissingResearchWarning()
        {
            turnWithMissingResearchWarning = -1;
        }

        public bool EndGame(GameOverReason reason)
        {
            if (IsGameOver)
            {
                return false;
            }

            IsGameOver = true;
            GameEnded?.Invoke(this, reason);
            Debug.Log($"Game over: {FormatGameOverReason(reason)}.", this);
            return true;
        }

        private static string FormatGameOverReason(GameOverReason reason)
        {
            return reason switch
            {
                GameOverReason.NoPalace => "no palace remains",
                _ => reason.ToString()
            };
        }

        #endregion
    }
}
