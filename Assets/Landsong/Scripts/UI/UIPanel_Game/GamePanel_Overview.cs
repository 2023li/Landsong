using Landsong.UISystem;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GamePanel_Overview : MonoBehaviour
{
    public enum OverviewTab
    {
        Economy,
        Buildings
    }

    [Header("切换")]
    [SerializeField] private ToggleGroup toggleGroup;
    [SerializeField] private Toggle tgo_建筑概览面板;
    [SerializeField] private Toggle tgo_经济概览面板;
    [SerializeField] private Button btn_关闭;

    [Header("子面板")]
    [SerializeField] private GamePanel_BuildingStatusOverview buildingStatusOverview;
    [SerializeField] private GamePanel_EconomyForecast economyForecast;

    [Header("默认行为")]
    [SerializeField] private OverviewTab defaultTab = OverviewTab.Economy;
    [SerializeField] private bool rememberLastTab = true;

    private UIPanel_Game gamePanel;
    private OverviewTab activeTab;
    private bool hasSelectedTab;
    private bool suppressToggleCallbacks;

    public OverviewTab ActiveTab => activeTab;
    public GamePanel_EconomyForecast EconomyForecastPanel => economyForecast;
    public GamePanel_BuildingStatusOverview BuildingStatusOverview => buildingStatusOverview;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureToggleGroup();
        SubscribeControls();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureToggleGroup();
        SubscribeControls();
        SelectTab(hasSelectedTab && rememberLastTab ? activeTab : defaultTab, true);
    }

    private void OnDestroy()
    {
        UnsubscribeControls();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        SelectTab(hasSelectedTab && rememberLastTab ? activeTab : defaultTab, true);
    }

    public void Show(OverviewTab tab)
    {
        gameObject.SetActive(true);
        SelectTab(tab, true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SelectEconomyTab() => SelectTab(OverviewTab.Economy, true);

    public void SelectBuildingTab() => SelectTab(OverviewTab.Buildings, true);

    public void RefreshActivePanel()
    {
        if (activeTab == OverviewTab.Buildings)
        {
            buildingStatusOverview?.Refresh();
            return;
        }

        economyForecast?.RefreshForecast();
    }

    private void SelectTab(OverviewTab tab, bool refresh)
    {
        activeTab = tab;
        hasSelectedTab = true;

        suppressToggleCallbacks = true;
        if (tgo_经济概览面板 != null)
        {
            tgo_经济概览面板.SetIsOnWithoutNotify(tab == OverviewTab.Economy);
        }

        if (tgo_建筑概览面板 != null)
        {
            tgo_建筑概览面板.SetIsOnWithoutNotify(tab == OverviewTab.Buildings);
        }
        suppressToggleCallbacks = false;

        SetChildActive(economyForecast, tab == OverviewTab.Economy);
        SetChildActive(buildingStatusOverview, tab == OverviewTab.Buildings);

        if (refresh)
        {
            RefreshActivePanel();
        }
    }

    private void HandleEconomyToggleChanged(bool isOn)
    {
        if (!suppressToggleCallbacks && isOn)
        {
            SelectTab(OverviewTab.Economy, true);
        }
    }

    private void HandleBuildingToggleChanged(bool isOn)
    {
        if (!suppressToggleCallbacks && isOn)
        {
            SelectTab(OverviewTab.Buildings, true);
        }
    }

    private void HandleCloseClicked()
    {
        gamePanel?.Hide_Overview();
    }

    private void ResolveReferences()
    {
        gamePanel ??= GetComponentInParent<UIPanel_Game>(true);
        buildingStatusOverview ??= GetComponentInChildren<GamePanel_BuildingStatusOverview>(true);
        economyForecast ??= GetComponentInChildren<GamePanel_EconomyForecast>(true);
    }

    private void ConfigureToggleGroup()
    {
        if (toggleGroup == null)
        {
            toggleGroup = tgo_经济概览面板 != null
                ? tgo_经济概览面板.group
                : tgo_建筑概览面板 != null
                    ? tgo_建筑概览面板.group
                    : null;
        }

        if (toggleGroup == null)
        {
            return;
        }

        toggleGroup.allowSwitchOff = false;
        if (tgo_经济概览面板 != null)
        {
            tgo_经济概览面板.group = toggleGroup;
        }

        if (tgo_建筑概览面板 != null)
        {
            tgo_建筑概览面板.group = toggleGroup;
        }
    }

    private void SubscribeControls()
    {
        UnsubscribeControls();
        tgo_经济概览面板?.onValueChanged.AddListener(HandleEconomyToggleChanged);
        tgo_建筑概览面板?.onValueChanged.AddListener(HandleBuildingToggleChanged);
        btn_关闭?.onClick.AddListener(HandleCloseClicked);
    }

    private void UnsubscribeControls()
    {
        tgo_经济概览面板?.onValueChanged.RemoveListener(HandleEconomyToggleChanged);
        tgo_建筑概览面板?.onValueChanged.RemoveListener(HandleBuildingToggleChanged);
        btn_关闭?.onClick.RemoveListener(HandleCloseClicked);
    }

    private static void SetChildActive(Component child, bool active)
    {
        if (child != null && child.gameObject.activeSelf != active)
        {
            child.gameObject.SetActive(active);
        }
    }
}
