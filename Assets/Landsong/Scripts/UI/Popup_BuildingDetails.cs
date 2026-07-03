using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Popup_BuildingDetails : MonoBehaviour
{
    [SerializeField] private GameObject go_补贴栏标题;
    [SerializeField] private GameObject go_补贴栏内容;
    [SerializeField] private Slider sld_补贴滑动条;

    [SerializeField] private TMP_Text txt_建筑名称;
    [SerializeField] private TMP_Text txt_建筑描述;
    [SerializeField] private Image img_建筑图标;
    [SerializeField] private Button btn_关闭弹窗;

    [SerializeField] private RectTransform root_内容栏;
    [SerializeField] private GameObject prefab_标题;
    [SerializeField] private GameObject prefab_内容文本;
    [SerializeField, Min(1)] private int maxSubsidyGoldPerTurn = 100;

    private readonly List<GameObject> activeContentItems = new List<GameObject>();
    private BuildingBase building;
    private IBuildingJobSource subsidySource;
    private bool suppressSubsidySliderCallback;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        if (btn_关闭弹窗 != null)
        {
            btn_关闭弹窗.onClick.AddListener(Hide);
        }

        if (sld_补贴滑动条 != null)
        {
            sld_补贴滑动条.wholeNumbers = true;
            sld_补贴滑动条.minValue = 0f;
            sld_补贴滑动条.maxValue = Mathf.Max(1, maxSubsidyGoldPerTurn);
            sld_补贴滑动条.onValueChanged.AddListener(HandleSubsidySliderChanged);
        }

        ClearContentRoot();
        Hide();
    }

    private void OnDestroy()
    {
        UnsubscribeBuilding();

        if (btn_关闭弹窗 != null)
        {
            btn_关闭弹窗.onClick.RemoveListener(Hide);
        }

        if (sld_补贴滑动条 != null)
        {
            sld_补贴滑动条.onValueChanged.RemoveListener(HandleSubsidySliderChanged);
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
        subsidySource = null;
        SetText(txt_建筑名称, string.Empty);
        SetText(txt_建筑描述, string.Empty);
        SetIcon(null);
        SetSubsidyVisible(false);
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
        RefreshSubsidy(building);
        RebuildContent(building);
    }

    private void RebuildContent(BuildingBase targetBuilding)
    {
        ClearContentRoot();

        if (root_内容栏 == null)
        {
            return;
        }

        IReadOnlyList<BuildingDetailSection> sections = BuildingDetailUIFormatter.CreateDetailSections(targetBuilding);
        if (!BuildingDetailUIFormatter.HasAnyValidSection(sections))
        {
            AddContentText("暂无详情");
            return;
        }

        for (int i = 0; i < sections.Count; i++)
        {
            BuildingDetailSection section = sections[i];
            if (!section.IsValid)
            {
                continue;
            }

            AddTitle(section.Title);

            IReadOnlyList<BuildingDetailRow> rows = section.Rows;
            for (int j = 0; j < rows.Count; j++)
            {
                BuildingDetailRow row = rows[j];
                if (!row.IsValid)
                {
                    continue;
                }

                AddContentText(FormatRow(row));
            }
        }
    }

    private void RefreshSubsidy(BuildingBase targetBuilding)
    {
        subsidySource = targetBuilding as IBuildingJobSource;
        if (subsidySource == null)
        {
            SetSubsidyVisible(false);
            return;
        }

        SetSubsidyVisible(true);

        if (sld_补贴滑动条 == null)
        {
            return;
        }

        int value = Mathf.Max(0, subsidySource.SubsidyGoldPerTurn);
        sld_补贴滑动条.maxValue = Mathf.Max(1, maxSubsidyGoldPerTurn, value);
        suppressSubsidySliderCallback = true;
        sld_补贴滑动条.value = value;
        suppressSubsidySliderCallback = false;
    }

    private void HandleSubsidySliderChanged(float value)
    {
        if (suppressSubsidySliderCallback || subsidySource == null)
        {
            return;
        }

        subsidySource.SetSubsidyGoldPerTurn(Mathf.RoundToInt(value));
    }

    private void HandleBuildingStateChanged(BuildingBase changedBuilding)
    {
        if (changedBuilding == building && IsVisible)
        {
            Refresh();
        }
    }

    private void UnsubscribeBuilding()
    {
        if (building != null)
        {
            building.StateChanged -= HandleBuildingStateChanged;
        }
    }

    private void AddTitle(string text)
    {
        GameObject item = InstantiateContent(prefab_标题);
        SetText(GetText(item), text);
    }

    private void AddContentText(string text)
    {
        GameObject item = InstantiateContent(prefab_内容文本);
        SetText(GetText(item), text);
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

        if (root_内容栏 == null)
        {
            return;
        }

        for (int i = root_内容栏.childCount - 1; i >= 0; i--)
        {
            Transform child = root_内容栏.GetChild(i);
            if (child == null
                || child.gameObject == go_补贴栏标题
                || child.gameObject == go_补贴栏内容)
            {
                continue;
            }

            DestroyIfNeeded(child.gameObject);
        }
    }

    private void SetSubsidyVisible(bool visible)
    {
        SetActive(go_补贴栏标题, visible);
        SetActive(go_补贴栏内容, visible);
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

        return targetBuilding.Definition == null ? string.Empty : targetBuilding.Definition.BuildingId;
    }

    private static string FormatRow(BuildingDetailRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Label))
        {
            return row.Value;
        }

        if (string.IsNullOrWhiteSpace(row.Value))
        {
            return row.Label;
        }

        return $"{row.Label}：{row.Value}";
    }

    private static TMP_Text GetText(GameObject item)
    {
        return item == null ? null : item.GetComponentInChildren<TMP_Text>(true);
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
