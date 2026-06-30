using Landsong.UISystem;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel_Game : UIPanelBase
{
    [SerializeField] private GamePanel_HUD hudPanel;
    [SerializeField] private GamePanel_Inventory inventoryPanel;
    [SerializeField] private GamePanel_Building buildingPanel;

    private void Reset()
    {
        hudPanel = GetComponentInChildren<GamePanel_HUD>(true);
        inventoryPanel = GetComponentInChildren<GamePanel_Inventory>(true);
        buildingPanel = GetComponentInChildren<GamePanel_Building>(true);
    }



    public void Show_HUD()
    {
        HideAll();
        hudPanel.Show();
    }
    public void Show_Inventory()
    {
        HideAll();
        inventoryPanel.Show();
    }
    public void Show_Building()
    {
        HideAll();
        buildingPanel.Show();
    }
    private void HideAll()
    {
        hudPanel.Hide();
        inventoryPanel.Hide();
        buildingPanel.Hide();
    }




}
