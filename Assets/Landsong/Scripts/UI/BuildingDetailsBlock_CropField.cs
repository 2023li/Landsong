using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingDetailsBlock_CropField : BuildingDetailsBlockBase
{
    [SerializeField, Required, LabelText("作物状态文本")] private TMP_Text txt_作物状态;
    [SerializeField, Required, LabelText("成熟回合文本")] private TMP_Text txt_成熟回合;
    [SerializeField, Required, LabelText("自动收获文本")] private TMP_Text txt_自动收获;
    [SerializeField, Required, LabelText("选择作物按钮")] private Button btn_选择作物;
    [SerializeField, Required, LabelText("作物选择面板")] private GameObject go_作物选择面板;
    [SerializeField, Required, LabelText("作物选项根节点")] private RectTransform root_作物选项;
    [SerializeField, Required, LabelText("作物选项按钮预制体")]
    private BuildingDetailsCropOptionItem prefab_作物选项按钮;
    [SerializeField, Required, LabelText("收获按钮")] private Button btn_收获;
    [SerializeField, Required, LabelText("铲除按钮")] private Button btn_铲除;
    [SerializeField, Required, LabelText("自动收获开关")] private Toggle tgl_自动收获;

    private readonly List<GameObject> activeCropOptionItems = new List<GameObject>();
    private IBuildingCropFieldSource cropSource;
    private IBuildingCropFieldActions cropActions;
    private bool listenersBound;
    private bool suppressCallbacks;

    public override bool ValidateConfiguration(out string error)
    {
        var missing = new List<string>();
        AddMissingReference(missing, txt_作物状态, nameof(txt_作物状态));
        AddMissingReference(missing, txt_成熟回合, nameof(txt_成熟回合));
        AddMissingReference(missing, txt_自动收获, nameof(txt_自动收获));
        AddMissingReference(missing, btn_选择作物, nameof(btn_选择作物));
        AddMissingReference(missing, go_作物选择面板, nameof(go_作物选择面板));
        AddMissingReference(missing, root_作物选项, nameof(root_作物选项));
        AddMissingReference(missing, prefab_作物选项按钮, nameof(prefab_作物选项按钮));
        AddMissingReference(missing, btn_收获, nameof(btn_收获));
        AddMissingReference(missing, btn_铲除, nameof(btn_铲除));
        AddMissingReference(missing, tgl_自动收获, nameof(tgl_自动收获));
        if (!BuildValidationResult(missing, out error))
        {
            return false;
        }

        return prefab_作物选项按钮.ValidateConfiguration(out error);
    }

    public override void Initialize(Popup_BuildingDetails detailOwner)
    {
        BindListeners();
        SetCropSelectionPanelVisible(false);
    }

    public override bool CanShow(BuildingBase targetBuilding)
    {
        return targetBuilding != null
               && targetBuilding.TryGetCapability<IBuildingCropFieldSource>(out _)
               && targetBuilding.TryGetCapability<IBuildingCropFieldActions>(out _);
    }

    public override void Bind(BuildingBase targetBuilding)
    {
        cropSource = null;
        cropActions = null;
        targetBuilding?.TryGetCapability(out cropSource);
        targetBuilding?.TryGetCapability(out cropActions);
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

        if (!cropActions.TryHarvest())
        {
            Refresh();
        }
    }

    private void HandleClearCropClicked()
    {
        if (cropActions == null)
        {
            return;
        }

        if (!cropActions.TryClearCrop())
        {
            Refresh();
        }
    }

    private void HandleAutoHarvestChanged(bool enabled)
    {
        if (suppressCallbacks || cropActions == null)
        {
            return;
        }

        if (!cropActions.TrySetAutoHarvestEnabled(enabled))
        {
            Refresh();
        }
    }

    private void HandleCropOptionClicked(string cropId)
    {
        if (cropActions == null)
        {
            return;
        }

        if (cropActions.TryPlant(cropId))
        {
            SetCropSelectionPanelVisible(false);
            return;
        }

        Refresh();
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
        item.Button.onClick.AddListener(() => HandleCropOptionClicked(cropId));
        item.Bind(option, cropActions != null && cropActions.CanPlant(cropId));
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
