using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BuildingDetailsBlock_Function : MonoBehaviour
{
    [SerializeField] private TMP_Text txt_资源;
    [SerializeField] private TMP_Text txt_功能性;
    [SerializeField] private GameObject go_功能详情触发区;

    private readonly List<BuildingFunctionBlockEntry> sourceEntries = new List<BuildingFunctionBlockEntry>();
    private readonly List<AggregatedEntry> aggregatedEntries = new List<AggregatedEntry>();
    private readonly List<AggregatedEntry> sidebarAggregatedEntries = new List<AggregatedEntry>();
    private readonly List<BuildingDetailsSidebarRow> sidebarRows = new List<BuildingDetailsSidebarRow>();
    private Popup_BuildingDetails owner;
    private BuildingBase building;
    private bool detailTriggerBound;
    private bool detailSidebarVisible;

    public bool CanShow(BuildingBase targetBuilding)
    {
        return HasAnyEntry(targetBuilding);
    }

    public void Initialize(Popup_BuildingDetails detailOwner)
    {
        owner = detailOwner;
        ResolveTextFields();
        BindDetailTrigger();
    }

    public void Bind(BuildingBase targetBuilding)
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

    public void Refresh()
    {
        if (!CollectEntries())
        {
            SetBlockVisible(false);
            return;
        }

        RebuildAggregatedEntries();
        SetText(txt_资源, BuildGroupSummary(BuildingFunctionBlockGroup.Resource));
        SetText(txt_功能性, BuildGroupSummary(BuildingFunctionBlockGroup.Functionality));

        if (detailSidebarVisible)
        {
            ShowFunctionDetailSidebar();
        }
    }

    public void Unbind()
    {
        building = null;
        sourceEntries.Clear();
        aggregatedEntries.Clear();
        sidebarAggregatedEntries.Clear();
        sidebarRows.Clear();
        detailSidebarVisible = false;
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
        return group == BuildingFunctionBlockGroup.Functionality ? "功能性" : "资源";
    }

    private static string FormatSummaryEntry(AggregatedEntry entry)
    {
        var includePositiveSign = entry.Group == BuildingFunctionBlockGroup.Functionality;
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

    private void ResolveTextFields()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (txt_资源 == null && text.name.Contains("资源"))
            {
                txt_资源 = text;
                continue;
            }

            if (txt_功能性 == null && text.name.Contains("功能"))
            {
                txt_功能性 = text;
            }
        }

        if (txt_资源 == null && texts.Length > 0)
        {
            txt_资源 = texts[0];
        }

        if (txt_功能性 == null && texts.Length > 1)
        {
            txt_功能性 = texts[1];
        }
    }

    private void BindDetailTrigger()
    {
        if (detailTriggerBound)
        {
            return;
        }

        var triggerObject = go_功能详情触发区 == null ? gameObject : go_功能详情触发区;
        if (triggerObject == null)
        {
            return;
        }

        EnsureDetailTriggerRaycastTarget(triggerObject);

        var eventTrigger = triggerObject.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = triggerObject.AddComponent<EventTrigger>();
        }

        AddEventTrigger(eventTrigger, EventTriggerType.PointerEnter, HandleDetailPointerEnter);
        AddEventTrigger(eventTrigger, EventTriggerType.PointerExit, _ => HandleDetailPointerExit());
        AddEventTrigger(eventTrigger, EventTriggerType.PointerDown, HandleDetailPointerDown);
        AddEventTrigger(eventTrigger, EventTriggerType.PointerUp, HandleDetailPointerUp);
        detailTriggerBound = true;
    }

    private static void AddEventTrigger(
        EventTrigger eventTrigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        if (eventTrigger == null || callback == null)
        {
            return;
        }

        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(callback);
        eventTrigger.triggers.Add(entry);
    }

    private static void EnsureDetailTriggerRaycastTarget(GameObject triggerObject)
    {
        if (triggerObject == null || HasRaycastableGraphic(triggerObject))
        {
            return;
        }

        if (triggerObject.GetComponent<RectTransform>() == null)
        {
            return;
        }

        var image = triggerObject.GetComponent<Image>();
        if (image == null)
        {
            image = triggerObject.AddComponent<Image>();
        }

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
    }

    private static bool HasRaycastableGraphic(GameObject target)
    {
        var graphics = target.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && graphics[i].raycastTarget)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleDetailPointerEnter(BaseEventData eventData)
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

    private void HandleDetailPointerExit()
    {
        HideFunctionDetailSidebar();
    }

    private void HandleDetailPointerDown(BaseEventData eventData)
    {
        if (!IsTouchPointer(eventData) || building == null || !isActiveAndEnabled)
        {
            return;
        }

        ShowFunctionDetailSidebar();
    }

    private void HandleDetailPointerUp(BaseEventData eventData)
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

        detailSidebarVisible = true;
        RebuildSidebarRows();
        owner.ShowDetailSidebar(sidebarRows);
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
        detailSidebarVisible = false;
        if (owner != null)
        {
            owner.HideDetailSidebar();
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
