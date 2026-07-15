using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BuildingDetailsBlock_Function : BuildingDetailsBlockBase,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField, Required, LabelText("资源文本")] private TMP_Text txt_资源;
    [SerializeField, Required, LabelText("功能性文本")] private TMP_Text txt_功能性;

    private readonly List<BuildingFunctionBlockEntry> sourceEntries = new List<BuildingFunctionBlockEntry>();
    private readonly List<AggregatedEntry> aggregatedEntries = new List<AggregatedEntry>();
    private readonly List<AggregatedEntry> sidebarAggregatedEntries = new List<AggregatedEntry>();
    private readonly List<BuildingDetailsSidebarRow> sidebarRows = new List<BuildingDetailsSidebarRow>();
    private Popup_BuildingDetails owner;
    private BuildingBase building;

    public override bool ValidateConfiguration(out string error)
    {
        var missing = new List<string>();
        AddMissingReference(missing, txt_资源, nameof(txt_资源));
        AddMissingReference(missing, txt_功能性, nameof(txt_功能性));
        return BuildValidationResult(missing, out error);
    }

    public override bool CanShow(BuildingBase targetBuilding)
    {
        return HasAnyEntry(targetBuilding);
    }

    public override void Initialize(Popup_BuildingDetails detailOwner)
    {
        owner = detailOwner;
    }

    public override void Bind(BuildingBase targetBuilding)
    {
        building = targetBuilding;
        if (!HasAnyEntry(building))
        {
            Unbind();
            return;
        }

        SetBlockVisible(true);
        Refresh();
    }

    public override void Refresh()
    {
        if (!CollectEntries())
        {
            SetBlockVisible(false);
            return;
        }

        RebuildAggregatedEntries();
        SetText(txt_资源, BuildGroupSummary(BuildingFunctionBlockGroup.资源组));
        SetText(txt_功能性, BuildGroupSummary(BuildingFunctionBlockGroup.功能性));

        if (owner != null && owner.IsDetailSidebarOwner(this))
        {
            ShowFunctionDetailSidebar();
        }
    }

    public override void Unbind()
    {
        building = null;
        sourceEntries.Clear();
        aggregatedEntries.Clear();
        sidebarAggregatedEntries.Clear();
        sidebarRows.Clear();
        SetText(txt_资源, string.Empty);
        SetText(txt_功能性, string.Empty);
        HideFunctionDetailSidebar();
        SetBlockVisible(false);
    }

    private bool HasAnyEntry(BuildingBase targetBuilding)
    {
        if (targetBuilding == null)
        {
            return false;
        }

        var entries = targetBuilding.GetFunctionBlockEntries();
        if (entries == null)
        {
            return false;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsValid)
            {
                return true;
            }
        }

        return false;
    }

    private bool CollectEntries()
    {
        sourceEntries.Clear();
        if (building == null)
        {
            return false;
        }

        var entries = building.GetFunctionBlockEntries();
        if (entries == null)
        {
            return false;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.IsValid)
            {
                sourceEntries.Add(entry);
            }
        }

        return sourceEntries.Count > 0;
    }

    private void RebuildAggregatedEntries()
    {
        aggregatedEntries.Clear();
        for (var i = 0; i < sourceEntries.Count; i++)
        {
            var entry = sourceEntries[i];
            var existingIndex = FindAggregatedEntryIndex(entry);
            if (existingIndex < 0)
            {
                aggregatedEntries.Add(new AggregatedEntry(
                    entry.Group,
                    entry.DisplayName,
                    entry.Amount));
                continue;
            }

            var existing = aggregatedEntries[existingIndex];
            aggregatedEntries[existingIndex] = new AggregatedEntry(
                existing.Group,
                existing.DisplayName,
                existing.Amount + entry.Amount);
        }

        for (var i = aggregatedEntries.Count - 1; i >= 0; i--)
        {
            if (aggregatedEntries[i].Amount == 0)
            {
                aggregatedEntries.RemoveAt(i);
            }
        }
    }

    private int FindAggregatedEntryIndex(BuildingFunctionBlockEntry entry)
    {
        for (var i = 0; i < aggregatedEntries.Count; i++)
        {
            var current = aggregatedEntries[i];
            if (current.Group == entry.Group
                && string.Equals(current.DisplayName, entry.DisplayName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private string BuildGroupSummary(BuildingFunctionBlockGroup group)
    {
        var content = string.Empty;
        for (var i = 0; i < aggregatedEntries.Count; i++)
        {
            var entry = aggregatedEntries[i];
            if (entry.Group != group)
            {
                continue;
            }

            if (content.Length > 0)
            {
                content += "、";
            }

            content += FormatSummaryEntry(entry);
        }

        if (content.Length == 0)
        {
            return string.Empty;
        }

        return $"{GetGroupLabel(group)}：{content}";
    }

    private static string GetGroupLabel(BuildingFunctionBlockGroup group)
    {
        return group == BuildingFunctionBlockGroup.功能性 ? "功能性" : "资源";
    }

    private static string FormatSummaryEntry(AggregatedEntry entry)
    {
        var includePositiveSign = entry.Group == BuildingFunctionBlockGroup.功能性;
        return FormatSignedAmount(entry.Amount, entry.DisplayName, includePositiveSign);
    }

    private static string FormatSidebarEntry(AggregatedEntry entry)
    {
        return FormatSignedAmount(entry.Amount, entry.DisplayName, true);
    }

    private static string FormatSignedAmount(int amount, string displayName, bool includePositiveSign)
    {
        var prefix = amount < 0 ? "-" : includePositiveSign ? "+" : string.Empty;
        return $"{prefix}{Mathf.Abs(amount)}{displayName}";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsTouchPointer(eventData) || building == null)
        {
            return;
        }

        ShowFunctionDetailSidebar();
    }

    private static bool IsTouchPointer(BaseEventData eventData)
    {
        var pointerEventData = eventData as PointerEventData;
        return pointerEventData != null
               && pointerEventData.pointerId >= 0
               && (Application.isMobilePlatform || Input.touchCount > 0);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideFunctionDetailSidebar();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsTouchPointer(eventData) || building == null || !isActiveAndEnabled)
        {
            return;
        }

        ShowFunctionDetailSidebar();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsTouchPointer(eventData))
        {
            return;
        }

        HideFunctionDetailSidebar();
    }

    private void ShowFunctionDetailSidebar()
    {
        if (owner == null || !CollectEntries())
        {
            return;
        }

        RebuildSidebarRows();
        owner.ShowDetailSidebar(this, sidebarRows);
    }

    private void RebuildSidebarRows()
    {
        sidebarRows.Clear();
        sidebarAggregatedEntries.Clear();
        for (var i = 0; i < sourceEntries.Count; i++)
        {
            var entry = sourceEntries[i];
            if (!entry.IsValid)
            {
                continue;
            }

            if (entry.HasSidebarRows && AppendEntrySidebarRows(entry))
            {
                continue;
            }

            AddSidebarAggregatedEntry(entry);
        }

        for (var i = 0; i < sidebarAggregatedEntries.Count; i++)
        {
            var entry = sidebarAggregatedEntries[i];
            if (entry.Amount == 0)
            {
                continue;
            }

            sidebarRows.Add(new BuildingDetailsSidebarRow(
                FormatSidebarEntry(entry),
                string.Empty,
                entry.Amount,
                true));
        }
    }

    private void AddSidebarAggregatedEntry(BuildingFunctionBlockEntry entry)
    {
        for (var i = 0; i < sidebarAggregatedEntries.Count; i++)
        {
            var current = sidebarAggregatedEntries[i];
            if (current.Group != entry.Group
                || !string.Equals(current.DisplayName, entry.DisplayName, StringComparison.Ordinal))
            {
                continue;
            }

            sidebarAggregatedEntries[i] = new AggregatedEntry(
                current.Group,
                current.DisplayName,
                current.Amount + entry.Amount);
            return;
        }

        sidebarAggregatedEntries.Add(new AggregatedEntry(
            entry.Group,
            entry.DisplayName,
            entry.Amount));
    }

    private bool AppendEntrySidebarRows(BuildingFunctionBlockEntry entry)
    {
        var rows = entry.SidebarRows;
        if (rows == null)
        {
            return false;
        }

        var hasAnyRow = false;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!row.IsValid)
            {
                continue;
            }

            sidebarRows.Add(new BuildingDetailsSidebarRow(
                row.Label,
                row.Value,
                row.SignedValue,
                row.HasSignedValue));
            hasAnyRow = true;
        }

        return hasAnyRow;
    }

    private void HideFunctionDetailSidebar()
    {
        if (owner != null)
        {
            owner.HideDetailSidebar(this);
        }
    }

    private void SetBlockVisible(bool visible)
    {
        SetActive(gameObject, visible);
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
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private readonly struct AggregatedEntry
    {
        public AggregatedEntry(
            BuildingFunctionBlockGroup group,
            string displayName,
            int amount)
        {
            Group = group;
            DisplayName = displayName;
            Amount = amount;
        }

        public BuildingFunctionBlockGroup Group { get; }

        public string DisplayName { get; }

        public int Amount { get; }
    }
}
