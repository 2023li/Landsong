using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.UISystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Popup_BuildingDetails : MonoBehaviour
{
    [SerializeField, LabelText("详情侧边栏对象")] private GameObject go_详情侧边栏;
    [SerializeField, LabelText("详情侧边栏内容根节点")] private RectTransform root_详情侧边栏内容;
    [SerializeField, LabelText("详情侧边栏文本预制体")] private GameObject prefab_详情侧边栏文本;

    [SerializeField, LabelText("建筑名称文本")] private TMP_Text txt_建筑名称;
    [SerializeField, LabelText("建筑描述文本")] private TMP_Text txt_建筑描述;
    [SerializeField, LabelText("建筑图标")] private Image img_建筑图标;
    [SerializeField, LabelText("关闭弹窗按钮")] private Button btn_关闭弹窗;

    [SerializeField, LabelText("内容栏根节点")] private RectTransform root_内容栏;
    [SerializeField, LabelText("标题预制体")] private GameObject prefab_标题;
    [SerializeField, LabelText("内容文本预制体")] private GameObject prefab_内容文本;
    [SerializeField, LabelText("详情块列表")] private BuildingDetailsBlockBase[] detailBlocks = Array.Empty<BuildingDetailsBlockBase>();

    private readonly List<GameObject> activeContentItems = new List<GameObject>();
    private readonly List<GameObject> activeSidebarItems = new List<GameObject>();
    private BuildingBase building;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        ResolveDetailBlocks();

        if (btn_关闭弹窗 != null)
        {
            btn_关闭弹窗.onClick.AddListener(Hide);
        }

        ClearContentRoot();
        ClearDetailSidebarRoot();
        Hide();
    }

    private void OnDestroy()
    {
        UnsubscribeBuilding();

        if (btn_关闭弹窗 != null)
        {
            btn_关闭弹窗.onClick.RemoveListener(Hide);
        }

    }

    public void ShowBuilding(BuildingBase targetBuilding)
    {
        if (targetBuilding == null)
        {
            Hide();
            return;
        }

        if (building != targetBuilding)
        {
            UnsubscribeBuilding();
            building = targetBuilding;
            building.StateChanged += HandleBuildingStateChanged;
        }

        Refresh();
        SetActive(gameObject, true);
    }

    public void Hide()
    {
        UnsubscribeBuilding();
        building = null;
        UnbindDetailBlocks();

        SetText(txt_建筑名称, string.Empty);
        SetText(txt_建筑描述, string.Empty);
        SetIcon(null);
        HideDetailSidebar();
        ClearContentRoot();
        SetActive(gameObject, false);
    }

    private void Refresh()
    {
        if (building == null)
        {
            return;
        }

        BuildingStatusDisplayData displayData = BuildingStatusUIFormatter.CreateDisplayData(building);
        SetText(txt_建筑名称, displayData.BuildingName);
        SetText(txt_建筑描述, BuildDescriptionText(building, displayData));
        SetIcon(building.Definition == null ? null : building.Definition.Icon);
        RefreshDetailBlocks(building);
        ClearContentRoot();
    }

    public void RefreshCurrentBuildingDetails()
    {
        if (building != null)
        {
            Refresh();
        }
    }

    private void HandleBuildingStateChanged(BuildingBase changedBuilding)
    {
        if (changedBuilding == building && IsVisible)
        {
            Refresh();
        }
    }

    private void ResolveDetailBlocks()
    {
        EnsureFallbackBlockComponent<BuildingDetailsBlock_Function>("info_功能");
        EnsureFallbackBlockComponent<BuildingDetailsBlock_Level>("info_等级");

        var resolvedBlocks = new List<BuildingDetailsBlockBase>();
        AddDetailBlocks(detailBlocks, resolvedBlocks);
        AddDetailBlocks(GetComponentsInChildren<BuildingDetailsBlockBase>(true), resolvedBlocks);
        detailBlocks = resolvedBlocks.ToArray();

        for (var i = 0; i < detailBlocks.Length; i++)
        {
            if (detailBlocks[i] != null)
            {
                detailBlocks[i].Initialize(this);
            }
        }
    }

    private void EnsureFallbackBlockComponent<TBlock>(string childName)
        where TBlock : BuildingDetailsBlockBase
    {
        if (GetComponentInChildren<TBlock>(true) != null)
        {
            return;
        }

        var blockRoot = FindChildByName(transform, childName);
        if (blockRoot != null)
        {
            blockRoot.gameObject.AddComponent<TBlock>();
        }
    }

    private static void AddDetailBlocks(
        IReadOnlyList<BuildingDetailsBlockBase> source,
        List<BuildingDetailsBlockBase> target)
    {
        if (source == null || target == null)
        {
            return;
        }

        for (var i = 0; i < source.Count; i++)
        {
            var block = source[i];
            if (block != null && !target.Contains(block))
            {
                target.Add(block);
            }
        }
    }

    private void RefreshDetailBlocks(BuildingBase targetBuilding)
    {
        if (detailBlocks == null)
        {
            return;
        }

        for (var i = 0; i < detailBlocks.Length; i++)
        {
            var block = detailBlocks[i];
            if (block == null)
            {
                continue;
            }

            if (block.CanShow(targetBuilding))
            {
                block.Bind(targetBuilding);
                continue;
            }

            block.Unbind();
        }
    }

    private void UnbindDetailBlocks()
    {
        if (detailBlocks == null)
        {
            return;
        }

        for (var i = 0; i < detailBlocks.Length; i++)
        {
            if (detailBlocks[i] != null)
            {
                detailBlocks[i].Unbind();
            }
        }
    }

    private void UnsubscribeBuilding()
    {
        if (building != null)
        {
            building.StateChanged -= HandleBuildingStateChanged;
        }
    }

    public void ShowDetailSidebar(IReadOnlyList<BuildingDetailsSidebarRow> rows)
    {
        RebuildDetailSidebar(rows);
        SetDetailSidebarVisible(true);
    }

    public void HideDetailSidebar()
    {
        SetDetailSidebarVisible(false);
        ClearDetailSidebarRoot();
    }

    private void RebuildDetailSidebar(IReadOnlyList<BuildingDetailsSidebarRow> rows)
    {
        ClearDetailSidebarRoot();
        if (root_详情侧边栏内容 == null || prefab_详情侧边栏文本 == null)
        {
            return;
        }

        if (rows == null || rows.Count == 0)
        {
            AddDetailSidebarText(new BuildingDetailsSidebarRow("暂无详情", string.Empty));
            return;
        }

        var hasAnyRow = false;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!row.IsValid)
            {
                continue;
            }

            AddDetailSidebarText(row);
            hasAnyRow = true;
        }

        if (!hasAnyRow)
        {
            AddDetailSidebarText(new BuildingDetailsSidebarRow("暂无详情", string.Empty));
        }
    }

    private void AddDetailSidebarText(BuildingDetailsSidebarRow row)
    {
        if (prefab_详情侧边栏文本 == null || root_详情侧边栏内容 == null)
        {
            return;
        }

        var item = Instantiate(prefab_详情侧边栏文本, root_详情侧边栏内容);
        item.SetActive(true);
        activeSidebarItems.Add(item);

        var texts = item.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0)
        {
            return;
        }

        if (texts.Length == 1)
        {
            texts[0].text = FormatSidebarRow(row);
            if (row.HasSignedValue)
            {
                texts[0].color = ResolveSignedColor(row.SignedValue, texts[0].color);
            }

            return;
        }

        texts[0].text = row.Label ?? string.Empty;
        texts[1].text = row.Value ?? string.Empty;
        if (row.HasSignedValue)
        {
            texts[1].color = ResolveSignedColor(row.SignedValue, texts[1].color);
        }
    }

    private void ClearDetailSidebarRoot()
    {
        for (var i = 0; i < activeSidebarItems.Count; i++)
        {
            DestroyIfNeeded(activeSidebarItems[i]);
        }

        activeSidebarItems.Clear();
    }

    private GameObject InstantiateContent(GameObject prefab)
    {
        if (prefab == null || root_内容栏 == null)
        {
            return null;
        }

        GameObject item = Instantiate(prefab, root_内容栏);
        item.SetActive(true);
        activeContentItems.Add(item);
        return item;
    }

    private void ClearContentRoot()
    {
        for (int i = 0; i < activeContentItems.Count; i++)
        {
            DestroyIfNeeded(activeContentItems[i]);
        }

        activeContentItems.Clear();
    }

    private void SetDetailSidebarVisible(bool visible)
    {
        var target = go_详情侧边栏 != null
            ? go_详情侧边栏
            : (root_详情侧边栏内容 == null ? null : root_详情侧边栏内容.gameObject);
        if (target == null)
        {
            return;
        }

        SetActive(target, visible);
    }

    private void SetIcon(Sprite sprite)
    {
        if (img_建筑图标 == null)
        {
            return;
        }

        img_建筑图标.sprite = sprite;
        img_建筑图标.enabled = sprite != null;
    }

    private static string BuildDescriptionText(BuildingBase targetBuilding, BuildingStatusDisplayData displayData)
    {
        if (!string.IsNullOrWhiteSpace(displayData.BaseInfoText))
        {
            return displayData.BaseInfoText;
        }

        if (!string.IsNullOrWhiteSpace(displayData.StatusInfoText))
        {
            return $"状态：{displayData.StatusInfoText}";
        }

        return targetBuilding == null ? string.Empty : targetBuilding.FamilyId;
    }

    private static string FormatSidebarRow(BuildingDetailsSidebarRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Label))
        {
            return row.Value ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(row.Value))
        {
            return row.Label;
        }

        return $"{row.Label}：{row.Value}";
    }

    private static Color ResolveSignedColor(float value, Color fallback)
    {
        if (value > 0f)
        {
            return new Color(0.25f, 0.75f, 0.35f, fallback.a);
        }

        if (value < 0f)
        {
            return new Color(0.9f, 0.25f, 0.25f, fallback.a);
        }

        return fallback;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name == childName)
            {
                return child;
            }

            var nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
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

    private static void DestroyIfNeeded(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

}
