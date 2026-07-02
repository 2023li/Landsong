using System;
using Landsong.UISystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

public class UIPanel_Game : UIPanelBase
{
    [SerializeField] private RectTransform gameMarkRoot;
    [SerializeField] private GamePanel_HUD hudPanel;
    [SerializeField] private GamePanel_Inventory inventoryPanel;
    [SerializeField] private GamePanel_Building buildingPanel;
    [SerializeField] private GamePanel_BuildingPlacementControls buildingPlacementControls;
    [SerializeField] private GamePanel_BuildingStatusOverview buildingStatusOverview;

    [SerializeField] private GamePanel_BuildingEventMessageList buildingEventMessageList;
    [SerializeField] private GamePanel_SelectedBuildingOverview selectedBuildingOverview;
    [SerializeField] private GamePanel_BuildingDetaiPopup buildingDetailPopup;
    [SerializeField] private GamePanel_BuildingSelectionView buildingSelectionView;

    public RectTransform GameMarkRoot => gameMarkRoot;
    public GamePanel_BuildingEventMessageList BuildingEventMessageList => buildingEventMessageList;
    public GamePanel_SelectedBuildingOverview SelectedBuildingOverview => selectedBuildingOverview;
    public GamePanel_BuildingDetaiPopup BuildingDetailPopup => buildingDetailPopup;
    public GamePanel_BuildingSelectionView BuildingSelectionView => buildingSelectionView;
    public GamePanel_BuildingPlacementControls BuildingPlacementControls => buildingPlacementControls;

    private void Reset()
    {
        GetReference();
    }

    private void Awake()
    {
        GetReference();
    }

    public void Show_HUD()
    {
        if (hudPanel != null)
        {
            hudPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no HUD panel assigned.", this);
        }
    }

    public void Show_Inventory()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no inventory panel assigned.", this);
        }
    }
    internal void Hide_Inventory()
    {
        inventoryPanel.Hide();
    }


    public void Show_Building()
    {
        if (buildingPanel != null)
        {
            buildingPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no building panel assigned.", this);
        }
    }
    public void Hide_Building()
    {

        buildingPanel.Hide();
        Show_HUD();

    }



    internal void Show_Overview()
    {
        buildingStatusOverview.Show();
    }

    internal void Hide_Overview()
    {
        buildingStatusOverview.Hide();
    }








    private void GetReference()
    {
        if (hudPanel == null)
        {
            hudPanel = GetComponentInChildren<GamePanel_HUD>(true);
        }

        if (inventoryPanel == null)
        {
            inventoryPanel = GetComponentInChildren<GamePanel_Inventory>(true);
        }

        if (buildingPanel == null)
        {
            buildingPanel = GetComponentInChildren<GamePanel_Building>(true);
        }

        if (buildingPlacementControls == null)
        {
            buildingPlacementControls = GetComponentInChildren<GamePanel_BuildingPlacementControls>(true);
        }
        if (buildingEventMessageList == null)
        {
            buildingEventMessageList = GetComponentInChildren<GamePanel_BuildingEventMessageList>(true);
        }
        if (selectedBuildingOverview == null)
        {
            selectedBuildingOverview = GetComponentInChildren<GamePanel_SelectedBuildingOverview>(true);
        }
        if (buildingDetailPopup == null)
        {
            buildingDetailPopup = GetComponentInChildren<GamePanel_BuildingDetaiPopup>(true);
        }
        if (buildingSelectionView == null)
        {
            buildingSelectionView = GetComponentInChildren<GamePanel_BuildingSelectionView>(true);
        }
        if (buildingStatusOverview == null)
        {
            buildingStatusOverview = GetComponentInChildren<GamePanel_BuildingStatusOverview>();
        }
    }
}
