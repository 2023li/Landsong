using System.Collections.Generic;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingDetailsBlock_Level : MonoBehaviour
{
    [SerializeField] private Toggle tgl_自动升级;
    [SerializeField] private Slider sld_经验进度;
    [SerializeField] private TMP_Text txt_经验;
    [SerializeField] private Button btn_升级;
    [SerializeField] private TMP_Text txt_升级消耗;

    private BuildingBase building;
    private BM_等级升级 levelModule;
    private bool suppressCallback;
    private bool listenersBound;

    public bool CanShow(BuildingBase targetBuilding)
    {
        return targetBuilding != null
               && targetBuilding.TryGetModule<BM_等级升级>(out _);
    }

    public void Initialize(Popup_BuildingDetails detailOwner)
    {
        ResolveFields();
        BindListeners();
    }

    public void Bind(BuildingBase targetBuilding)
    {
        building = targetBuilding;
        if (building == null || !building.TryGetModule(out levelModule))
        {
            Unbind();
            return;
        }

        SetBlockVisible(true);
        Refresh();
    }

    public void Refresh()
    {
        if (levelModule == null)
        {
            SetBlockVisible(false);
            return;
        }

        suppressCallback = true;
        if (tgl_自动升级 != null)
        {
            tgl_自动升级.isOn = levelModule.AutoUpgradeEnabled;
        }

        if (sld_经验进度 != null)
        {
            sld_经验进度.minValue = 0f;
            sld_经验进度.maxValue = 1f;
            sld_经验进度.value = levelModule.ExperienceProgress;
            sld_经验进度.interactable = false;
        }

        suppressCallback = false;

        SetText(txt_经验, $"{levelModule.CurrentExperience}/{levelModule.RequiredExperience}");
        SetText(txt_升级消耗, FormatUpgradeCosts(levelModule.UpgradeCosts));
        if (btn_升级 != null)
        {
            btn_升级.interactable = levelModule.CanUpgrade(building);
        }
    }

    public void Unbind()
    {
        building = null;
        levelModule = null;
        suppressCallback = false;
        if (tgl_自动升级 != null)
        {
            tgl_自动升级.isOn = false;
        }

        if (sld_经验进度 != null)
        {
            sld_经验进度.value = 0f;
        }

        SetText(txt_经验, string.Empty);
        SetText(txt_升级消耗, string.Empty);
        if (btn_升级 != null)
        {
            btn_升级.interactable = false;
        }

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

        if (tgl_自动升级 != null)
        {
            tgl_自动升级.onValueChanged.AddListener(HandleAutoUpgradeChanged);
        }

        if (btn_升级 != null)
        {
            btn_升级.onClick.AddListener(HandleUpgradeClicked);
        }

        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
        {
            return;
        }

        if (tgl_自动升级 != null)
        {
            tgl_自动升级.onValueChanged.RemoveListener(HandleAutoUpgradeChanged);
        }

        if (btn_升级 != null)
        {
            btn_升级.onClick.RemoveListener(HandleUpgradeClicked);
        }

        listenersBound = false;
    }

    private void HandleAutoUpgradeChanged(bool enabled)
    {
        if (suppressCallback || levelModule == null)
        {
            return;
        }

        levelModule.SetAutoUpgradeEnabled(enabled);
    }

    private void HandleUpgradeClicked()
    {
        if (building == null || levelModule == null)
        {
            return;
        }

        if (!levelModule.TryUpgrade(building))
        {
            Refresh();
        }
    }

    private void ResolveFields()
    {
        if (tgl_自动升级 == null)
        {
            tgl_自动升级 = GetComponentInChildren<Toggle>(true);
        }

        if (sld_经验进度 == null)
        {
            sld_经验进度 = GetComponentInChildren<Slider>(true);
        }

        if (btn_升级 == null)
        {
            btn_升级 = GetComponentInChildren<Button>(true);
        }

        ResolveTextFields();
    }

    private void ResolveTextFields()
    {
        if (txt_经验 != null)
        {
            if (txt_升级消耗 != null)
            {
                return;
            }
        }

        var texts = GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text != null && text.name.Contains("经验"))
            {
                txt_经验 = text;
                continue;
            }

            if (text != null && text.name.Contains("消耗"))
            {
                txt_升级消耗 = text;
            }
        }

        if (txt_经验 == null && texts.Length > 0)
        {
            txt_经验 = texts[0];
        }

        if (txt_升级消耗 == null && texts.Length > 1)
        {
            txt_升级消耗 = texts[1];
        }
    }

    private static string FormatUpgradeCosts(IReadOnlyList<BuildingCost> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return "升级消耗：无";
        }

        var content = string.Empty;
        for (var i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (!cost.IsValid)
            {
                continue;
            }

            if (content.Length > 0)
            {
                content += "、";
            }

            content += $"{cost.ItemId}x{cost.Amount}";
        }

        return content.Length == 0 ? "升级消耗：无" : $"升级消耗：{content}";
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
}
