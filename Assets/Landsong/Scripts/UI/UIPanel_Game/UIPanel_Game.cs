using Landsong.UISystem;
using Moyo.Unity;
using UnityEngine;

public class UIPanel_Game : UIPanelBase
{
    [SerializeField] private GamePanel_HUD hudPanel;
    [SerializeField] private GamePanel_Inventory inventoryPanel;
    [SerializeField] private GamePanel_Building buildingPanel;
    [SerializeField] private GamePanel_BuildingPlacementControls buildingPlacementControls;

    private void Reset()
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
    }

    private void Awake()
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
    }


    public GamePanel_BuildingPlacementControls BuildingPlacementControls => buildingPlacementControls;


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
}
