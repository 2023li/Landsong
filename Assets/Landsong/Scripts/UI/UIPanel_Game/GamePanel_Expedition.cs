using System;
using System.Collections.Generic;
using Landsong.DynastySystem;
using Landsong.ExpeditionSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_Expedition : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField, LabelText("关闭按钮")] private Button closeButton;
        [SerializeField, LabelText("出发按钮")] private Button launchButton;
        [SerializeField, LabelText("出发按钮文本")] private TMP_Text launchButtonLabel;
        [SerializeField, LabelText("人口输入")] private TMP_InputField populationInput;
        [SerializeField, LabelText("状态文本")] private TMP_Text statusLabel;
        [SerializeField, LabelText("惩罚文本")] private TMP_Text penaltyLabel;

        [Header("Destination")]
        [SerializeField, LabelText("目的地根节点")] private RectTransform destinationRoot;
        [SerializeField, LabelText("目的地条目预制体")] private GamePanel_ExpeditionDestinationItem destinationItemPrefab;
        [SerializeField, LabelText("空目的地根节点")] private GameObject emptyDestinationRoot;
        [SerializeField, LabelText("空目的地文本")] private TMP_Text emptyDestinationLabel;
        [SerializeField, LabelText("显示不可用目的地")] private bool includeUnavailableDestinations = true;

        [Header("Selected Destination")]
        [SerializeField, LabelText("选中名称")] private TMP_Text selectedTitleLabel;
        [SerializeField, LabelText("选中描述")] private TMP_Text selectedDescriptionLabel;
        [SerializeField, LabelText("选中规则")] private TMP_Text selectedRuleLabel;
        [SerializeField, LabelText("预览成功率")] private TMP_Text successChancePreviewLabel;
        [SerializeField, LabelText("人口提示")] private TMP_Text populationHintLabel;

        [Header("Supplies")]
        [SerializeField, LabelText("物资根节点")] private RectTransform supplyRoot;
        [SerializeField, LabelText("物资输入预制体")] private GamePanel_ExpeditionSupplyInput supplyInputPrefab;
        [SerializeField, LabelText("空物资根节点")] private GameObject emptySupplyRoot;

        [Header("Expeditions")]
        [SerializeField, LabelText("远征记录根节点")] private RectTransform expeditionRoot;
        [SerializeField, LabelText("远征记录预制体")] private GamePanel_ExpeditionStateItem expeditionItemPrefab;
        [SerializeField, LabelText("空远征根节点")] private GameObject emptyExpeditionRoot;
        [SerializeField, LabelText("空远征文本")] private TMP_Text emptyExpeditionLabel;
        [SerializeField, LabelText("显示历史记录")] private bool showHistory = true;

        private readonly List<GamePanel_ExpeditionDestinationItem> activeDestinationItems =
            new List<GamePanel_ExpeditionDestinationItem>();
        private readonly List<GamePanel_ExpeditionDestinationItem> destinationItemPool =
            new List<GamePanel_ExpeditionDestinationItem>();
        private readonly List<GamePanel_ExpeditionSupplyInput> activeSupplyInputs =
            new List<GamePanel_ExpeditionSupplyInput>();
        private readonly List<GamePanel_ExpeditionSupplyInput> supplyInputPool =
            new List<GamePanel_ExpeditionSupplyInput>();
        private readonly List<GamePanel_ExpeditionStateItem> activeExpeditionItems =
            new List<GamePanel_ExpeditionStateItem>();
        private readonly List<GamePanel_ExpeditionStateItem> expeditionItemPool =
            new List<GamePanel_ExpeditionStateItem>();

        private UIPanel_Game gamePanel;
        private GameSystem gameSystem;
        private InventoryService inventory;
        private DynastyService dynasty;
        private bool subscribedToExpeditions;
        private bool subscribedToInventory;
        private bool subscribedToDynasty;
        private string selectedDestinationId = string.Empty;

        private void Reset()
        {
            destinationRoot = transform as RectTransform;
            destinationItemPrefab = GetComponentInChildren<GamePanel_ExpeditionDestinationItem>(true);
            supplyInputPrefab = GetComponentInChildren<GamePanel_ExpeditionSupplyInput>(true);
            expeditionItemPrefab = GetComponentInChildren<GamePanel_ExpeditionStateItem>(true);
        }

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeRuntime();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeRuntime();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            UnsubscribeRuntime();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ResolveReferences();
            SubscribeRuntime();
            Refresh();
        }

        public void Hide()
        {
            UnsubscribeRuntime();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            ResolveReferences();
            RefreshPenalty();
            RefreshDestinations();
            RefreshSelectedDestination();
            RefreshExpeditions();
        }

        private void ResolveReferences()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            gameSystem = GameSystem.Instance;
            inventory = gameSystem == null ? null : gameSystem.Inventory;
            dynasty = gameSystem == null ? null : gameSystem.Dynasty;
        }

        private void BindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            if (launchButton != null)
            {
                launchButton.onClick.RemoveListener(HandleLaunchClicked);
                launchButton.onClick.AddListener(HandleLaunchClicked);
            }

            if (populationInput != null)
            {
                populationInput.onValueChanged.RemoveListener(HandleInputChanged);
                populationInput.onValueChanged.AddListener(HandleInputChanged);
            }
        }

        private void UnbindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }

            if (launchButton != null)
            {
                launchButton.onClick.RemoveListener(HandleLaunchClicked);
            }

            if (populationInput != null)
            {
                populationInput.onValueChanged.RemoveListener(HandleInputChanged);
            }
        }

        private void SubscribeRuntime()
        {
            if (!subscribedToExpeditions && gameSystem != null)
            {
                gameSystem.ExpeditionsChanged += HandleExpeditionsChanged;
                subscribedToExpeditions = true;
            }

            if (!subscribedToInventory && inventory != null)
            {
                inventory.InventoryChanged += HandleInventoryChanged;
                subscribedToInventory = true;
            }

            if (!subscribedToDynasty && dynasty != null)
            {
                dynasty.PopulationChanged += HandlePopulationChanged;
                subscribedToDynasty = true;
            }
        }

        private void UnsubscribeRuntime()
        {
            if (subscribedToExpeditions && gameSystem != null)
            {
                gameSystem.ExpeditionsChanged -= HandleExpeditionsChanged;
            }

            if (subscribedToInventory && inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }

            if (subscribedToDynasty && dynasty != null)
            {
                dynasty.PopulationChanged -= HandlePopulationChanged;
            }

            subscribedToExpeditions = false;
            subscribedToInventory = false;
            subscribedToDynasty = false;
        }

        private void RefreshDestinations()
        {
            ReleaseDestinationItems();
            if (gameSystem == null || destinationRoot == null || destinationItemPrefab == null)
            {
                SetEmptyDestination(true, "远征系统未初始化");
                return;
            }

            var destinations = gameSystem.GetExpeditionDestinations(includeUnavailableDestinations);
            EnsureSelectedDestination(destinations);

            var visibleCount = 0;
            for (var i = 0; i < destinations.Count; i++)
            {
                var availability = destinations[i];
                if (!availability.IsVisible || availability.Destination == null)
                {
                    continue;
                }

                var item = GetDestinationItem();
                item.Bind(
                    availability,
                    IsSelectedDestination(availability.Destination),
                    HandleDestinationClicked);
                activeDestinationItems.Add(item);
                visibleCount++;
            }

            SetEmptyDestination(visibleCount <= 0, "当前没有可显示的远征目的地");
        }

        private void RefreshSelectedDestination()
        {
            var destination = FindSelectedDestination();
            var availability = destination == null || gameSystem == null || gameSystem.Expeditions == null
                ? default
                : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn);

            if (destination == null)
            {
                SetText(selectedTitleLabel, "未选择目的地");
                SetText(selectedDescriptionLabel, string.Empty);
                SetText(selectedRuleLabel, string.Empty);
                SetText(successChancePreviewLabel, string.Empty);
                SetText(populationHintLabel, string.Empty);
                SetText(statusLabel, string.Empty);
                ReleaseSupplyInputs();
                SetActive(emptySupplyRoot, true);
                RefreshLaunchButton(false, "选择目的地");
                return;
            }

            SetText(selectedTitleLabel, destination.DisplayName);
            SetText(selectedDescriptionLabel, destination.Description);
            SetText(selectedRuleLabel, FormatDestinationRule(destination, availability));
            RefreshPopulationInputDefault(destination);
            RebuildSupplyInputs(destination);
            RefreshLaunchPreview(destination, availability);
        }

        private void RefreshExpeditions()
        {
            ReleaseExpeditionItems();
            if (gameSystem == null || expeditionRoot == null || expeditionItemPrefab == null)
            {
                SetEmptyExpedition(true, "远征系统未初始化");
                return;
            }

            var expeditions = gameSystem.ExpeditionStates;
            var visibleCount = 0;
            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition == null || (!showHistory && !expedition.IsActive))
                {
                    continue;
                }

                var item = GetExpeditionItem();
                item.Bind(expedition, gameSystem, HandleClaimClicked);
                activeExpeditionItems.Add(item);
                visibleCount++;
            }

            SetEmptyExpedition(visibleCount <= 0, "当前没有远征队");
        }

        private void RefreshPenalty()
        {
            if (gameSystem == null || !gameSystem.HasActiveExpeditionSubsidyPenalty)
            {
                SetText(penaltyLabel, string.Empty);
                return;
            }

            SetText(
                penaltyLabel,
                $"补贴不足惩罚 {gameSystem.ExpeditionSubsidyPenaltyStacks} 层，持续至第 {gameSystem.ExpeditionSubsidyPenaltyActiveUntilTurn} 回合");
        }

        private void RebuildSupplyInputs(ExpeditionDestinationDefinition destination)
        {
            ReleaseSupplyInputs();
            if (destination == null || supplyRoot == null || supplyInputPrefab == null)
            {
                SetActive(emptySupplyRoot, true);
                return;
            }

            var options = destination.SupplyOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null || !option.IsValid)
                {
                    continue;
                }

                var input = GetSupplyInput();
                input.Bind(option, inventory, HandleSupplyChanged);
                activeSupplyInputs.Add(input);
            }

            SetActive(emptySupplyRoot, activeSupplyInputs.Count <= 0);
        }

        private void RefreshPopulationInputDefault(ExpeditionDestinationDefinition destination)
        {
            if (populationInput == null || destination == null || !string.IsNullOrWhiteSpace(populationInput.text))
            {
                return;
            }

            populationInput.SetTextWithoutNotify(destination.MinPopulation.ToString());
        }

        private void RefreshLaunchPreview(
            ExpeditionDestinationDefinition destination,
            ExpeditionDestinationAvailability availability)
        {
            if (destination == null)
            {
                SetText(successChancePreviewLabel, string.Empty);
                SetText(populationHintLabel, string.Empty);
                RefreshLaunchButton(false, "选择目的地");
                return;
            }

            var population = ParsePopulation();
            var successChance = destination.CalculateSuccessChance(population, BuildSupplyLookup());
            var availableBasePopulation = dynasty == null ? 0 : dynasty.BasePopulation;
            SetText(successChancePreviewLabel, $"预计成功率 {successChance * 100f:0.#}%");
            SetText(
                populationHintLabel,
                destination.HasMaxPopulation
                    ? $"基础人口 {availableBasePopulation}，需要 {destination.MinPopulation}-{destination.MaxPopulation}"
                    : $"基础人口 {availableBasePopulation}，至少 {destination.MinPopulation}");

            var canLaunch = availability.IsAvailable
                            && population >= destination.MinPopulation
                            && (!destination.HasMaxPopulation || population <= destination.MaxPopulation)
                            && availableBasePopulation >= population;
            var label = canLaunch ? "出发" : FormatLaunchBlockedReason(destination, availability, population, availableBasePopulation);
            RefreshLaunchButton(canLaunch, label);
            SetText(statusLabel, canLaunch ? string.Empty : label);
        }

        private void RefreshLaunchButton(bool interactable, string label)
        {
            if (launchButton != null)
            {
                launchButton.interactable = interactable;
            }

            SetText(launchButtonLabel, label);
        }

        private void HandleCloseClicked()
        {
            if (gamePanel != null)
            {
                gamePanel.Hide_Expedition();
                return;
            }

            Hide();
        }

        private void HandleLaunchClicked()
        {
            var destination = FindSelectedDestination();
            if (gameSystem == null || destination == null)
            {
                return;
            }

            var supplies = BuildAssignedSupplies();
            if (!gameSystem.TryStartExpedition(destination, ParsePopulation(), supplies, out var result))
            {
                SetText(statusLabel, result.Message);
                RefreshLaunchPreview(
                    destination,
                    gameSystem.Expeditions == null
                        ? default
                        : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
                return;
            }

            populationInput?.SetTextWithoutNotify(string.Empty);
            SetText(statusLabel, result.Message);
            Refresh();
        }

        private void HandleClaimClicked(ExpeditionState expedition)
        {
            if (gameSystem == null || expedition == null)
            {
                return;
            }

            if (!gameSystem.TryClaimExpeditionRewards(expedition.ExpeditionId, out var result))
            {
                SetText(statusLabel, result.Message);
                return;
            }

            SetText(statusLabel, result.Message);
            Refresh();
        }

        private void HandleDestinationClicked(ExpeditionDestinationDefinition destination)
        {
            selectedDestinationId = destination == null ? string.Empty : destination.DestinationId;
            if (populationInput != null)
            {
                populationInput.SetTextWithoutNotify(string.Empty);
            }

            Refresh();
        }

        private void HandleInputChanged(string _)
        {
            var destination = FindSelectedDestination();
            RefreshLaunchPreview(
                destination,
                gameSystem == null || gameSystem.Expeditions == null || destination == null
                    ? default
                    : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
        }

        private void HandleSupplyChanged()
        {
            RefreshLaunchPreview(
                FindSelectedDestination(),
                gameSystem == null || gameSystem.Expeditions == null
                    ? default
                    : gameSystem.Expeditions.EvaluateDestination(FindSelectedDestination(), gameSystem.CurrentTurn));
        }

        private void HandleExpeditionsChanged(GameSystem changedGameSystem)
        {
            gameSystem = changedGameSystem;
            Refresh();
        }

        private void HandleInventoryChanged(InventoryService changedInventory)
        {
            inventory = changedInventory;
            Refresh();
        }

        private void HandlePopulationChanged(DynastyService changedDynasty)
        {
            dynasty = changedDynasty;
            RefreshSelectedDestination();
        }

        private void EnsureSelectedDestination(IReadOnlyList<ExpeditionDestinationAvailability> destinations)
        {
            if (HasSelectedDestination(destinations))
            {
                return;
            }

            selectedDestinationId = string.Empty;
            if (destinations == null)
            {
                return;
            }

            for (var i = 0; i < destinations.Count; i++)
            {
                var availability = destinations[i];
                if (availability.Destination == null || !availability.IsAvailable)
                {
                    continue;
                }

                selectedDestinationId = availability.Destination.DestinationId;
                return;
            }

            for (var i = 0; i < destinations.Count; i++)
            {
                var availability = destinations[i];
                if (availability.Destination != null)
                {
                    selectedDestinationId = availability.Destination.DestinationId;
                    return;
                }
            }
        }

        private bool HasSelectedDestination(IReadOnlyList<ExpeditionDestinationAvailability> destinations)
        {
            if (string.IsNullOrWhiteSpace(selectedDestinationId) || destinations == null)
            {
                return false;
            }

            for (var i = 0; i < destinations.Count; i++)
            {
                var destination = destinations[i].Destination;
                if (destination != null
                    && string.Equals(destination.DestinationId, selectedDestinationId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private ExpeditionDestinationDefinition FindSelectedDestination()
        {
            if (gameSystem == null || gameSystem.ExpeditionDestinationCatalog == null || string.IsNullOrWhiteSpace(selectedDestinationId))
            {
                return null;
            }

            return gameSystem.ExpeditionDestinationCatalog.TryGetDestination(selectedDestinationId, out var destination)
                ? destination
                : null;
        }

        private bool IsSelectedDestination(ExpeditionDestinationDefinition destination)
        {
            return destination != null
                   && !string.IsNullOrWhiteSpace(selectedDestinationId)
                   && string.Equals(destination.DestinationId, selectedDestinationId, StringComparison.Ordinal);
        }

        private int ParsePopulation()
        {
            if (populationInput == null || string.IsNullOrWhiteSpace(populationInput.text))
            {
                return 0;
            }

            return int.TryParse(populationInput.text.Trim(), out var population)
                ? Mathf.Max(0, population)
                : 0;
        }

        private List<ItemAmount> BuildAssignedSupplies()
        {
            var result = new List<ItemAmount>();
            for (var i = 0; i < activeSupplyInputs.Count; i++)
            {
                var input = activeSupplyInputs[i];
                if (input != null && input.TryCreateItemAmount(out var itemAmount))
                {
                    result.Add(itemAmount);
                }
            }

            return result;
        }

        private Dictionary<string, int> BuildSupplyLookup()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < activeSupplyInputs.Count; i++)
            {
                var input = activeSupplyInputs[i];
                if (input == null || input.Option == null || input.Amount <= 0)
                {
                    continue;
                }

                var itemId = input.Option.ItemId;
                if (!result.ContainsKey(itemId))
                {
                    result.Add(itemId, 0);
                }

                result[itemId] += input.Amount;
            }

            return result;
        }

        private GamePanel_ExpeditionDestinationItem GetDestinationItem()
        {
            var lastIndex = destinationItemPool.Count - 1;
            GamePanel_ExpeditionDestinationItem item;
            if (lastIndex >= 0)
            {
                item = destinationItemPool[lastIndex];
                destinationItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(destinationItemPrefab);
            }

            item.transform.SetParent(destinationRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private GamePanel_ExpeditionSupplyInput GetSupplyInput()
        {
            var lastIndex = supplyInputPool.Count - 1;
            GamePanel_ExpeditionSupplyInput input;
            if (lastIndex >= 0)
            {
                input = supplyInputPool[lastIndex];
                supplyInputPool.RemoveAt(lastIndex);
            }
            else
            {
                input = Instantiate(supplyInputPrefab);
            }

            input.transform.SetParent(supplyRoot, false);
            input.gameObject.SetActive(true);
            return input;
        }

        private GamePanel_ExpeditionStateItem GetExpeditionItem()
        {
            var lastIndex = expeditionItemPool.Count - 1;
            GamePanel_ExpeditionStateItem item;
            if (lastIndex >= 0)
            {
                item = expeditionItemPool[lastIndex];
                expeditionItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(expeditionItemPrefab);
            }

            item.transform.SetParent(expeditionRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseDestinationItems()
        {
            for (var i = 0; i < activeDestinationItems.Count; i++)
            {
                var item = activeDestinationItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                item.transform.SetParent(destinationRoot, false);
                destinationItemPool.Add(item);
            }

            activeDestinationItems.Clear();
        }

        private void ReleaseSupplyInputs()
        {
            for (var i = 0; i < activeSupplyInputs.Count; i++)
            {
                var input = activeSupplyInputs[i];
                if (input == null)
                {
                    continue;
                }

                input.Unbind();
                input.gameObject.SetActive(false);
                input.transform.SetParent(supplyRoot, false);
                supplyInputPool.Add(input);
            }

            activeSupplyInputs.Clear();
        }

        private void ReleaseExpeditionItems()
        {
            for (var i = 0; i < activeExpeditionItems.Count; i++)
            {
                var item = activeExpeditionItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                item.transform.SetParent(expeditionRoot, false);
                expeditionItemPool.Add(item);
            }

            activeExpeditionItems.Clear();
        }

        private void SetEmptyDestination(bool visible, string message)
        {
            SetActive(emptyDestinationRoot, visible);
            SetText(emptyDestinationLabel, visible ? message : string.Empty);
        }

        private void SetEmptyExpedition(bool visible, string message)
        {
            SetActive(emptyExpeditionRoot, visible);
            SetText(emptyExpeditionLabel, visible ? message : string.Empty);
        }

        private static string FormatDestinationRule(
            ExpeditionDestinationDefinition destination,
            ExpeditionDestinationAvailability availability)
        {
            if (destination == null)
            {
                return string.Empty;
            }

            var maxPopulation = destination.HasMaxPopulation
                ? destination.MaxPopulation.ToString()
                : "不限";
            var status = availability.IsAvailable ? "可出发" : FormatAvailabilityReason(availability);
            return $"持续 {destination.DurationTurns} 回合，人口 {destination.MinPopulation}-{maxPopulation}，基础成功率 {destination.BaseSuccessChance * 100f:0.#}%，{status}";
        }

        private static string FormatLaunchBlockedReason(
            ExpeditionDestinationDefinition destination,
            ExpeditionDestinationAvailability availability,
            int population,
            int availableBasePopulation)
        {
            if (!availability.IsAvailable)
            {
                return FormatAvailabilityReason(availability);
            }

            if (population < destination.MinPopulation)
            {
                return "人口不足";
            }

            if (destination.HasMaxPopulation && population > destination.MaxPopulation)
            {
                return "人口超限";
            }

            if (availableBasePopulation < population)
            {
                return "基础人口不足";
            }

            return "不可出发";
        }

        private static string FormatAvailabilityReason(ExpeditionDestinationAvailability availability)
        {
            return availability.Reason switch
            {
                ExpeditionDestinationUnavailableReason.WindowClosed => "不在窗口期",
                ExpeditionDestinationUnavailableReason.ConditionLocked => "条件未满足",
                ExpeditionDestinationUnavailableReason.AlreadyCompleted => "已完成",
                _ => "不可用"
            };
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
