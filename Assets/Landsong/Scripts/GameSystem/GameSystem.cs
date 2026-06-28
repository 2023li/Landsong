using System;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Landsong.TurnSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Landsong
{
    [DisallowMultipleComponent]
    public sealed class GameSystem : MonoSingleton<GameSystem>
    {
        [Header("Inventory")]
        [SerializeField, LabelText("物品目录")] private ItemCatalog itemCatalog;
        [SerializeField, LabelText("库存格子数量"), Min(0)] private int inventorySlotCount = 24;
        [SerializeField, LabelText("初始物品")] private ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        [Header("Scene Systems")]
        [SerializeField, LabelText("建筑目录")] private BuildingCatalog buildingCatalog;

        [Header("Building Runtime")]
        [SerializeField, LabelText("初始已解锁科技")] private string[] startingUnlockedTechnologies = Array.Empty<string>();

        [ShowInInspector, ReadOnly, LabelText("库存服务")]
        public InventoryService Inventory { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("回合服务")]
        public TurnService Turn { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("建筑目录")]
        public BuildingCatalog BuildingCatalog => buildingCatalog;

        protected override void Init()
        {
            CreateInventoryService();
            CreateTurnService();
        }

        private void Update()
        {
            UpdateTurnInput();
        }

        private void OnValidate()
        {
            inventorySlotCount = Mathf.Max(0, inventorySlotCount);
            startingTurn = Mathf.Max(1, startingTurn);

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

            if (!building.HasDefinition)
            {
                Debug.LogWarning($"Cannot register building '{building.name}' because it has no BuildingDefinition.", building);
                return;
            }

            Turn.RegisterBuilding(building);
            building.Initialize();
        }

        public void UnregisterBuilding(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            Turn?.UnregisterBuilding(building);
            if (building.IsRegistered)
            {
                building.NotifyUnregisteredFromGame(this);
            }
        }

        private void CreateInventoryService()
        {
            Inventory = new InventoryService(itemCatalog, inventorySlotCount, startingItems);
        }

        #region Turn

        [Header("Turn")]
        [SerializeField, LabelText("起始回合"), Min(1)] private int startingTurn = 1;
        [SerializeField, LabelText("允许键盘推进回合")] private bool allowKeyboardNextTurn = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField, LabelText("下一回合按键")] private Key nextTurnKey = Key.N;
#else
        [SerializeField, LabelText("下一回合按键")] private KeyCode nextTurnKey = KeyCode.N;
#endif
        [SerializeField, LabelText("输出回合日志")] private bool logTurnResult = true;

        public int CurrentTurn => Turn == null ? startingTurn : Turn.CurrentTurn;

        [Button("下一回合")]
        public TurnAdvanceSummary NextTurn()
        {
            if (Turn == null)
            {
                CreateTurnService();
            }

            var summary = Turn.NextTurn();
            LogTurnSummary(summary);
            return summary;
        }

        private void CreateTurnService()
        {
            Turn = new TurnService(startingTurn);
        }

        private void UpdateTurnInput()
        {
            if (allowKeyboardNextTurn && IsNextTurnKeyPressed())
            {
                NextTurn();
            }
        }

        private bool IsNextTurnKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[nextTurnKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(nextTurnKey);
#endif
        }

        private void LogTurnSummary(TurnAdvanceSummary summary)
        {
            if (!logTurnResult)
            {
                return;
            }

            Debug.Log(
                $"Advanced to turn {CurrentTurn}. Processed: {summary.OperatingConsumed}, failed: {summary.Failed}, skipped: {summary.Skipped}.",
                this);
        }

        #endregion
    }
}
