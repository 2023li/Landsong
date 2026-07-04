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

        [Header("Inventory")]
        [SerializeField, LabelText("物品目录")] private ItemCatalog itemCatalog;
        [SerializeField, LabelText("库存格子数量"), Min(0)] private int inventorySlotCount = 24;
        [SerializeField, LabelText("初始物品")] private ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        [Header("Dynasty")]
        [SerializeField, LabelText("初始人口"), Min(0)] private int startingPopulation;
        [SerializeField, LabelText("回合结束时无王宫则结束游戏")] private bool endGameWhenNoPalaceAtTurnEnd = true;

        [Header("Technology")]
        [SerializeField, LabelText("科技目录")] private TechnologyCatalog technologyCatalog;
        [SerializeField, LabelText("初始科技点"), Min(0)] private int startingTechnologyPoints;
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

        public int SciencePoints => Technology == null ? startingTechnologyPoints : Technology.SciencePoints;
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
            startingTechnologyPoints = Mathf.Max(0, startingTechnologyPoints);
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
                return unlocked;
            }

            if (string.IsNullOrWhiteSpace(technologyId))
            {
                return false;
            }

            return unlockedTechnologies.Add(technologyId.Trim());
        }

        public bool TryUnlockTechnology(string technologyId)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            var unlocked = Technology != null && Technology.TryUnlock(technologyId);
            MirrorUnlockedTechnologiesFromService();
            return unlocked;
        }

        public bool TryUnlockTechnology(TechnologyDefinition definition)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            var unlocked = Technology != null && Technology.TryUnlock(definition);
            MirrorUnlockedTechnologiesFromService();
            return unlocked;
        }

        public void AddTechnologyPoints(int amount)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            Technology?.AddSciencePoints(amount);
            startingTechnologyPoints = Technology == null ? startingTechnologyPoints : Technology.SciencePoints;
        }

        public void SetTechnologyPoints(int amount)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            Technology?.SetSciencePoints(amount);
            startingTechnologyPoints = Technology == null ? Mathf.Max(0, amount) : Technology.SciencePoints;
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
                SciencePoints = Mathf.Max(0, startingTechnologyPoints),
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
                        SciencePoints = Technology.SciencePoints,
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
                    SciencePoints = startingTechnologyPoints,
                    UnlockedTechnologyIds = fallbackUnlockedTechnologies == null
                        ? new List<string>(startingUnlockedTechnologies ?? Array.Empty<string>())
                        : new List<string>(fallbackUnlockedTechnologies)
                };
            }

            Technology?.RestoreSaveData(technologyData, null);
            startingTechnologyPoints = Technology == null ? 0 : Technology.SciencePoints;
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
            Technology = new TechnologyService(technologyCatalog, startingUnlockedTechnologies, startingTechnologyPoints);
            MirrorUnlockedTechnologiesFromService();
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
                Turn.TurnAdvanced -= HandleTurnAdvanced;
            }

            Turn = new TurnService(startingTurn);
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

        private void HandleTurnAdvanced(TurnService turn, TurnAdvanceSummary summary)
        {
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
