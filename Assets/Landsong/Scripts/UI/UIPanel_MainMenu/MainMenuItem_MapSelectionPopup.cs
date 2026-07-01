using System.Collections.Generic;
using Landsong.AppSystem;
using Sirenix.OdinInspector;
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

    internal void Hide()
    {
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
        HideTemplate();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
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

        foreach (MapDataCatalog.MapData mapData in mapDataCatalog.GetValidMapDatas())
        {
            CreateMapDataView(mapData);
            createdCount++;
        }

      
    }

    private void HideTemplate()
    {
        mapDataViewPrefab.gameObject.SetActive(false);
    }

    private void CreateMapDataView(MapDataCatalog.MapData mapData)
    {
        MainMenuItem_MapDataView view = Instantiate(mapDataViewPrefab, mapDataViewRoot);
        view.gameObject.SetActive(true);
        view.Initialize(mapData, ConfirmMapSelection);
        mapDataViews.Add(view);
    }

    private void ConfirmMapSelection(MapDataCatalog.MapData mapData)
    {
        if (mapData == null || !mapData.IsValid)
        {
            Debug.LogWarning("开始新游戏失败：没有选择有效地图。", this);
            return;
        }

        AppManager.Instance.StartNewGame(mapData);
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

        return isValid;
    }
}
