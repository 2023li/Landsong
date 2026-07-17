using System;
using System.Collections.Generic;
using System.Text;
using Landsong;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Landsong.Localization;
using Landsong.TurnSystem;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GamePanel_EconomyForecast : MonoBehaviour
{
    private const string RiskColor = "#E66A5C";
    private const string PositiveColor = "#75B56F";
    private const string MutedColor = "#A8A8A8";

    [Header("预测范围")]
    [SerializeField, Range(1, 10)] private int forecastTurns = 5;
    [SerializeField, Min(1)] private int maxResourceRows = 12;
    [SerializeField, Min(1)] private int maxScheduledEvents = 20;
    [SerializeField, Min(1)] private int maxWarnings = 8;
    [SerializeField] private bool refreshOnEnable = true;

    [Header("概览")]
    [SerializeField] private TMP_Text summaryText;

    [Header("资源时间线")]
    [SerializeField] private TMP_Text resourceTimelineText;

    [Header("计划事件")]
    [SerializeField] private TMP_Text scheduledEventsText;

    [Header("居民与生活质量")]
    [SerializeField] private TMP_Text residentialForecastText;

    [Header("风险与预测说明")]
    [SerializeField] private TMP_Text warningText;

    private readonly HashSet<BuildingBase> subscribedBuildings = new HashSet<BuildingBase>();

    private GameSystem gameSystem;
    private InventoryService inventoryService;
    private BuildingService buildingService;
    private TurnService turnService;
    private bool subscribed;

    public int ForecastTurns => forecastTurns;

    private void OnEnable()
    {
        Landsong.Localization.L10n.LanguageChanged += RefreshForecast;
        ResolveServices();
        Subscribe();

        if (refreshOnEnable)
        {
            RefreshForecast();
        }
    }

    private void OnDisable()
    {
        Landsong.Localization.L10n.LanguageChanged -= RefreshForecast;
        Unsubscribe();
    }

    public void SetForecastTurns(int turns)
    {
        var normalized = Mathf.Clamp(turns, 1, 10);
        if (forecastTurns == normalized)
        {
            return;
        }

        forecastTurns = normalized;
        if (isActiveAndEnabled)
        {
            RefreshForecast();
        }
    }

    public void RefreshForecast()
    {
        ResolveServices();
        var service = gameSystem?.Services.EconomyForecast;
        if (service == null)
        {
            RenderUnavailable(L10n.Gameplay("gameplay.economy.not_initialized", "经济预测服务未初始化。"));
            return;
        }

        var forecast = service.ForecastTurns(forecastTurns);
        RenderSummary(forecast);
        RenderResources(forecast);
        RenderScheduledEvents(forecast);
        RenderResidential(forecast);
        RenderWarnings(forecast);
    }

    private void RenderSummary(EconomyForecastTimelineResult forecast)
    {
        if (summaryText == null)
        {
            return;
        }

        var riskyResources = 0;
        var blockedEvents = 0;
        var earliestRiskTurn = int.MaxValue;
        for (var i = 0; i < forecast.ResourceTimelines.Count; i++)
        {
            var timeline = forecast.ResourceTimelines[i];
            if (!timeline.HasRisk)
            {
                continue;
            }

            riskyResources++;
            for (var turnIndex = 0; turnIndex < timeline.Turns.Count; turnIndex++)
            {
                if (timeline.Turns[turnIndex].HasRisk)
                {
                    earliestRiskTurn = Mathf.Min(earliestRiskTurn, timeline.Turns[turnIndex].TurnOffset);
                    break;
                }
            }
        }

        for (var i = 0; i < forecast.Events.Count; i++)
        {
            if (!forecast.Events[i].IsBlocked)
            {
                continue;
            }

            blockedEvents++;
            earliestRiskTurn = Mathf.Min(earliestRiskTurn, forecast.Events[i].TurnOffset);
        }

        var builder = new StringBuilder();
        builder.AppendLine(L10n.Gameplay(
            "gameplay.economy.summary.range",
            "未来 {0} 回合{1}",
            forecast.ForecastTurns,
            forecast.IsApproximate
                ? L10n.Gameplay("gameplay.economy.summary.conservative", " · 保守预测")
                : L10n.Gameplay("gameplay.economy.summary.exact", " · 精确预测")));
        builder.AppendLine(L10n.Gameplay(
            "gameplay.economy.summary.slots",
            "库存格 {0}/{1}  →  {2}/{1}（T+{3}）",
            forecast.OccupiedSlots,
            forecast.TotalSlots,
            forecast.ProjectedOccupiedSlots,
            forecast.ForecastTurns));

        if (riskyResources == 0 && blockedEvents == 0)
        {
            builder.Append(Colorize(L10n.Gameplay(
                "gameplay.economy.summary.no_risk",
                "预测范围内未发现明确的短缺或入库阻塞。"), PositiveColor));
        }
        else
        {
            builder.Append(Colorize(
                L10n.Gameplay(
                    "gameplay.economy.summary.risk",
                    "T+{0} 起存在风险：{1} 种资源异常，{2} 个计划事件受阻。",
                    earliestRiskTurn,
                    riskyResources,
                    blockedEvents),
                RiskColor));
        }

        summaryText.text = builder.ToString();
    }

    private void RenderResources(EconomyForecastTimelineResult forecast)
    {
        if (resourceTimelineText == null)
        {
            return;
        }

        if (forecast.ResourceTimelines.Count == 0)
        {
            resourceTimelineText.text = L10n.Gameplay("gameplay.economy.resources.empty", "预测范围内没有库存物品变化。");
            return;
        }

        var builder = new StringBuilder();
        var visibleCount = Mathf.Min(maxResourceRows, forecast.ResourceTimelines.Count);
        for (var i = 0; i < visibleCount; i++)
        {
            var timeline = forecast.ResourceTimelines[i];
            builder.Append("<b>")
                .Append(EscapeRichText(ResolveItemName(timeline.ItemId)))
                .Append("</b>  ")
                .Append(L10n.Gameplay("gameplay.economy.resources.current", "当前 {0}", timeline.CurrentQuantity))
                .AppendLine();

            for (var turnIndex = 0; turnIndex < timeline.Turns.Count; turnIndex++)
            {
                if (turnIndex > 0)
                {
                    builder.Append("  │  ");
                }

                AppendTurnCell(builder, timeline.Turns[turnIndex]);
            }

            if (i < visibleCount - 1)
            {
                builder.AppendLine().AppendLine();
            }
        }

        if (visibleCount < forecast.ResourceTimelines.Count)
        {
            builder.AppendLine()
                .AppendLine()
                .Append(Colorize(L10n.Gameplay(
                    "gameplay.economy.resources.more",
                    "另有 {0} 种资源未展开。",
                    forecast.ResourceTimelines.Count - visibleCount), MutedColor));
        }

        resourceTimelineText.text = builder.ToString();
    }

    private static void AppendTurnCell(StringBuilder builder, EconomyForecastResourceTurn turn)
    {
        var cell = new StringBuilder();
        cell.Append("T+").Append(turn.TurnOffset).Append(' ');
        if (turn.HasRange)
        {
            cell.Append(turn.MinimumProjectedQuantity)
                .Append('~')
                .Append(turn.MaximumProjectedQuantity);
        }
        else
        {
            cell.Append(turn.MinimumProjectedQuantity);
        }

        var details = new List<string>();
        if (turn.ExpectedConsumption > 0)
        {
            details.Add(L10n.Gameplay("gameplay.economy.cell.consume", "耗{0}", turn.ExpectedConsumption));
        }

        if (turn.MaximumProduction > 0)
        {
            details.Add(turn.MinimumProduction == turn.MaximumProduction
                ? L10n.Gameplay("gameplay.economy.cell.produce", "产{0}", turn.MaximumProduction)
                : L10n.Gameplay("gameplay.economy.cell.produce_range", "产{0}~{1}", turn.MinimumProduction, turn.MaximumProduction));
        }

        if (turn.ExpectedLoss > 0)
        {
            details.Add(L10n.Gameplay("gameplay.economy.cell.loss", "损{0}", turn.ExpectedLoss));
        }

        if (turn.Shortage > 0)
        {
            details.Add(L10n.Gameplay("gameplay.economy.cell.shortage", "缺{0}", turn.Shortage));
        }

        if (turn.Overflow > 0)
        {
            details.Add(L10n.Gameplay("gameplay.economy.cell.overflow", "溢{0}", turn.Overflow));
        }

        if (details.Count > 0)
        {
            cell.Append(L10n.Gameplay(
                "gameplay.economy.cell.details",
                "（{0}）",
                string.Join(L10n.Gameplay("gameplay.common.comma", "，"), details)));
        }

        builder.Append(turn.HasRisk
            ? Colorize(cell.ToString(), RiskColor)
            : cell.ToString());
    }

    private void RenderScheduledEvents(EconomyForecastTimelineResult forecast)
    {
        if (scheduledEventsText == null)
        {
            return;
        }

        if (forecast.Events.Count == 0)
        {
            scheduledEventsText.text = L10n.Gameplay("gameplay.economy.events.empty", "预测范围内没有周期产出、施工、收获或税收事件。");
            return;
        }

        var builder = new StringBuilder();
        var visibleCount = Mathf.Min(maxScheduledEvents, forecast.Events.Count);
        for (var i = 0; i < visibleCount; i++)
        {
            var forecastEvent = forecast.Events[i];
            var line = $"T+{forecastEvent.TurnOffset}  {GetEventKindLabel(forecastEvent.Kind)}  "
                       + $"{EscapeRichText(forecastEvent.BuildingDisplayName)}："
                       + $"{EscapeRichText(forecastEvent.Description)}"
                       + GetCertaintySuffix(forecastEvent.Certainty);

            builder.Append(forecastEvent.IsBlocked ? Colorize(line, RiskColor) : line);
            if (i < visibleCount - 1)
            {
                builder.AppendLine();
            }
        }

        if (visibleCount < forecast.Events.Count)
        {
            builder.AppendLine()
                .Append(Colorize(L10n.Gameplay(
                    "gameplay.economy.events.more",
                    "另有 {0} 个计划事件未展开。",
                    forecast.Events.Count - visibleCount), MutedColor));
        }

        scheduledEventsText.text = builder.ToString();
    }

    private void RenderResidential(EconomyForecastTimelineResult forecast)
    {
        if (residentialForecastText == null)
        {
            return;
        }

        if (forecast.ResidentialForecasts.Count == 0)
        {
            residentialForecastText.text = L10n.Gameplay("gameplay.economy.residential.empty", "当前没有居民饮食与生活质量预测。");
            return;
        }

        var rowsByBuilding = new Dictionary<string, List<ResidentialQualityForecast>>(StringComparer.Ordinal);
        var buildingOrder = new List<string>();
        for (var i = 0; i < forecast.ResidentialForecasts.Count; i++)
        {
            var row = forecast.ResidentialForecasts[i];
            var key = string.IsNullOrWhiteSpace(row.BuildingInstanceId)
                ? row.BuildingDisplayName
                : row.BuildingInstanceId;
            if (!rowsByBuilding.TryGetValue(key, out var rows))
            {
                rows = new List<ResidentialQualityForecast>();
                rowsByBuilding.Add(key, rows);
                buildingOrder.Add(key);
            }

            rows.Add(row);
        }

        var builder = new StringBuilder();
        for (var buildingIndex = 0; buildingIndex < buildingOrder.Count; buildingIndex++)
        {
            var rows = rowsByBuilding[buildingOrder[buildingIndex]];
            if (rows.Count == 0)
            {
                continue;
            }

            builder.Append("<b>")
                .Append(EscapeRichText(rows[0].BuildingDisplayName))
                .Append("</b>")
                .AppendLine();

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var line = L10n.Gameplay(
                    "gameplay.economy.residential.line",
                    "T+{0}  人口 {1}→{2}  饮食{3} · {4}类 · {5:0.#}分  生活质量 {6:0.#}→{7:0.#}",
                    row.TurnOffset,
                    row.Population,
                    row.PredictedPopulation,
                    row.FoodSatisfied
                        ? L10n.Gameplay("gameplay.economy.residential.satisfied", "满足")
                        : L10n.Gameplay("gameplay.economy.residential.insufficient", "不足"),
                    row.PredictedDietVariety,
                    row.PredictedDietScore,
                    row.CurrentLifeQuality,
                    row.PredictedLifeQuality);
                builder.Append(row.FoodSatisfied ? line : Colorize(line, RiskColor));
                if (rowIndex < rows.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            if (buildingIndex < buildingOrder.Count - 1)
            {
                builder.AppendLine().AppendLine();
            }
        }

        residentialForecastText.text = builder.ToString();
    }

    private void RenderWarnings(EconomyForecastTimelineResult forecast)
    {
        if (warningText == null)
        {
            return;
        }

        if (forecast.Warnings.Count == 0)
        {
            warningText.text = string.Empty;
            return;
        }

        var builder = new StringBuilder();
        var visibleCount = Mathf.Min(maxWarnings, forecast.Warnings.Count);
        for (var i = 0; i < visibleCount; i++)
        {
            builder.Append("• ").Append(EscapeRichText(forecast.Warnings[i]));
            if (i < visibleCount - 1)
            {
                builder.AppendLine();
            }
        }

        if (visibleCount < forecast.Warnings.Count)
        {
            builder.AppendLine().Append(L10n.Gameplay(
                "gameplay.economy.warnings.more",
                "• 另有 {0} 条说明未展开。",
                forecast.Warnings.Count - visibleCount));
        }

        warningText.text = builder.ToString();
    }

    private void RenderUnavailable(string message)
    {
        SetText(summaryText, message);
        SetText(resourceTimelineText, string.Empty);
        SetText(scheduledEventsText, string.Empty);
        SetText(residentialForecastText, string.Empty);
        SetText(warningText, string.Empty);
    }

    private void ResolveServices()
    {
        gameSystem = GameSystem.Instance;
        inventoryService = gameSystem?.Services.Inventory;
        buildingService = gameSystem?.Services.Buildings;
        turnService = gameSystem?.Services.Turn;
    }

    private void Subscribe()
    {
        Unsubscribe();

        if (inventoryService != null)
        {
            inventoryService.InventoryChanged += HandleInventoryChanged;
        }

        if (buildingService != null)
        {
            buildingService.BuildingsChanged += HandleBuildingsChanged;
        }

        if (turnService != null)
        {
            turnService.TurnAdvanced += HandleTurnAdvanced;
        }

        RefreshBuildingSubscriptions();
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed)
        {
            if (inventoryService != null)
            {
                inventoryService.InventoryChanged -= HandleInventoryChanged;
            }

            if (buildingService != null)
            {
                buildingService.BuildingsChanged -= HandleBuildingsChanged;
            }

            if (turnService != null)
            {
                turnService.TurnAdvanced -= HandleTurnAdvanced;
            }
        }

        UnsubscribeBuildingStates();
        subscribed = false;
    }

    private void RefreshBuildingSubscriptions()
    {
        UnsubscribeBuildingStates();
        var buildings = buildingService?.Buildings;
        if (buildings == null)
        {
            return;
        }

        for (var i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null || !subscribedBuildings.Add(building))
            {
                continue;
            }

            building.StateChanged += HandleBuildingStateChanged;
        }
    }

    private void UnsubscribeBuildingStates()
    {
        foreach (var building in subscribedBuildings)
        {
            if (building != null)
            {
                building.StateChanged -= HandleBuildingStateChanged;
            }
        }

        subscribedBuildings.Clear();
    }

    private void HandleInventoryChanged(InventoryService changedInventory)
    {
        inventoryService = changedInventory;
        RefreshUnlessTurnIsAdvancing();
    }

    private void HandleBuildingsChanged(BuildingService changedBuildings)
    {
        buildingService = changedBuildings;
        RefreshBuildingSubscriptions();
        RefreshUnlessTurnIsAdvancing();
    }

    private void HandleBuildingStateChanged(BuildingBase changedBuilding)
    {
        RefreshUnlessTurnIsAdvancing();
    }

    private void HandleTurnAdvanced(TurnService changedTurnService, TurnAdvanceSummary summary)
    {
        turnService = changedTurnService;
        RefreshForecast();
    }

    private void RefreshUnlessTurnIsAdvancing()
    {
        if (turnService == null || !turnService.IsAdvancingTurn)
        {
            RefreshForecast();
        }
    }

    private string ResolveItemName(string itemId)
    {
        var catalog = inventoryService?.ItemCatalog;
        return catalog != null
               && catalog.TryGetDefinition(itemId, out var definition)
               && definition != null
               && !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName.Trim()
            : itemId;
    }

    private static string GetEventKindLabel(EconomyForecastEventKind kind)
    {
        return kind switch
        {
            EconomyForecastEventKind.Construction => L10n.Gameplay("gameplay.economy.event.construction", "[施工]"),
            EconomyForecastEventKind.Production => L10n.Gameplay("gameplay.economy.event.production", "[生产]"),
            EconomyForecastEventKind.Processing => L10n.Gameplay("gameplay.economy.event.processing", "[加工]"),
            EconomyForecastEventKind.Harvest => L10n.Gameplay("gameplay.economy.event.harvest", "[收获]"),
            EconomyForecastEventKind.Tax => L10n.Gameplay("gameplay.economy.event.tax", "[税收]"),
            EconomyForecastEventKind.Market => L10n.Gameplay("gameplay.economy.event.market", "[市场]"),
            EconomyForecastEventKind.Population => L10n.Gameplay("gameplay.economy.event.population", "[人口]"),
            _ => L10n.Gameplay("gameplay.economy.event.risk", "[风险]")
        };
    }

    private static string GetCertaintySuffix(EconomyForecastCertainty certainty)
    {
        return certainty switch
        {
            EconomyForecastCertainty.Range => L10n.Gameplay("gameplay.economy.certainty.range", "  [范围]"),
            EconomyForecastCertainty.Conditional => L10n.Gameplay("gameplay.economy.certainty.conditional", "  [条件]"),
            EconomyForecastCertainty.Manual => L10n.Gameplay("gameplay.economy.certainty.manual", "  [需操作]"),
            _ => string.Empty
        };
    }

    private static string Colorize(string value, string color) =>
        $"<color={color}>{value}</color>";

    private static string EscapeRichText(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value ?? string.Empty;
        }
    }
}
