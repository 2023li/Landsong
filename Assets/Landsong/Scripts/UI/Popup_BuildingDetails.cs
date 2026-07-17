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
    [SerializeField, Required, LabelText("详情侧边栏对象")] private GameObject go_详情侧边栏;
    [SerializeField, Required, LabelText("详情侧边栏内容根节点")] private RectTransform root_详情侧边栏内容;
    [SerializeField, Required, LabelText("详情侧边栏文本预制体")] private TMP_Text prefab_详情侧边栏文本;

    [SerializeField, Required, LabelText("建筑名称文本")] private TMP_Text txt_建筑名称;
    [SerializeField, Required, LabelText("建筑描述文本")] private TMP_Text txt_建筑描述;
    [SerializeField, Required, LabelText("建筑图标")] private Image img_建筑图标;
    [SerializeField, Required, LabelText("关闭弹窗按钮")] private Button btn_关闭弹窗;

    [SerializeField, Required, LabelText("详情块列表")] private BuildingDetailsBlockBase[] detailBlocks = Array.Empty<BuildingDetailsBlockBase>();

    private readonly List<BuildingDetailsBlockBase> initializedDetailBlocks = new List<BuildingDetailsBlockBase>();
    private readonly List<TMP_Text> activeSidebarItems = new List<TMP_Text>();
    private readonly Stack<TMP_Text> sidebarItemPool = new Stack<TMP_Text>();
    private BuildingBase building;
    private BuildingDetailsBlockBase sidebarOwner;
    private Color defaultSidebarItemColor = Color.white;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        ValidatePopupReferences();
        InitializeDetailSidebarPool();
        InitializeDetailBlocks();

        if (btn_关闭弹窗 != null)
        {
            btn_关闭弹窗.onClick.AddListener(Hide);
        }

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
        HideDetailSidebarImmediately();
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
    }

    private void HandleBuildingStateChanged(BuildingBase changedBuilding)
    {
        if (changedBuilding == building && IsVisible)
        {
            Refresh();
        }
    }

    private void InitializeDetailBlocks()
    {
        initializedDetailBlocks.Clear();
        var serializedBlocks = new HashSet<BuildingDetailsBlockBase>();
        if (detailBlocks == null)
        {
            detailBlocks = Array.Empty<BuildingDetailsBlockBase>();
        }

        for (var i = 0; i < detailBlocks.Length; i++)
        {
            var block = detailBlocks[i];
            if (block == null)
            {
                Debug.LogError($"{nameof(Popup_BuildingDetails)} 的详情块列表第 {i} 项为空。", this);
                continue;
            }

            if (!serializedBlocks.Add(block))
            {
                Debug.LogError($"{nameof(Popup_BuildingDetails)} 的详情块列表重复引用 {block.name}。", this);
                continue;
            }

            if (block.transform != transform && !block.transform.IsChildOf(transform))
            {
                Debug.LogError($"详情块 {block.GetType().Name} 不属于当前详情面板层级。", block);
                continue;
            }

            if (!block.ValidateConfiguration(out var error))
            {
                Debug.LogError($"详情块 {block.GetType().Name} 配置不完整：{error}", block);
                block.gameObject.SetActive(false);
                continue;
            }

            block.Initialize(this);
            initializedDetailBlocks.Add(block);
        }

        var childBlocks = GetComponentsInChildren<BuildingDetailsBlockBase>(true);
        for (var i = 0; i < childBlocks.Length; i++)
        {
            var childBlock = childBlocks[i];
            if (childBlock != null && !serializedBlocks.Contains(childBlock))
            {
                Debug.LogError(
                    $"详情块 {childBlock.GetType().Name} 未加入 {nameof(Popup_BuildingDetails)} 的详情块列表。",
                    childBlock);
                childBlock.gameObject.SetActive(false);
            }
        }
    }

    private void ValidatePopupReferences()
    {
        var missing = new List<string>();
        AddMissingReference(missing, go_详情侧边栏, nameof(go_详情侧边栏));
        AddMissingReference(missing, root_详情侧边栏内容, nameof(root_详情侧边栏内容));
        AddMissingReference(missing, prefab_详情侧边栏文本, nameof(prefab_详情侧边栏文本));
        AddMissingReference(missing, txt_建筑名称, nameof(txt_建筑名称));
        AddMissingReference(missing, txt_建筑描述, nameof(txt_建筑描述));
        AddMissingReference(missing, img_建筑图标, nameof(img_建筑图标));
        AddMissingReference(missing, btn_关闭弹窗, nameof(btn_关闭弹窗));
        if (detailBlocks == null || detailBlocks.Length == 0)
        {
            missing.Add(nameof(detailBlocks));
        }

        if (missing.Count > 0)
        {
            Debug.LogError($"{nameof(Popup_BuildingDetails)} 缺少必需引用：{string.Join("、", missing)}", this);
        }

    }

    private static void AddMissingReference(List<string> missing, UnityEngine.Object target, string fieldName)
    {
        if (target == null)
        {
            missing.Add(fieldName);
        }
    }

    private void RefreshDetailBlocks(BuildingBase targetBuilding)
    {
        for (var i = 0; i < initializedDetailBlocks.Count; i++)
        {
            var block = initializedDetailBlocks[i];
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
        for (var i = 0; i < initializedDetailBlocks.Count; i++)
        {
            if (initializedDetailBlocks[i] != null)
            {
                initializedDetailBlocks[i].Unbind();
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

    public bool IsDetailSidebarOwner(BuildingDetailsBlockBase source)
    {
        return source != null && sidebarOwner == source;
    }

    public void ShowDetailSidebar(
        BuildingDetailsBlockBase source,
        IReadOnlyList<BuildingDetailsSidebarRow> rows)
    {
        if (source == null || !initializedDetailBlocks.Contains(source))
        {
            return;
        }

        sidebarOwner = source;
        RebuildDetailSidebar(rows);
        SetDetailSidebarVisible(true);
        RebuildDetailSidebarLayout();
    }

    public void HideDetailSidebar(BuildingDetailsBlockBase source)
    {
        if (source == null || sidebarOwner != source)
        {
            return;
        }

        HideDetailSidebarImmediately();
    }

    private void HideDetailSidebarImmediately()
    {
        sidebarOwner = null;
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

        var item = GetDetailSidebarItem();
        if (item == null)
        {
            return;
        }

        item.color = defaultSidebarItemColor;
        item.text = FormatSidebarRow(row);
        if (row.HasSignedValue)
        {
            item.color = ResolveSignedColor(row.SignedValue, item.color);
        }

        item.gameObject.SetActive(true);
        activeSidebarItems.Add(item);
    }

    private void ClearDetailSidebarRoot()
    {
        for (var i = 0; i < activeSidebarItems.Count; i++)
        {
            var item = activeSidebarItems[i];
            if (item == null)
            {
                continue;
            }

            item.text = string.Empty;
            item.color = defaultSidebarItemColor;
            item.gameObject.SetActive(false);
            sidebarItemPool.Push(item);
        }

        activeSidebarItems.Clear();
    }

    private void InitializeDetailSidebarPool()
    {
        activeSidebarItems.Clear();
        sidebarItemPool.Clear();
        if (prefab_详情侧边栏文本 != null)
        {
            defaultSidebarItemColor = prefab_详情侧边栏文本.color;
        }

        if (root_详情侧边栏内容 == null)
        {
            return;
        }

        for (var i = 0; i < root_详情侧边栏内容.childCount; i++)
        {
            var child = root_详情侧边栏内容.GetChild(i);
            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            if (!child.TryGetComponent<TMP_Text>(out var item))
            {
                Debug.LogError($"侧栏容器子对象 '{child.name}' 缺少 {nameof(TMP_Text)}，无法加入对象池。", child);
                continue;
            }

            item.text = string.Empty;
            item.color = defaultSidebarItemColor;
            sidebarItemPool.Push(item);
        }
    }

    private TMP_Text GetDetailSidebarItem()
    {
        while (sidebarItemPool.Count > 0)
        {
            var pooledItem = sidebarItemPool.Pop();
            if (pooledItem != null)
            {
                return pooledItem;
            }
        }

        return prefab_详情侧边栏文本 == null || root_详情侧边栏内容 == null
            ? null
            : Instantiate(prefab_详情侧边栏文本, root_详情侧边栏内容);
    }

    private void RebuildDetailSidebarLayout()
    {
        if (root_详情侧边栏内容 != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(root_详情侧边栏内容);
        }

        if (go_详情侧边栏 != null && go_详情侧边栏.transform is RectTransform sidebarRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(sidebarRect);
        }
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

}
