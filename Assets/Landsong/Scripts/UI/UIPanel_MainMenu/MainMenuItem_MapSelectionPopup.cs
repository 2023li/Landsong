using System.Collections.Generic;
using Landsong.AppSystem;
using Landsong.DynastySystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MainMenuItem_MapSelectionPopup : MonoBehaviour
{
    [Header("Data"), LabelText("地图目录")]
    [SerializeField] private MapDataCatalog mapDataCatalog;

    [Header("List"), LabelText("地图项预制体")]
    [SerializeField] private MainMenuItem_MapDataView mapDataViewPrefab;
    [FormerlySerializedAs("mapDataVieRoot")]
    [LabelText("地图项根节点")]
    [SerializeField] private RectTransform mapDataViewRoot;
    [LabelText("显示时重建列表")]
    [SerializeField] private bool rebuildOnShow = true;

    [Header("Buttons"), LabelText("关闭按钮")]
    [FormerlySerializedAs("btn_关闭")]
    [SerializeField] private Button closeButton;

    private readonly List<MainMenuItem_MapDataView> mapDataViews = new List<MainMenuItem_MapDataView>();
    private MapDefinition pendingMapData;

    internal void Hide()
    {
        HideDynastyNamePopup();
        gameObject.SetActive(false);
    }

    internal void Show()
    {
        gameObject.SetActive(true);

        if (rebuildOnShow || mapDataViews.Count <= 0)
        {
            RefreshMapList();
        }
    }

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            return;
        }

        closeButton.onClick.AddListener(Hide);
        btn_Pop确认.onClick.AddListener(ConfirmDynastyName);
        btn_Pop取消.onClick.AddListener(CancelDynastyName);
        HideTemplate();
        HideDynastyNamePopup();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }

        if (btn_Pop确认 != null)
        {
            btn_Pop确认.onClick.RemoveListener(ConfirmDynastyName);
        }

        if (btn_Pop取消 != null)
        {
            btn_Pop取消.onClick.RemoveListener(CancelDynastyName);
        }
    }

    [ContextMenu("Refresh Map List")]
    public void RefreshMapList()
    {
        if (!ValidateRequiredReferences())
        {
            return;
        }

        ClearMapDataViews();

        int createdCount = 0;

        foreach (MapDefinition mapData in mapDataCatalog.GetValidMapDefinitions())
        {
            CreateMapDataView(mapData);
            createdCount++;
        }
    }

    private void HideTemplate()
    {
        mapDataViewPrefab.gameObject.SetActive(false);
    }

    private void CreateMapDataView(MapDefinition mapData)
    {
        MainMenuItem_MapDataView view = Instantiate(mapDataViewPrefab, mapDataViewRoot);
        view.gameObject.SetActive(true);
        view.Initialize(mapData, ConfirmMapSelection);
        mapDataViews.Add(view);
    }

    private void ConfirmMapSelection(MapDefinition mapData)
    {
        if (mapData == null || !mapData.IsValid)
        {
            Debug.LogWarning("开始新游戏失败：没有选择有效地图。", this);
            return;
        }

        ShowDynastyNamePopup(mapData);
    }

    private void ClearMapDataViews()
    {
        for (int i = 0; i < mapDataViews.Count; i++)
        {
            DestroyMapDataView(mapDataViews[i] == null ? null : mapDataViews[i].gameObject);
        }

        mapDataViews.Clear();
        HideTemplate();
    }

    private static void DestroyMapDataView(GameObject viewObject)
    {
        if (viewObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(viewObject);
        }
        else
        {
            DestroyImmediate(viewObject);
        }
    }

   

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (mapDataCatalog == null)
        {
            Debug.LogError("地图选择弹窗配置错误：mapDataCatalog 未绑定。", this);
            isValid = false;
        }

        if (mapDataViewPrefab == null)
        {
            Debug.LogError("地图选择弹窗配置错误：mapDataViewPrefab 未绑定。", this);
            isValid = false;
        }

        if (mapDataViewRoot == null)
        {
            Debug.LogError("地图选择弹窗配置错误：mapDataViewRoot 未绑定。", this);
            isValid = false;
        }

        if (closeButton == null)
        {
            Debug.LogError("地图选择弹窗配置错误：closeButton 未绑定。", this);
            isValid = false;
        }

        if (pop_弹窗 == null)
        {
            Debug.LogError("地图选择弹窗配置错误：pop_弹窗 未绑定。", this);
            isValid = false;
        }

        if (btn_Pop确认 == null)
        {
            Debug.LogError("地图选择弹窗配置错误：btn_Pop确认 未绑定。", this);
            isValid = false;
        }

        if (btn_Pop取消 == null)
        {
            Debug.LogError("地图选择弹窗配置错误：btn_Pop取消 未绑定。", this);
            isValid = false;
        }

        if (ipt_名称输入框 == null)
        {
            Debug.LogError("地图选择弹窗配置错误：ipt_名称输入框 未绑定。", this);
            isValid = false;
        }

        if (dynastyNameCatalog == null)
        {
            Debug.LogError("地图选择弹窗配置错误：dynastyNameCatalog 未绑定。", this);
            isValid = false;
        }

        return isValid;
    }


    #region 弹窗
    [SerializeField,FoldoutGroup("取名弹窗")] private GameObject pop_弹窗;
    [SerializeField, FoldoutGroup("取名弹窗")] private Button btn_Pop确认;
    [SerializeField, FoldoutGroup("取名弹窗")] private Button btn_Pop取消;
    [SerializeField, FoldoutGroup("取名弹窗")] private TMP_InputField ipt_名称输入框;
    [SerializeField, FoldoutGroup("取名弹窗")] private DynastyNameCatalog dynastyNameCatalog;

    private void ShowDynastyNamePopup(MapDefinition mapData)
    {
        pendingMapData = mapData;
        ipt_名称输入框.text = GetRandomDynastyName();
        pop_弹窗.SetActive(true);
        ipt_名称输入框.Select();
        ipt_名称输入框.ActivateInputField();
    }

    private void HideDynastyNamePopup()
    {
        pendingMapData = null;

        if (pop_弹窗 != null)
        {
            pop_弹窗.SetActive(false);
        }
    }

    private void ConfirmDynastyName()
    {
        if (pendingMapData == null || !pendingMapData.IsValid)
        {
            Debug.LogWarning("开始新游戏失败：没有选择有效地图。", this);
            HideDynastyNamePopup();
            return;
        }

        MapDefinition selectedMapData = pendingMapData;
        string dynastyName = DynastyService.NormalizeDynastyName(ipt_名称输入框.text);
        HideDynastyNamePopup();
        AppManager.Instance.StartNewGame(selectedMapData, dynastyName);
    }

    private void CancelDynastyName()
    {
        HideDynastyNamePopup();
    }

    private string GetRandomDynastyName()
    {
        return dynastyNameCatalog == null
            ? DynastyService.DefaultDynastyName
            : dynastyNameCatalog.GetRandomName(DynastyService.DefaultDynastyName);
    }
    #endregion
}
