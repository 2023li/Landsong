using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.ExpeditionSystem;
using Landsong.GameEventSystem;
using Landsong.InheritanceSystem;
using Landsong.InventorySystem;
using Landsong.PolicySystem;
using Landsong.TalentSystem;
using Landsong.TechnologySystem;
using Landsong.TurnSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong
{
    public enum GameOverReason
    {
        None = 0,
        NoPalace = 1
    }

    [DisallowMultipleComponent]
    public sealed partial class GameSystem : MonoSingleton<GameSystem>, IBuildingJobAttractionModifierProvider
    {
        private static readonly IReadOnlyList<BuildingJobAttractionModifier> EmptyJobAttractionModifiers =
            Array.Empty<BuildingJobAttractionModifier>();
        private const string InspectorInventory = "库存";
        private const string InspectorDynasty = "王朝";
        private const string InspectorTechnology = "科技";
        private const string InspectorPolicy = "政策";
        private const string InspectorQuest = "任务";
        private const string InspectorExpedition = "远征";
        private const string InspectorTalent = "人才";
        private const string InspectorInheritance = "继承";
        private const string InspectorSceneSystems = "场景系统";
        private const string InspectorRuntimeServices = "运行时服务";
        private const string InspectorRuntimeStatus = "运行时状态";
        private const string InspectorTurn = "回合";
        private const string GameplayDebugGoldItemId = "金币";

        private readonly List<IBuildingInventorySlotProvider> inventorySlotProviders =
            new List<IBuildingInventorySlotProvider>();
        private readonly List<InventorySlotProvision> inventorySlotProvisions =
            new List<InventorySlotProvision>();
        private readonly List<IBuildingJobAttractionModifierSource> localJobAttractionModifierSources =
            new List<IBuildingJobAttractionModifierSource>();
        private readonly List<BuildingJobAttractionModifier> activeJobAttractionModifiers =
            new List<BuildingJobAttractionModifier>();

        [SerializeField, FoldoutGroup(InspectorInventory), LabelText("物品目录")] private ItemCatalog itemCatalog;
        [SerializeField, FoldoutGroup(InspectorInventory), LabelText("库存槽位类型目录")]
        private InventorySlotTypeCatalog inventorySlotTypeCatalog;
        [SerializeField, FoldoutGroup(InspectorInventory), LabelText("初始物品")] private ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        [SerializeField, FoldoutGroup(InspectorDynasty), LabelText("初始王朝名称")] private string startingDynastyName = DynastyService.DefaultDynastyName;
        [SerializeField, FoldoutGroup(InspectorDynasty), LabelText("初始人口"), Min(0)] private int startingPopulation;
        [SerializeField, FoldoutGroup(InspectorDynasty), LabelText("回合结束时无王宫则结束游戏")] private bool endGameWhenNoPalaceAtTurnEnd = true;

        [SerializeField, FoldoutGroup(InspectorTechnology), LabelText("科技目录")] private TechnologyCatalog technologyCatalog;
        [SerializeField, FoldoutGroup(InspectorTechnology), LabelText("初始当前研究科技")] private TechnologyDefinition startingResearchTechnology;
        [SerializeField, FoldoutGroup(InspectorTechnology), LabelText("初始当前研究进度"), Min(0)] private int startingResearchProgress;
        [SerializeField, FoldoutGroup(InspectorTechnology), LabelText("初始已解锁科技")] private string[] startingUnlockedTechnologies = Array.Empty<string>();
        [SerializeField, FoldoutGroup(InspectorTechnology), LabelText("全局 Buff 目录")]
        private TechnologyGlobalBuffCatalog technologyGlobalBuffCatalog;
        [SerializeField, FoldoutGroup(InspectorTechnology), LabelText("额外科技内容生产者")]
        [Tooltip("实现 ITechnologyUnlockContentProducer 的领域目录。它们在初始化时主动向中央注册表注入完整快照；UI 不会扫描这些资产。")]
        private ScriptableObject[] technologyUnlockContentProducerAssets = Array.Empty<ScriptableObject>();

        [SerializeField, FoldoutGroup(InspectorPolicy), LabelText("政策目录")] private PolicyCatalog policyCatalog;
        [SerializeField, FoldoutGroup(InspectorPolicy), LabelText("初始民意"), Min(0)] private int startingPublicOpinion;
        [SerializeField, FoldoutGroup(InspectorPolicy), LabelText("初始已选择政策")] private PolicyDefinition[] startingSelectedPolicies = Array.Empty<PolicyDefinition>();

        [SerializeField, FoldoutGroup(InspectorExpedition), LabelText("远征目的地目录")] private ExpeditionDestinationCatalog expeditionDestinationCatalog;
        [SerializeField, FoldoutGroup(InspectorExpedition), LabelText("远征补贴金币物品")] private ItemDefinition expeditionSubsidyGoldItemDefinition;
        [SerializeField, FoldoutGroup(InspectorExpedition), LabelText("远征队伍上限"), Min(1)] private int expeditionTeamCapacity = 3;
        [SerializeField, FoldoutGroup(InspectorSceneSystems), LabelText("额外初始建筑蓝图")] private string[] startingUnlockedBuildingBlueprintIds = Array.Empty<string>();
        [SerializeField, FoldoutGroup(InspectorExpedition), LabelText("补贴不足惩罚持续回合"), Min(1)] private int expeditionSubsidyPenaltyDurationTurns = 5;
        [SerializeField, FoldoutGroup(InspectorExpedition), LabelText("每层补贴不足惩罚岗位吸引力"), Min(0f)] private float expeditionSubsidyPenaltyAttractionPerStack = 5f;

        [SerializeField, FoldoutGroup(InspectorTalent), LabelText("人才目录")] private TalentCatalog talentCatalog;
        [SerializeField, FoldoutGroup(InspectorTalent), LabelText("人才金币物品")] private ItemDefinition talentGoldItemDefinition;
        [SerializeField, FoldoutGroup(InspectorTalent), LabelText("刷新费用"), Min(0)] private int talentRefreshGoldCost = 20;
        [SerializeField, FoldoutGroup(InspectorTalent), LabelText("每次刷新卡数"), Min(1)] private int talentRefreshCardCount = 3;
        [SerializeField, FoldoutGroup(InspectorTalent), LabelText("初始人才槽")] private TalentSlotDefinition[] startingTalentSlots = Array.Empty<TalentSlotDefinition>();

        [SerializeField, FoldoutGroup(InspectorInheritance), LabelText("王族特性目录")] private RoyalTraitCatalog royalTraitCatalog;
        [SerializeField, FoldoutGroup(InspectorInheritance), LabelText("继承系统配置")] private RoyalInheritanceConfig royalInheritanceConfig = new RoyalInheritanceConfig();

        [SerializeField, FoldoutGroup(InspectorSceneSystems), LabelText("建筑目录")] private BuildingCatalog buildingCatalog;
        [SerializeField, FoldoutGroup(InspectorSceneSystems), LabelText("建筑选择控制器")] private BuildingSelectionController buildingSelection;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("库存服务")]
        internal InventoryService Inventory { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("经济预测服务")]
        internal EconomyForecastService EconomyForecast { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("回合服务")]
        internal TurnService Turn { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("王朝服务")]
        internal DynastyService Dynasty { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("建筑服务")]
        internal BuildingService Buildings { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("事件服务")]
        internal GameEventService Events { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("科技服务")]
        internal TechnologyService Technology { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("科技解锁内容注册表")]
        internal TechnologyUnlockContentRegistry TechnologyUnlockContents { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("全局 Buff 服务")]
        internal TechnologyGlobalBuffService GlobalBuffs { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("政策服务")]
        internal PolicyService Policies { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("远征服务")]
        internal ExpeditionService Expeditions { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("人才服务")]
        internal TalentService Talents { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeServices), LabelText("继承服务")]
        internal RoyalInheritanceService Inheritance { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("当前建筑选择控制器")]
        internal BuildingSelectionController BuildingSelection => buildingSelection;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("建筑目录")]
        internal BuildingCatalog BuildingCatalog => buildingCatalog;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("科技目录")]
        internal TechnologyCatalog TechnologyCatalog => technologyCatalog;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("政策目录")]
        internal PolicyCatalog PolicyCatalog => policyCatalog;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("当前民意")]
        internal int PublicOpinion => Policies == null ? startingPublicOpinion : Policies.PublicOpinion;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("当前人口")]
        internal int Population => Dynasty == null ? startingPopulation : Dynasty.Population;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("王朝名称")]
        internal string DynastyName => Dynasty == null ? startingDynastyName : Dynasty.DynastyName;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("拥有王宫")]
        internal bool HasPalace => Dynasty != null && Dynasty.HasPalace;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorRuntimeStatus), LabelText("游戏已结束")]
        public bool IsGameOver { get; private set; }

        public event Action<GameSystem, GameOverReason> GameEnded;

        protected override void Init()
        {
            EnsureRuntimeServices();
            ResolveBuildingSelectionController();
        }

        internal void EnsureRuntimeServices()
        {
            if (Features == null)
            {
                Features = new GameFeatureUnlockService(startingUnlockedFeatures);
                Features.StateChanged += HandleFeatureAvailabilityChanged;
            }

            if (Technology == null)
            {
                CreateTechnologyService();
            }

            GlobalBuffs ??= new TechnologyGlobalBuffService(this, technologyGlobalBuffCatalog);
            TechnologyUnlockContents ??= new TechnologyUnlockContentRegistry();

            if (Policies == null)
            {
                CreatePolicyService();
            }

            if (Inventory == null)
            {
                CreateInventoryService();
            }

            if (Dynasty == null)
            {
                CreateDynastyService();
            }

            if (Turn == null)
            {
                CreateTurnService();
            }

            if (Buildings == null)
            {
                CreateBuildingService();
            }

            if (Events == null)
            {
                CreateGameEventService();
            }

            if (EconomyForecast == null)
            {
                CreateEconomyForecastService();
            }

            if (BuildingBlueprints == null)
            {
                CreateBuildingBlueprintService();
            }

            if (Expeditions == null)
            {
                CreateExpeditionService();
            }

            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Inheritance == null)
            {
                CreateInheritanceService();
            }

            if (Quest == null)
            {
                CreateQuestService();
            }
        }

        private void Update()
        {
            UpdateTurnInput();
        }

        private void OnDestroy()
        {
            if (Features != null)
            {
                Features.StateChanged -= HandleFeatureAvailabilityChanged;
            }

            UnsubscribeQuestRuntimeServices();
            UnsubscribeExpeditionService();
            UnsubscribeTalentService();
            UnsubscribeInheritanceService();
            buildingSelection = null;
        }

        private void HandleFeatureAvailabilityChanged(GameFeatureUnlockService changedFeatures)
        {
            Policies?.NotifyFeatureAvailabilityChanged();
            SyncExpeditionPopulationEmployment();
        }

        private void OnValidate()
        {
            startingDynastyName = DynastyService.NormalizeDynastyName(startingDynastyName);
            startingPopulation = Mathf.Max(0, startingPopulation);
            startingResearchProgress = Mathf.Max(0, startingResearchProgress);
            startingPublicOpinion = Mathf.Max(0, startingPublicOpinion);
            startingTurn = Mathf.Max(1, startingTurn);
            turnBuildingsPerFrame = Mathf.Max(1, turnBuildingsPerFrame);
            expeditionTeamCapacity = Mathf.Max(1, expeditionTeamCapacity);
            expeditionSubsidyPenaltyDurationTurns = Mathf.Max(1, expeditionSubsidyPenaltyDurationTurns);
            expeditionSubsidyPenaltyAttractionPerStack = Mathf.Max(0f, expeditionSubsidyPenaltyAttractionPerStack);
            startingRandomQuestCount = Mathf.Max(0, startingRandomQuestCount);
            maxActiveRandomQuests = Mathf.Max(0, maxActiveRandomQuests);
            randomQuestRefreshIntervalTurns = Mathf.Max(1, randomQuestRefreshIntervalTurns);
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

            startingSelectedPolicies ??= Array.Empty<PolicyDefinition>();

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

            NormalizeQuestConfiguration();
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

        private void CreatePolicyService()
        {
            Policies = new PolicyService(
                policyCatalog,
                () => Features != null && Features.IsUnlocked(GameFeature.Congress),
                startingPublicOpinion,
                startingSelectedPolicies);
        }

        internal void RegisterBuilding(BuildingBase building)
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

        internal void UnregisterBuilding(BuildingBase building)
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
            Inventory = new InventoryService(
                itemCatalog,
                inventorySlotTypeCatalog,
                startingItems,
                () => GlobalBuffs?.GetInventoryLossRateMultiplier() ?? 1f);
            RefreshInventorySlotCapacity();
            RefreshQuestSubscriptions();
        }

        internal void RefreshInventorySlotCapacity()
        {
            if (Inventory == null)
            {
                return;
            }

            inventorySlotProvisions.Clear();
            var buildings = Buildings == null ? null : Buildings.Buildings;
            if (buildings != null)
            {
                for (var i = 0; i < buildings.Count; i++)
                {
                    var building = buildings[i];
                    if (building == null || !building.isActiveAndEnabled || building.IsDemolishing)
                    {
                        continue;
                    }

                    inventorySlotProviders.Clear();
                    building.GetCapabilities(inventorySlotProviders);
                    for (var j = 0; j < inventorySlotProviders.Count; j++)
                    {
                        var provisions = inventorySlotProviders[j]
                            .GetInventorySlotProvisions(building);
                        if (provisions == null)
                        {
                            continue;
                        }

                        for (var k = 0; k < provisions.Count; k++)
                        {
                            if (provisions[k] != null && provisions[k].IsValid)
                            {
                                inventorySlotProvisions.Add(provisions[k]);
                            }
                        }
                    }
                }
            }

            inventorySlotProviders.Clear();
            if (!Inventory.SynchronizeSlots(inventorySlotProvisions))
            {
                Debug.LogWarning(
                    "库存槽位拓扑更新被拒绝：将被撤销的建筑槽位中仍存有物品。请先清空对应建筑的槽位。");
            }

            inventorySlotProvisions.Clear();
        }

        internal bool CanDemolishInventoryProvider(BuildingBase building, out string failureMessage)
        {
            if (building == null
                || Inventory == null
                || !Inventory.HasStoredItemsForProvider(building.InstanceId))
            {
                failureMessage = string.Empty;
                return true;
            }

            failureMessage = "该建筑提供的库存格中仍有物品，请先清空这些槽位。";
            return false;
        }

        private void CreateDynastyService()
        {
            Dynasty = new DynastyService(startingPopulation, DynastyStage.营地, startingDynastyName);
            IsGameOver = false;
            SyncExpeditionPopulationEmployment();
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

        private void CreateEconomyForecastService()
        {
            EconomyForecast = new EconomyForecastService(this);
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

    }
}
