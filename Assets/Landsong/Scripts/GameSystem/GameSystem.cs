using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
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
    public sealed class GameSystem : MonoSingleton<GameSystem>, IBuildingJobAttractionModifierProvider
    {
        private static readonly IReadOnlyList<BuildingJobAttractionModifier> EmptyJobAttractionModifiers =
            Array.Empty<BuildingJobAttractionModifier>();

        private readonly List<BuildingInventorySlotCapacityModule> inventorySlotCapacityModules =
            new List<BuildingInventorySlotCapacityModule>();
        private readonly HashSet<string> unlockedTechnologies =
            new HashSet<string>(StringComparer.Ordinal);
        private int pendingTechnologyPointsThisTurn;

        [Header("Inventory")]
        [SerializeField, LabelText("物品目录")] private ItemCatalog itemCatalog;
        [SerializeField, LabelText("库存格子数量"), Min(0)] private int inventorySlotCount = 24;
        [SerializeField, LabelText("初始物品")] private ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        [Header("Dynasty")]
        [SerializeField, LabelText("初始人口"), Min(0)] private int startingPopulation;
        [SerializeField, LabelText("回合结束时无王宫则结束游戏")] private bool endGameWhenNoPalaceAtTurnEnd = true;

        [Header("Technology")]
        [SerializeField, LabelText("科技目录")] private TechnologyCatalog technologyCatalog;
        [SerializeField, LabelText("初始当前研究科技")] private TechnologyDefinition startingResearchTechnology;
        [SerializeField, LabelText("初始当前研究进度"), Min(0)] private int startingResearchProgress;
        [SerializeField, LabelText("初始已解锁科技")] private string[] startingUnlockedTechnologies = Array.Empty<string>();

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

        [ShowInInspector, ReadOnly, LabelText("当前建筑选择控制器")]
        public BuildingSelectionController BuildingSelection => buildingSelection;

        [ShowInInspector, ReadOnly, LabelText("建筑目录")]
        public BuildingCatalog BuildingCatalog => buildingCatalog;

        [ShowInInspector, ReadOnly, LabelText("科技目录")]
        public TechnologyCatalog TechnologyCatalog => technologyCatalog;

        [ShowInInspector, ReadOnly, LabelText("当前人口")]
        public int Population => Dynasty == null ? startingPopulation : Dynasty.Population;

        [ShowInInspector, ReadOnly, LabelText("拥有王宫")]
        public bool HasPalace => Dynasty != null && Dynasty.HasPalace;

        [ShowInInspector, ReadOnly, LabelText("游戏已结束")]
        public bool IsGameOver { get; private set; }

        public event Action<GameSystem, GameOverReason> GameEnded;

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

        public IReadOnlyList<BuildingJobAttractionModifier> GetJobAttractionModifiers(BuildingBase building)
        {
            return EmptyJobAttractionModifiers;
        }

        protected override void Init()
        {
            CreateTechnologyService();
            CreateInventoryService();
            CreateDynastyService();
            CreateTurnService();
            CreateBuildingService();
            CreateGameEventService();
            ResolveBuildingSelectionController();
        }

        private void Update()
        {
            UpdateTurnInput();
        }

        private void OnDestroy()
        {
            buildingSelection = null;
        }

        private void OnValidate()
        {
            inventorySlotCount = Mathf.Max(0, inventorySlotCount);
            startingPopulation = Mathf.Max(0, startingPopulation);
            startingResearchProgress = Mathf.Max(0, startingResearchProgress);
            startingTurn = Mathf.Max(1, startingTurn);
            turnBuildingsPerFrame = Mathf.Max(1, turnBuildingsPerFrame);

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

        internal void RestoreCurrentTurn(int currentTurn)
        {
            if (Turn == null)
            {
                CreateTurnService();
            }

            Turn.SetCurrentTurn(currentTurn);
            startingTurn = Turn.CurrentTurn;
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
            Technology = new TechnologyService(
                technologyCatalog,
                startingUnlockedTechnologies,
                startingResearchTechnology == null ? null : startingResearchTechnology.TechnologyId,
                startingResearchProgress);
            MirrorUnlockedTechnologiesFromService();
            SyncStartingResearchFromService();
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
            Dynasty = new DynastyService(startingPopulation);
            IsGameOver = false;
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

            if (turnAdvanceCoroutine != null || Turn.IsAdvancingTurn)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                LogTurnSummary(Turn.NextTurn());
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
        }

        private void CreateBuildingService()
        {
            Buildings = new BuildingService(this);
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
            ApplyTechnologyResearchPoints(summary.ToTurn);

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
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            if (Technology == null)
            {
                pendingTechnologyPointsThisTurn = 0;
                return;
            }

            var result = Technology.ApplyResearchPoints(pendingTechnologyPointsThisTurn);
            pendingTechnologyPointsThisTurn = 0;
            SyncStartingResearchFromService();
            MirrorUnlockedTechnologiesFromService();

            if (!result.Completed || result.Technology == null)
            {
                return;
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
