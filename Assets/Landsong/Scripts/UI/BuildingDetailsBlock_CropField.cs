using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingDetailsBlock_CropField : BuildingDetailsBlockBase
{
    [SerializeField, LabelText("作物状态文本")] private TMP_Text txt_作物状态;
    [SerializeField, LabelText("成熟回合文本")] private TMP_Text txt_成熟回合;
    [SerializeField, LabelText("自动收获文本")] private TMP_Text txt_自动收获;
    [SerializeField, LabelText("选择作物按钮")] private Button btn_选择作物;
    [SerializeField, LabelText("作物选择面板")] private GameObject go_作物选择面板;
    [SerializeField, LabelText("作物选项根节点")] private RectTransform root_作物选项;
    [SerializeField, LabelText("作物选项按钮预制体")]
    [PropertyTooltip("直接第一个子对象必须挂 Image 作为图标，直接第二个子对象必须挂 TMP_Text 作为名称。")]
    private Button prefab_作物选项按钮;
    [SerializeField, LabelText("收获按钮")] private Button btn_收获;
    [SerializeField, LabelText("铲除按钮")] private Button btn_铲除;
    [SerializeField, LabelText("自动收获开关")] private Toggle tgl_自动收获;

    private readonly List<GameObject> activeCropOptionItems = new List<GameObject>();
    private Popup_BuildingDetails owner;
    private IBuildingCropFieldSource cropSource;
    private IBuildingCropFieldActions cropActions;
    private bool listenersBound;
    private bool suppressCallbacks;

    public override void Initialize(Popup_BuildingDetails detailOwner)
    {
        owner = detailOwner;
        ResolveFields();
        BindListeners();
        SetCropSelectionPanelVisible(false);
    }

    public override bool CanShow(BuildingBase targetBuilding)
    {
        return targetBuilding is IBuildingCropFieldSource
               && targetBuilding is IBuildingCropFieldActions;
    }

    public override void Bind(BuildingBase targetBuilding)
    {
        cropSource = targetBuilding as IBuildingCropFieldSource;
        cropActions = targetBuilding as IBuildingCropFieldActions;
        if (cropSource == null || cropActions == null)
        {
            Unbind();
            return;
        }

        SetBlockVisible(true);
        Refresh();
    }

    public override void Refresh()
    {
        if (cropSource == null || cropActions == null)
        {
            SetBlockVisible(false);
            return;
        }

        suppressCallbacks = true;
        if (tgl_自动收获 != null)
        {
            tgl_自动收获.isOn = cropSource.AutoHarvestEnabled;
            tgl_自动收获.interactable = true;
        }

        suppressCallbacks = false;

        SetText(txt_作物状态, BuildCropStatusText());
        SetText(txt_成熟回合, BuildGrowthText());
        SetText(txt_自动收获, cropSource.AutoHarvestEnabled ? "自动收获：开启" : "自动收获：关闭");
        RefreshButtons();

        if (go_作物选择面板 != null && go_作物选择面板.activeSelf)
        {
            RebuildCropOptions();
        }
    }

    public override void Unbind()
    {
        cropSource = null;
        cropActions = null;
        suppressCallbacks = false;
        SetText(txt_作物状态, string.Empty);
        SetText(txt_成熟回合, string.Empty);
        SetText(txt_自动收获, string.Empty);

        if (tgl_自动收获 != null)
        {
            tgl_自动收获.isOn = false;
            tgl_自动收获.interactable = false;
        }

        SetButtonInteractable(btn_选择作物, false);
        SetButtonInteractable(btn_收获, false);
        SetButtonInteractable(btn_铲除, false);
        SetCropSelectionPanelVisible(false);
        ClearCropOptions();
        SetBlockVisible(false);
    }

    private void OnDestroy()
    {
        UnbindListeners();
    }

    private void BindListeners()
    {
        if (listenersBound)
        {
            return;
        }

        if (btn_选择作物 != null)
        {
            btn_选择作物.onClick.AddListener(HandleSelectCropClicked);
        }

        if (btn_收获 != null)
        {
            btn_收获.onClick.AddListener(HandleHarvestClicked);
        }

        if (btn_铲除 != null)
        {
            btn_铲除.onClick.AddListener(HandleClearCropClicked);
        }

        if (tgl_自动收获 != null)
        {
            tgl_自动收获.onValueChanged.AddListener(HandleAutoHarvestChanged);
        }

        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
        {
            return;
        }

        if (btn_选择作物 != null)
        {
            btn_选择作物.onClick.RemoveListener(HandleSelectCropClicked);
        }

        if (btn_收获 != null)
        {
            btn_收获.onClick.RemoveListener(HandleHarvestClicked);
        }

        if (btn_铲除 != null)
        {
            btn_铲除.onClick.RemoveListener(HandleClearCropClicked);
        }

        if (tgl_自动收获 != null)
        {
            tgl_自动收获.onValueChanged.RemoveListener(HandleAutoHarvestChanged);
        }

        listenersBound = false;
    }

    private void HandleSelectCropClicked()
    {
        if (cropSource == null || cropActions == null)
        {
            return;
        }

        var nextVisible = go_作物选择面板 == null || !go_作物选择面板.activeSelf;
        SetCropSelectionPanelVisible(nextVisible);
        if (nextVisible)
        {
            RebuildCropOptions();
        }
    }

    private void HandleHarvestClicked()
    {
        if (cropActions == null)
        {
            return;
        }

        cropActions.TryHarvest();
        RefreshOwner();
    }

    private void HandleClearCropClicked()
    {
        if (cropActions == null)
        {
            return;
        }

        cropActions.TryClearCrop();
        RefreshOwner();
    }

    private void HandleAutoHarvestChanged(bool enabled)
    {
        if (suppressCallbacks || cropActions == null)
        {
            return;
        }

        cropActions.TrySetAutoHarvestEnabled(enabled);
        RefreshOwner();
    }

    private void HandleCropOptionClicked(string cropId)
    {
        if (cropActions == null)
        {
            return;
        }

        cropActions.TryPlant(cropId);
        SetCropSelectionPanelVisible(false);
        RefreshOwner();
    }

    private void RefreshButtons()
    {
        var hasCropOptions = cropSource != null
                             && cropSource.CropOptions != null
                             && cropSource.CropOptions.Count > 0;

        SetButtonInteractable(btn_选择作物, cropSource != null && !cropSource.HasCrop && hasCropOptions);
        SetButtonInteractable(btn_收获, cropActions != null && cropActions.CanHarvest());
        SetButtonInteractable(btn_铲除, cropActions != null && cropActions.CanClearCrop());
    }

    private void RebuildCropOptions()
    {
        ClearCropOptions();
        if (cropSource == null || cropActions == null || root_作物选项 == null || prefab_作物选项按钮 == null)
        {
            return;
        }

        var options = cropSource.CropOptions;
        if (options == null)
        {
            return;
        }

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (!option.IsValid)
            {
                continue;
            }

            AddCropOption(option);
        }
    }

    private void AddCropOption(BuildingCropOption option)
    {
        var item = Instantiate(prefab_作物选项按钮, root_作物选项);
        item.gameObject.SetActive(true);
        activeCropOptionItems.Add(item.gameObject);

        var cropId = option.CropId;
        item.onClick.AddListener(() => HandleCropOptionClicked(cropId));
        item.interactable = cropActions != null && cropActions.CanPlant(cropId);

        BindCropOptionIcon(item.transform, option.Icon);
        BindCropOptionName(item.transform, option);
    }

    private static void BindCropOptionIcon(Transform optionRoot, Sprite icon)
    {
        var iconImage = optionRoot != null && optionRoot.childCount > 0
            ? optionRoot.GetChild(0).GetComponent<Image>()
            : null;

        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    private static void BindCropOptionName(Transform optionRoot, BuildingCropOption option)
    {
        var nameText = optionRoot != null && optionRoot.childCount > 1
            ? optionRoot.GetChild(1).GetComponent<TMP_Text>()
            : null;

        if (nameText != null)
        {
            nameText.text = $"{option.DisplayName}（{option.GrowTurns}回合）";
        }
    }

    private void ClearCropOptions()
    {
        for (var i = 0; i < activeCropOptionItems.Count; i++)
        {
            DestroyIfNeeded(activeCropOptionItems[i]);
        }

        activeCropOptionItems.Clear();
    }

    private string BuildCropStatusText()
    {
        if (cropSource == null || !cropSource.HasCrop)
        {
            return "当前作物：未种植";
        }

        return cropSource.IsMature
            ? $"当前作物：{cropSource.PlantedCropDisplayName}（可收获）"
            : $"当前作物：{cropSource.PlantedCropDisplayName}";
    }

    private string BuildGrowthText()
    {
        if (cropSource == null || !cropSource.HasCrop)
        {
            return "成熟进度：无";
        }

        if (cropSource.IsMature)
        {
            return $"成熟进度：{cropSource.GrowthProgressTurns}/{cropSource.RequiredGrowTurns}";
        }

        return $"成熟剩余：{cropSource.RemainingGrowTurns}回合";
    }

    private void ResolveFields()
    {
        if (btn_选择作物 == null)
        {
            btn_选择作物 = FindButtonByNameOrText(transform, "选择作物")
                        ?? FindButtonByNameOrText(transform, "种植");
        }

        if (btn_收获 == null)
        {
            btn_收获 = FindButtonByNameOrText(transform, "收获");
        }

        if (btn_铲除 == null)
        {
            btn_铲除 = FindButtonByNameOrText(transform, "铲除");
        }

        if (tgl_自动收获 == null)
        {
            tgl_自动收获 = GetComponentInChildren<Toggle>(true);
        }

        if (go_作物选择面板 == null)
        {
            var panel = FindChildByName(transform, "作物选择面板")
                     ?? FindChildByName(transform, "crop_selection_panel");
            go_作物选择面板 = panel == null ? null : panel.gameObject;
        }

        if (root_作物选项 == null && go_作物选择面板 != null)
        {
            root_作物选项 = go_作物选择面板.GetComponentInChildren<RectTransform>(true);
        }

        ResolveTextFields();
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

            if (txt_作物状态 == null && text.name.Contains("作物"))
            {
                txt_作物状态 = text;
                continue;
            }

            if (txt_成熟回合 == null && (text.name.Contains("成熟") || text.name.Contains("回合")))
            {
                txt_成熟回合 = text;
                continue;
            }

            if (txt_自动收获 == null && text.name.Contains("自动"))
            {
                txt_自动收获 = text;
            }
        }

        if (txt_作物状态 == null && texts.Length > 0)
        {
            txt_作物状态 = texts[0];
        }

        if (txt_成熟回合 == null && texts.Length > 1)
        {
            txt_成熟回合 = texts[1];
        }

        if (txt_自动收获 == null && texts.Length > 2)
        {
            txt_自动收获 = texts[2];
        }
    }

    private static Button FindButtonByNameOrText(Transform root, string keyword)
    {
        if (root == null || string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        var buttons = root.GetComponentsInChildren<Button>(true);
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (button.name.Contains(keyword))
            {
                return button;
            }

            var texts = button.GetComponentsInChildren<TMP_Text>(true);
            for (var j = 0; j < texts.Length; j++)
            {
                if (texts[j] != null && texts[j].text.Contains(keyword))
                {
                    return button;
                }
            }
        }

        return null;
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

    private void SetCropSelectionPanelVisible(bool visible)
    {
        SetActive(go_作物选择面板, visible);
        if (!visible)
        {
            ClearCropOptions();
        }
    }

    private void SetBlockVisible(bool visible)
    {
        SetActive(gameObject, visible);
    }

    private void RefreshOwner()
    {
        if (owner != null)
        {
            owner.RefreshCurrentBuildingDetails();
            return;
        }

        Refresh();
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
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
