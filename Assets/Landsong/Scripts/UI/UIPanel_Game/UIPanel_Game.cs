using Landsong.UISystem;
using Moyo.Unity;
using UnityEngine;

public class UIPanel_Game : UIPanelBase
{
    [SerializeField] private GamePanel_HUD hudPanel;
    [SerializeField] private GamePanel_Inventory inventoryPanel;
    [SerializeField] private GamePanel_Building buildingPanel;

    private void Reset()
    {
        ResolvePanels();
    }

    private void OnValidate()
    {
        ResolvePanels();
    }

    private void Awake()
    {
        ResolvePanels();
    }

    public void Show_HUD()
    {
        ResolvePanels();
        HideAll();
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
        ResolvePanels();
        HideAll();
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
        ResolvePanels();
        HideAll();
        if (buildingPanel != null)
        {
            buildingPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no building panel assigned.", this);
        }
    }

    private void HideAll()
    {
        hudPanel?.Hide();
        inventoryPanel?.Hide();
        buildingPanel?.Hide();
    }

    private void ResolvePanels()
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
    }
}
