using System.Collections.Generic;
using Landsong.InventorySystem;
using Landsong.TalentSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_Talent : MonoBehaviour
    {
        [SerializeField, LabelText("关闭按钮")] private Button closeButton;
        [SerializeField, LabelText("刷新按钮")] private Button refreshButton;
        [SerializeField, LabelText("刷新按钮文本")] private TMP_Text refreshButtonLabel;
        [SerializeField, LabelText("状态文本")] private TMP_Text statusLabel;
        [SerializeField, LabelText("薪资汇总文本")] private TMP_Text salarySummaryLabel;
        [SerializeField, LabelText("选中人才文本")] private TMP_Text selectedTalentLabel;

        [TitleGroup("候选卡")]
        [SerializeField, LabelText("候选卡根节点")] private RectTransform offerRoot;
        [SerializeField, LabelText("候选卡预制体")] private GamePanel_TalentOfferItem offerItemPrefab;
        [SerializeField, LabelText("候选卡空状态")] private GameObject offerEmptyRoot;
        [SerializeField, LabelText("候选卡空状态文本")] private TMP_Text offerEmptyLabel;

        [TitleGroup("人才池")]
        [SerializeField, LabelText("人才池根节点")] private RectTransform poolRoot;
        [SerializeField, LabelText("人才池预制体")] private GamePanel_TalentPoolItem poolItemPrefab;
        [SerializeField, LabelText("人才池空状态")] private GameObject poolEmptyRoot;
        [SerializeField, LabelText("人才池空状态文本")] private TMP_Text poolEmptyLabel;

        [TitleGroup("人才槽")]
        [SerializeField, LabelText("人才槽根节点")] private RectTransform slotRoot;
        [SerializeField, LabelText("人才槽预制体")] private GamePanel_TalentSlotItem slotItemPrefab;
        [SerializeField, LabelText("人才槽空状态")] private GameObject slotEmptyRoot;
        [SerializeField, LabelText("人才槽空状态文本")] private TMP_Text slotEmptyLabel;

        private readonly List<GamePanel_TalentOfferItem> activeOfferItems = new List<GamePanel_TalentOfferItem>();
        private readonly List<GamePanel_TalentOfferItem> offerItemPool = new List<GamePanel_TalentOfferItem>();
        private readonly List<GamePanel_TalentPoolItem> activePoolItems = new List<GamePanel_TalentPoolItem>();
        private readonly List<GamePanel_TalentPoolItem> poolItemPool = new List<GamePanel_TalentPoolItem>();
        private readonly List<GamePanel_TalentSlotItem> activeSlotItems = new List<GamePanel_TalentSlotItem>();
        private readonly List<GamePanel_TalentSlotItem> slotItemPool = new List<GamePanel_TalentSlotItem>();

        private UIPanel_Game gamePanel;
        private GameSystem gameSystem;
        private InventoryService subscribedInventory;
        private bool subscribedToTalents;
        private string selectedTalentInstanceId = string.Empty;
        private string lastStatusMessage = string.Empty;

        private void Reset()
        {
            offerRoot = transform as RectTransform;
            poolRoot = transform as RectTransform;
            slotRoot = transform as RectTransform;
        }

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeRuntimeServices();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeRuntimeServices();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            UnsubscribeRuntimeServices();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ResolveReferences();
            SubscribeRuntimeServices();
            Refresh();
        }

        public void Hide()
        {
            UnsubscribeRuntimeServices();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            ResolveReferences();
            EnsureSelectedTalent();
            RefreshHeader();
            RefreshOffers();
            RefreshPool();
            RefreshSlots();
        }

        private void ResolveReferences()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            gameSystem = GameSystem.Instance;
        }

        private void BindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(HandleRefreshClicked);
                refreshButton.onClick.AddListener(HandleRefreshClicked);
            }
        }

        private void UnbindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }

            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(HandleRefreshClicked);
            }
        }

        private void SubscribeRuntimeServices()
        {
            SubscribeTalents();
            SubscribeInventory();
        }

        private void UnsubscribeRuntimeServices()
        {
            UnsubscribeTalents();
            UnsubscribeInventory();
        }

        private void SubscribeTalents()
        {
            if (subscribedToTalents || gameSystem == null)
            {
                return;
            }

            gameSystem.TalentsChanged += HandleTalentsChanged;
            subscribedToTalents = true;
        }

        private void UnsubscribeTalents()
        {
            if (!subscribedToTalents || gameSystem == null)
            {
                subscribedToTalents = false;
                return;
            }

            gameSystem.TalentsChanged -= HandleTalentsChanged;
            subscribedToTalents = false;
        }

        private void SubscribeInventory()
        {
            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            if (subscribedInventory == inventory)
            {
                return;
            }

            UnsubscribeInventory();
            subscribedInventory = inventory;
            if (subscribedInventory != null)
            {
                subscribedInventory.InventoryChanged += HandleInventoryChanged;
            }
        }

        private void UnsubscribeInventory()
        {
            if (subscribedInventory != null)
            {
                subscribedInventory.InventoryChanged -= HandleInventoryChanged;
                subscribedInventory = null;
            }
        }

        private void RefreshHeader()
        {
            var service = gameSystem == null ? null : gameSystem.Talents;
            if (service == null)
            {
                SetText(statusLabel, "人才系统未初始化");
                SetText(salarySummaryLabel, string.Empty);
                SetText(selectedTalentLabel, string.Empty);
                SetRefreshButton(false, "刷新");
                return;
            }

            var selectedTalent = FindSelectedTalent();
            SetText(selectedTalentLabel, selectedTalent == null ? "未选择人才" : $"已选择：{selectedTalent.DisplayName}");
            SetText(salarySummaryLabel, BuildSalarySummary(service));
            SetText(statusLabel, string.IsNullOrWhiteSpace(lastStatusMessage) ? BuildStatusSummary(service) : lastStatusMessage);
            SetRefreshButton(service.CanPayRefreshCost(), $"刷新人才（{service.RefreshGoldCost}）");
        }

        private string BuildSalarySummary(TalentService service)
        {
            if (service == null)
            {
                return string.Empty;
            }

            var goldName = service.SalaryGoldItemDefinition == null ? "金币未配置" : service.SalaryGoldItemDefinition.DisplayName;
            var salary = service.CalculateTotalSalaryGoldPerTurn();
            var available = service.SalaryGoldItemDefinition != null && gameSystem != null && gameSystem.Inventory != null
                ? gameSystem.Inventory.GetQuantity(service.SalaryGoldItemDefinition.ItemId)
                : 0;
            return $"{goldName} {available} / 薪资 {salary}/回合";
        }

        private string BuildStatusSummary(TalentService service)
        {
            return service == null
                ? "人才系统未初始化"
                : $"人才池 {service.OwnedTalents.Count} / 候选 {service.CurrentOffers.Count} / 槽位 {service.SlotStates.Count}";
        }

        private void RefreshOffers()
        {
            ReleaseActiveOfferItems();
            var offers = gameSystem == null ? null : gameSystem.TalentOffers;
            if (offerRoot == null || offerItemPrefab == null || offers == null)
            {
                SetEmptyState(offerEmptyRoot, offerEmptyLabel, true, "候选卡列表未配置");
                return;
            }

            for (var i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                if (offer == null || !offer.HasDefinition)
                {
                    continue;
                }

                var item = GetOfferItemFromPool();
                item.Bind(offer, HandleRecruitClicked);
                activeOfferItems.Add(item);
            }

            SetEmptyState(offerEmptyRoot, offerEmptyLabel, activeOfferItems.Count <= 0, "刷新后出现候选人才");
        }

        private void RefreshPool()
        {
            ReleaseActivePoolItems();
            var talents = gameSystem == null ? null : gameSystem.TalentPool;
            var service = gameSystem == null ? null : gameSystem.Talents;
            if (poolRoot == null || poolItemPrefab == null || talents == null)
            {
                SetEmptyState(poolEmptyRoot, poolEmptyLabel, true, "人才池列表未配置");
                return;
            }

            for (var i = 0; i < talents.Count; i++)
            {
                var talent = talents[i];
                if (talent == null)
                {
                    continue;
                }

                var slot = service == null ? null : service.GetAssignedSlotForTalent(talent.TalentInstanceId);
                var assignmentText = slot == null ? string.Empty : $"任命：{slot.DisplayName}";
                var item = GetPoolItemFromPool();
                item.Bind(
                    talent,
                    IsSelectedTalent(talent),
                    assignmentText,
                    HandleTalentSelected,
                    HandleUpgradeClicked,
                    HandleUnassignTalentClicked);
                activePoolItems.Add(item);
            }

            SetEmptyState(poolEmptyRoot, poolEmptyLabel, activePoolItems.Count <= 0, "尚未招募人才");
        }

        private void RefreshSlots()
        {
            ReleaseActiveSlotItems();
            var slots = gameSystem == null ? null : gameSystem.TalentSlots;
            if (slotRoot == null || slotItemPrefab == null || slots == null)
            {
                SetEmptyState(slotEmptyRoot, slotEmptyLabel, true, "人才槽列表未配置");
                return;
            }

            var selectedTalent = FindSelectedTalent();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                var item = GetSlotItemFromPool();
                item.Bind(slot, selectedTalent, HandleAssignClicked, HandleClearSlotClicked);
                activeSlotItems.Add(item);
            }

            SetEmptyState(slotEmptyRoot, slotEmptyLabel, activeSlotItems.Count <= 0, "没有可用人才槽");
        }

        private void EnsureSelectedTalent()
        {
            if (FindSelectedTalent() != null)
            {
                return;
            }

            selectedTalentInstanceId = string.Empty;
            var talents = gameSystem == null ? null : gameSystem.TalentPool;
            if (talents == null || talents.Count == 0)
            {
                return;
            }

            selectedTalentInstanceId = talents[0]?.TalentInstanceId ?? string.Empty;
        }

        private TalentState FindSelectedTalent()
        {
            return gameSystem == null || gameSystem.Talents == null
                ? null
                : gameSystem.Talents.FindOwnedTalent(selectedTalentInstanceId);
        }

        private bool IsSelectedTalent(TalentState talent)
        {
            return talent != null
                   && !string.IsNullOrWhiteSpace(selectedTalentInstanceId)
                   && talent.TalentInstanceId == selectedTalentInstanceId;
        }

        private GamePanel_TalentOfferItem GetOfferItemFromPool()
        {
            GamePanel_TalentOfferItem item;
            var lastIndex = offerItemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = offerItemPool[lastIndex];
                offerItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(offerItemPrefab);
            }

            item.transform.SetParent(offerRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private GamePanel_TalentPoolItem GetPoolItemFromPool()
        {
            GamePanel_TalentPoolItem item;
            var lastIndex = poolItemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = poolItemPool[lastIndex];
                poolItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(poolItemPrefab);
            }

            item.transform.SetParent(poolRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private GamePanel_TalentSlotItem GetSlotItemFromPool()
        {
            GamePanel_TalentSlotItem item;
            var lastIndex = slotItemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = slotItemPool[lastIndex];
                slotItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(slotItemPrefab);
            }

            item.transform.SetParent(slotRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseActiveOfferItems()
        {
            for (var i = 0; i < activeOfferItems.Count; i++)
            {
                var item = activeOfferItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                if (offerRoot != null)
                {
                    item.transform.SetParent(offerRoot, false);
                }
                offerItemPool.Add(item);
            }

            activeOfferItems.Clear();
        }

        private void ReleaseActivePoolItems()
        {
            for (var i = 0; i < activePoolItems.Count; i++)
            {
                var item = activePoolItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                if (poolRoot != null)
                {
                    item.transform.SetParent(poolRoot, false);
                }
                poolItemPool.Add(item);
            }

            activePoolItems.Clear();
        }

        private void ReleaseActiveSlotItems()
        {
            for (var i = 0; i < activeSlotItems.Count; i++)
            {
                var item = activeSlotItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                if (slotRoot != null)
                {
                    item.transform.SetParent(slotRoot, false);
                }
                slotItemPool.Add(item);
            }

            activeSlotItems.Clear();
        }

        private void HandleRefreshClicked()
        {
            if (gameSystem == null)
            {
                return;
            }

            gameSystem.TryRefreshTalents(out var result);
            lastStatusMessage = result.Message;
            Refresh();
        }

        private void HandleRecruitClicked(TalentOfferState offer)
        {
            if (gameSystem == null || offer == null)
            {
                return;
            }

            gameSystem.TryRecruitTalentOffer(offer.OfferId, out var result);
            if (result.Talent != null)
            {
                selectedTalentInstanceId = result.Talent.TalentInstanceId;
            }

            lastStatusMessage = result.Message;
            Refresh();
        }

        private void HandleTalentSelected(TalentState talent)
        {
            selectedTalentInstanceId = talent == null ? string.Empty : talent.TalentInstanceId;
            lastStatusMessage = string.Empty;
            Refresh();
        }

        private void HandleUpgradeClicked(TalentState talent)
        {
            if (gameSystem == null || talent == null)
            {
                return;
            }

            gameSystem.TryUpgradeTalent(talent.TalentInstanceId, out var result);
            lastStatusMessage = result.Message;
            Refresh();
        }

        private void HandleUnassignTalentClicked(TalentState talent)
        {
            if (gameSystem == null || talent == null)
            {
                return;
            }

            gameSystem.TryUnassignTalent(talent.TalentInstanceId, out var result);
            lastStatusMessage = result.Message;
            Refresh();
        }

        private void HandleAssignClicked(TalentSlotRuntimeState slot)
        {
            if (gameSystem == null || slot == null)
            {
                return;
            }

            var selectedTalent = FindSelectedTalent();
            if (selectedTalent == null)
            {
                return;
            }

            gameSystem.TryAssignTalent(selectedTalent.TalentInstanceId, slot.SlotId, out var result);
            lastStatusMessage = result.Message;
            Refresh();
        }

        private void HandleClearSlotClicked(TalentSlotRuntimeState slot)
        {
            if (gameSystem == null || slot == null)
            {
                return;
            }

            gameSystem.TryUnassignTalentSlot(slot.SlotId, out var result);
            lastStatusMessage = result.Message;
            Refresh();
        }

        private void HandleTalentsChanged(GameSystem changedGameSystem)
        {
            gameSystem = changedGameSystem;
            SubscribeInventory();
            Refresh();
        }

        private void HandleInventoryChanged(InventoryService changedInventory)
        {
            subscribedInventory = changedInventory;
            RefreshHeader();
        }

        private void HandleCloseClicked()
        {
            if (gamePanel != null)
            {
                gamePanel.Hide_Talent();
                return;
            }

            Hide();
        }

        private void SetRefreshButton(bool interactable, string label)
        {
            if (refreshButton != null)
            {
                refreshButton.interactable = interactable;
            }

            SetText(refreshButtonLabel, label);
        }

        private static void SetEmptyState(GameObject root, TMP_Text label, bool visible, string message)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }

            SetText(label, visible ? message : string.Empty);
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }
    }
}
