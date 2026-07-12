using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 市场：岗位运营由 BM_岗位运营 提供；本类只负责资源经手价值和金币结算。
/// </summary>
public sealed class Market : BuildingBase, IBuildingResourceProvisionAccounting, IBuildingTaxSource
{
    private const int RequiredWorkerCount = 3;
    private const string DefaultGoldItemId = "金币";
    private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
    private const string StatusInvalidGoldItem = BuildingRuntimeStatusCatalog.BS_金币配置异常;
    private const string StatusGoldIncomeFailed = BuildingRuntimeStatusCatalog.BS_市场收入存入失败;

    [TitleGroup("结算")]
    [SerializeField, LabelText("金币物品")] private string goldItemId = DefaultGoldItemId;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private long lastTurnProvidedResourceValue;
    [SerializeField, ReadOnly] private int lastTurnGoldIncome;
    [SerializeField, ReadOnly] private string lastAbnormalStatusId = string.Empty;
    [SerializeField, ReadOnly] private string lastAbnormalStatusText = string.Empty;

    private long providedResourceValueThisTurn;
    private IReadOnlyList<BuildingResourceChange> lastTaxRewards = EmptyResourceChanges;

    public bool IsResourceProviderOperational => Workforce.IsFullyStaffed;
    public long LastTurnProvidedResourceValue => lastTurnProvidedResourceValue;
    public int LastTurnGoldIncome => lastTurnGoldIncome;
    public IReadOnlyList<BuildingResourceChange> CurrentTaxRewards => EmptyResourceChanges;
    public IReadOnlyList<BuildingResourceChange> LastTaxRewards => lastTaxRewards;
    private BM_岗位运营 Workforce => EnsureWorkforceModule();

    public override string GetOverviewInfo()
    {
        return $"工人 {Workforce.CurrentWorkers}/{Workforce.MaxWorkers}，上回合 +{lastTurnGoldIncome} 金币";
    }

    public override IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
    {
        List<BuildingFunctionBlockEntry> entries = null;
        AddFunctionBlockEntry(
            ref entries,
            new BuildingFunctionBlockEntry(
                BuildingFunctionBlockGroup.功能性,
                "资源提供点",
                1,
                new[]
                {
                    new BuildingFunctionBlockSidebarRow("需要工人", $"{RequiredWorkerCount} 人"),
                    new BuildingFunctionBlockSidebarRow("提供优先级", ResourceProviderPriority.ToString()),
                    new BuildingFunctionBlockSidebarRow("上回合经手价值", lastTurnProvidedResourceValue.ToString()),
                    new BuildingFunctionBlockSidebarRow("金币结算", "总价值 × 10%（向下取整）")
                }));
        AppendBuildingModuleFunctionBlockEntries(ref entries);
        return entries ?? EmptyFunctionBlockEntries;
    }

    public override IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
    {
        List<BuildingRuntimeStatus> statuses = null;
        var workforce = Workforce;
        AppendRuntimeStatus(
            ref statuses,
            workforce.CurrentWorkers < workforce.MaxWorkers
                ? new BuildingRuntimeStatus(BuildingRuntimeStatusCatalog.BS_工人不足, "工人不足", workforce.CurrentWorkers, workforce.MaxWorkers)
                : default);
        AppendRuntimeStatus(
            ref statuses,
            string.IsNullOrWhiteSpace(lastAbnormalStatusId)
                ? default
                : new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText));
        AppendCommonRuntimeStatuses(ref statuses);
        return statuses ?? EmptyRuntimeStatuses;
    }

    protected override void OnInitialized()
    {
        EnsureWorkforceModule().Bind(this);
    }

    protected override void OnPlaced()
    {
        EnsureWorkforceModule().OnPlaced(this);
    }

    protected override void OnRegistered()
    {
        EnsureWorkforceModule().Bind(this);
    }

    protected override void OnUnregistered()
    {
        EnsureWorkforceModule().OnUnregistered(this);
    }

    protected override bool OnTurn()
    {
        return EnsureWorkforceModule().ProcessTurn(this);
    }

    public void BeginResourceProvisionTurn()
    {
        providedResourceValueThisTurn = 0;
        lastTurnProvidedResourceValue = 0;
        lastTurnGoldIncome = 0;
        lastTaxRewards = EmptyResourceChanges;
        lastAbnormalStatusId = string.Empty;
        lastAbnormalStatusText = string.Empty;
    }

    public void RecordProvidedResource(BuildingBase consumer, BuildingResourceChange resource)
    {
        var inventory = GameSystem?.Services.Inventory;
        if (!resource.IsValid || inventory?.ItemCatalog == null || !inventory.ItemCatalog.TryGetDefinition(resource.ItemId, out var definition) || definition == null)
        {
            return;
        }

        var value = (long)Mathf.Max(0, definition.BaseValue) * resource.Amount;
        providedResourceValueThisTurn = value > long.MaxValue - providedResourceValueThisTurn
            ? long.MaxValue
            : providedResourceValueThisTurn + value;
    }

    public void CompleteResourceProvisionTurn()
    {
        lastTurnProvidedResourceValue = providedResourceValueThisTurn;
        var income = (int)Math.Min(int.MaxValue, lastTurnProvidedResourceValue / 10L);
        if (income <= 0)
        {
            return;
        }

        var inventory = GameSystem?.Services.Inventory;
        if (inventory == null)
        {
            SetStatus(StatusMissingInventory, "库存服务缺失");
            return;
        }

        if (string.IsNullOrWhiteSpace(goldItemId))
        {
            SetStatus(StatusInvalidGoldItem, "金币配置异常");
            return;
        }

        if (!inventory.TryAddItem(goldItemId, income))
        {
            SetStatus(StatusGoldIncomeFailed, "金币存入失败");
            return;
        }

        lastTurnGoldIncome = income;
        lastTaxRewards = CreateResourceChanges(goldItemId, income);
        NotifyStateChanged();
    }

    protected override BuildingDataBase CaptureBuildingData()
    {
        return new MarketData
        {
            LastTurnProvidedResourceValue = lastTurnProvidedResourceValue,
            LastTurnGoldIncome = lastTurnGoldIncome,
            LastAbnormalStatusId = lastAbnormalStatusId,
            LastAbnormalStatusText = lastAbnormalStatusText
        };
    }

    protected override void RestoreBuildingData(BuildingDataBase data)
    {
        if (data is not MarketData marketData)
        {
            return;
        }

        lastTurnProvidedResourceValue = marketData.LastTurnProvidedResourceValue;
        lastTurnGoldIncome = marketData.LastTurnGoldIncome;
        lastTaxRewards = CreateResourceChanges(goldItemId, lastTurnGoldIncome);
        lastAbnormalStatusId = NormalizeText(marketData.LastAbnormalStatusId);
        lastAbnormalStatusText = NormalizeText(marketData.LastAbnormalStatusText);
    }

    private BM_岗位运营 EnsureWorkforceModule()
    {
        var module = EnsureBuildingModule<BM_岗位运营>();
        module.ConfigureDefaultsIfUnset(RequiredWorkerCount, RequiredWorkerCount, 100f, 10, true, RequiredWorkerCount, goldItemId);
        return module;
    }

    private void SetStatus(string id, string text)
    {
        lastAbnormalStatusId = NormalizeText(id);
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(text) ? lastAbnormalStatusId : text.Trim();
    }

    private static string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    [BuildingDataTypeId("building.market")]
    private sealed class MarketData : BuildingDataBase
    {
        public long LastTurnProvidedResourceValue;
        public int LastTurnGoldIncome;
        public string LastAbnormalStatusId;
        public string LastAbnormalStatusText;
    }
}
