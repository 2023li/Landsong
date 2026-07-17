using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 建筑等级面板。升级由 BuildingUpgradeService 原地应用等级配置，不再使用经验或替换 Prefab。
/// </summary>
public sealed class BuildingDetailsBlock_Level : BuildingDetailsBlockBase
{
    [SerializeField, Required, LabelText("旧自动升级开关（终态禁用）")] private Toggle tgl_自动升级;
    [SerializeField, Required, LabelText("等级进度条")] private Slider sld_经验进度;
    [SerializeField, Required, LabelText("等级文本")] private TMP_Text txt_经验;
    [SerializeField, Required, LabelText("升级按钮")] private Button btn_升级;
    [SerializeField, Required, LabelText("升级消耗/条件文本")] private TMP_Text txt_升级消耗;

    private BuildingBase building;
    private bool listenersBound;

    public override bool ValidateConfiguration(out string error)
    {
        var missing = new List<string>();
        AddMissingReference(missing, tgl_自动升级, nameof(tgl_自动升级));
        AddMissingReference(missing, sld_经验进度, nameof(sld_经验进度));
        AddMissingReference(missing, txt_经验, nameof(txt_经验));
        AddMissingReference(missing, btn_升级, nameof(btn_升级));
        AddMissingReference(missing, txt_升级消耗, nameof(txt_升级消耗));
        return BuildValidationResult(missing, out error);
    }

    public override bool CanShow(BuildingBase targetBuilding)
    {
        return targetBuilding?.FamilyDefinition != null
               && targetBuilding.FamilyDefinition.Levels.Count > 1;
    }

    public override void Initialize(Popup_BuildingDetails detailOwner)
    {
        BindListeners();
        if (tgl_自动升级 != null)
        {
            tgl_自动升级.gameObject.SetActive(false);
        }
    }

    public override void Bind(BuildingBase targetBuilding)
    {
        building = targetBuilding;
        SetBlockVisible(CanShow(building));
        Refresh();
    }

    public override void Refresh()
    {
        if (!CanShow(building))
        {
            SetBlockVisible(false);
            return;
        }

        var service = building.GameSystem?.Services.Buildings?.Upgrades;
        var evaluation = service == null
            ? new BuildingUpgradeResult(
                false,
                BuildingUpgradeFailure.MissingBuilding,
                0,
                Landsong.Localization.L10n.Gameplay("gameplay.building.upgrade.not_initialized", "升级服务未初始化。"))
            : service.Evaluate(building);
        var maxLevel = building.FamilyDefinition.Levels.Count;
        var targetLevel = building.CurrentLevel + 1;

        if (sld_经验进度 != null)
        {
            sld_经验进度.minValue = 1f;
            sld_经验进度.maxValue = Mathf.Max(1, maxLevel);
            sld_经验进度.value = building.CurrentLevel;
            sld_经验进度.interactable = false;
        }

        SetText(txt_经验, $"LV{building.CurrentLevel} / LV{maxLevel}");
        if (building.FamilyDefinition.TryGetLevel(targetLevel, out var target))
        {
            var costs = FormatUpgradeCosts(target.UpgradeCosts);
            SetText(
                txt_升级消耗,
                evaluation.Succeeded ? costs : $"{costs}\n{evaluation.Message}");
        }
        else
        {
            SetText(txt_升级消耗, evaluation.Message);
        }

        if (btn_升级 != null)
        {
            btn_升级.interactable = evaluation.Succeeded;
        }
    }

    public override void Unbind()
    {
        building = null;
        SetText(txt_经验, string.Empty);
        SetText(txt_升级消耗, string.Empty);
        if (btn_升级 != null) btn_升级.interactable = false;
        if (sld_经验进度 != null) sld_经验进度.value = 1f;
        SetBlockVisible(false);
    }

    private void OnDestroy() => UnbindListeners();

    private void BindListeners()
    {
        if (listenersBound) return;
        if (btn_升级 != null) btn_升级.onClick.AddListener(HandleUpgradeClicked);
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound) return;
        if (btn_升级 != null) btn_升级.onClick.RemoveListener(HandleUpgradeClicked);
        listenersBound = false;
    }

    private void HandleUpgradeClicked()
    {
        var upgradeService = building?.GameSystem?.Services.Buildings?.Upgrades;
        if (upgradeService == null || !upgradeService.TryUpgrade(building).Succeeded)
        {
            Refresh();
        }
    }

    private static string FormatUpgradeCosts(IReadOnlyList<BuildingCost> costs)
    {
        var content = string.Empty;
        if (costs != null)
        {
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (!cost.IsValid) continue;
                if (content.Length > 0) content += Landsong.Localization.L10n.Gameplay("gameplay.common.list_separator", "、");
                content += $"{cost.ItemId}x{cost.Amount}";
            }
        }

        return content.Length == 0
            ? Landsong.Localization.L10n.Gameplay("gameplay.building.upgrade.no_cost", "升级消耗：无")
            : Landsong.Localization.L10n.Gameplay("gameplay.building.upgrade.cost", "升级消耗：{0}", content);
    }

    private void SetBlockVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target != null) target.text = text ?? string.Empty;
    }
}
