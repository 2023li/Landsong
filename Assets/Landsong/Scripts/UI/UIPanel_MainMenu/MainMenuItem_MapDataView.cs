using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MainMenuItem_MapDataView : MonoBehaviour
{
    [FormerlySerializedAs("btn_确认选择")]
    [SerializeField, LabelText("确认选择按钮")] private Button confirmSelectionButton;
    [SerializeField, LabelText("地图名称文本")] private TMP_Text mapName;
    [SerializeField, LabelText("地图描述文本")] private TMP_Text mapDescription;
    [FormerlySerializedAs("map_icon")]
    [SerializeField, LabelText("地图图标")] private Image mapIcon;

    private MapDefinition mapData;
    private Action<MapDefinition> onConfirmSelection;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            return;
        }

        confirmSelectionButton.onClick.AddListener(ConfirmSelection);
    }

    private void OnDestroy()
    {
        if (confirmSelectionButton != null)
        {
            confirmSelectionButton.onClick.RemoveListener(ConfirmSelection);
        }
    }

    public void Initialize(MapDefinition newMapData, Action<MapDefinition> confirmSelection)
    {
        mapData = newMapData;
        onConfirmSelection = confirmSelection;
        RefreshView();
    }

    private void RefreshView()
    {
        if (!ValidateRequiredReferences())
        {
            return;
        }

        bool hasValidMap = mapData != null && mapData.IsValid;

            mapName.text = hasValidMap
                ? mapData.DisplayName
                : Landsong.Localization.L10n.Ui("ui.map.invalid", "无效地图");
        mapDescription.text = hasValidMap ? FormatOptionalText(mapData.Description) : "-";
        mapIcon.sprite = hasValidMap ? mapData.Icon : null;
        mapIcon.enabled = hasValidMap && mapData.Icon != null;
        confirmSelectionButton.interactable = hasValidMap;
    }

    private void ConfirmSelection()
    {
        if (mapData == null || !mapData.IsValid)
        {
            Debug.LogWarning("开始新游戏失败：没有选择有效地图。", this);
            RefreshView();
            return;
        }

        if (onConfirmSelection == null)
        {
            Debug.LogError("地图项配置错误：未绑定确认选择回调。", this);
            return;
        }

        onConfirmSelection.Invoke(mapData);
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (confirmSelectionButton == null)
        {
            Debug.LogError("地图项配置错误：confirmSelectionButton 未绑定。", this);
            isValid = false;
        }

        if (mapName == null)
        {
            Debug.LogError("地图项配置错误：mapName 未绑定。", this);
            isValid = false;
        }

        if (mapDescription == null)
        {
            Debug.LogError("地图项配置错误：mapDescription 未绑定。", this);
            isValid = false;
        }

        if (mapIcon == null)
        {
            Debug.LogError("地图项配置错误：mapIcon 未绑定。", this);
            isValid = false;
        }

        return isValid;
    }

    private string FormatOptionalText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
    }

}
