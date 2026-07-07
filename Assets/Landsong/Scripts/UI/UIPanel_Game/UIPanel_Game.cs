using System;
using System.Threading.Tasks;
using Landsong;
using Landsong.InputSystem;
using Landsong.UISystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;



public class UIPanel_Game : UIPanelBase
{
    [SerializeField] private RectTransform gameMarkRoot;
    [SerializeField] private GamePanel_HUD hudPanel;
    [SerializeField] private GamePanel_Inventory inventoryPanel;
    [SerializeField] private GamePanel_Technology technologyPanel;
    [SerializeField] private GamePanel_Building buildingPanel;
    [SerializeField] private GamePanel_BuildingPlacementControls buildingPlacementControls;
    [SerializeField] private GamePanel_BuildingStatusOverview buildingStatusOverview;
    [SerializeField] private GamePanel_Quest questPanel;
    [SerializeField] private GamePanel_Expedition expeditionPanel;
    [SerializeField] private GamePanel_Talent talentPanel;
    [SerializeField] private GamePanel_Inheritance inheritancePanel;
    [SerializeField] private GamePanel_Pause pausePanel;

    [SerializeField] private GamePanel_BuildingEventMessageList buildingEventMessageList;
    [SerializeField] private GamePanel_SelectedBuildingOverview selectedBuildingOverview;
    [SerializeField] private Popup_BuildingDetails buildingDetailPopup;
    [SerializeField] private GamePanel_BuildingSelectionView buildingSelectionView;

    private InputController subscribedInputController;

    public RectTransform GameMarkRoot => gameMarkRoot;
    public GamePanel_BuildingEventMessageList BuildingEventMessageList => buildingEventMessageList;
    public GamePanel_SelectedBuildingOverview SelectedBuildingOverview => selectedBuildingOverview;
    public Popup_BuildingDetails BuildingDetailPopup => buildingDetailPopup;
    public GamePanel_BuildingSelectionView BuildingSelectionView => buildingSelectionView;
    public GamePanel_BuildingPlacementControls BuildingPlacementControls => buildingPlacementControls;
    public GamePanel_Technology TechnologyPanel => technologyPanel;
    public GamePanel_Quest QuestPanel => questPanel;
    public GamePanel_Expedition ExpeditionPanel => expeditionPanel;
    public GamePanel_Talent TalentPanel => talentPanel;
    public GamePanel_Inheritance InheritancePanel => inheritancePanel;
    public GamePanel_Pause PausePanel => pausePanel;

    private void Reset()
    {
        GetReference();
    }

    private void Awake()
    {
        GetReference();
    }

    private void OnDestroy()
    {
        UnsubscribeInputController();
    }

    public override Task OnCreateAsync()
    {
        GetReference();
        SubscribeInputController();
        return base.OnCreateAsync();
    }

    public override Task OnOpenAsync(object args)
    {
        GetReference();
        SubscribeInputController();
        return base.OnOpenAsync(args);
    }

    public override Task OnReleaseAsync()
    {
        UnsubscribeInputController();
        return base.OnReleaseAsync();
    }

    public void Show_HUD()
    {
        GetReference();
        HideAllPanels();

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
        GetReference();
        HideAllPanels();

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
        if (inventoryPanel != null)
        {
            inventoryPanel.Hide();
        }

        Show_HUD();
    }

    public void Show_Technology()
    {
        GetReference();
        HideAllPanels();

        if (technologyPanel != null)
        {
            technologyPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no technology panel assigned.", this);
        }
    }

    internal void Hide_Technology()
    {
        if (technologyPanel != null)
        {
            technologyPanel.Hide();
        }

        Show_HUD();
    }

    public void Show_Quest()
    {
        GetReference();
        HideAllPanels();

        if (questPanel != null)
        {
            questPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no quest panel assigned.", this);
        }
    }

    public void Show_Quest(GameQuestState focusedQuest)
    {
        GetReference();
        HideAllPanels();

        if (questPanel != null)
        {
            questPanel.Show(focusedQuest);
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no quest panel assigned.", this);
        }
    }

    internal void Hide_Quest()
    {
        if (questPanel != null)
        {
            questPanel.Hide();
        }

        Show_HUD();
    }

    public void Show_Expedition()
    {
        GetReference();
        HideAllPanels();

        if (expeditionPanel != null)
        {
            expeditionPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no expedition panel assigned.", this);
        }
    }

    internal void Hide_Expedition()
    {
        if (expeditionPanel != null)
        {
            expeditionPanel.Hide();
        }

        Show_HUD();
    }

    public void Show_Talent()
    {
        GetReference();
        HideAllPanels();

        if (talentPanel != null)
        {
            talentPanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no talent panel assigned.", this);
        }
    }

    internal void Hide_Talent()
    {
        if (talentPanel != null)
        {
            talentPanel.Hide();
        }

        Show_HUD();
    }

    public void Show_Inheritance()
    {
        GetReference();
        HideAllPanels();

        if (inheritancePanel != null)
        {
            inheritancePanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no inheritance panel assigned.", this);
        }
    }

    internal void Hide_Inheritance()
    {
        if (inheritancePanel != null)
        {
            inheritancePanel.Hide();
        }

        Show_HUD();
    }


    public void Show_Building()
    {
        GetReference();
        HideAllPanels();

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
        if (buildingPanel != null)
        {
            buildingPanel.Hide();
        }

        Show_HUD();

    }



    internal void Show_Overview()
    {
        GetReference();
        HideAllPanels();

        if (buildingStatusOverview != null)
        {
            buildingStatusOverview.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no building status overview assigned.", this);
        }
    }

    internal void Hide_Overview()
    {
        if (buildingStatusOverview != null)
        {
            buildingStatusOverview.Hide();
        }

        Show_HUD();
    }




    private void HideAllPanels()
    {
        hudPanel?.Hide();
        inventoryPanel?.Hide();
        technologyPanel?.Hide();
        buildingPanel?.Hide();
        buildingStatusOverview?.Hide();
        questPanel?.Hide();
        expeditionPanel?.Hide();
        talentPanel?.Hide();
        inheritancePanel?.Hide();
        buildingDetailPopup?.Hide();
        pausePanel?.Hide();
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

        if (technologyPanel == null)
        {
            technologyPanel = GetComponentInChildren<GamePanel_Technology>(true);
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
            buildingDetailPopup = GetComponentInChildren<Popup_BuildingDetails>(true);
        }
        if (buildingSelectionView == null)
        {
            buildingSelectionView = GetComponentInChildren<GamePanel_BuildingSelectionView>(true);
        }
        if (buildingSelectionView == null)
        {
            buildingSelectionView = gameObject.AddComponent<GamePanel_BuildingSelectionView>();
        }
        if (buildingStatusOverview == null)
        {
            buildingStatusOverview = GetComponentInChildren<GamePanel_BuildingStatusOverview>(true);
        }

        if (questPanel == null)
        {
            questPanel = GetComponentInChildren<GamePanel_Quest>(true);
        }

        if (expeditionPanel == null)
        {
            expeditionPanel = GetComponentInChildren<GamePanel_Expedition>(true);
        }

        if (talentPanel == null)
        {
            talentPanel = GetComponentInChildren<GamePanel_Talent>(true);
        }

        if (inheritancePanel == null)
        {
            inheritancePanel = GetComponentInChildren<GamePanel_Inheritance>(true);
        }

        if (pausePanel == null)
        {
            pausePanel = GetComponentInChildren<GamePanel_Pause>(true);
        }
    }

    private void SubscribeInputController()
    {
        var inputController = InputController.Instance;
        if (subscribedInputController == inputController)
        {
            return;
        }

        UnsubscribeInputController();

        if (inputController == null)
        {
            return;
        }

        inputController.OpenBuildingPanelRequested += HandleOpenBuildingPanelRequested;
        inputController.OpenInventoryPanelRequested += HandleOpenInventoryPanelRequested;
        inputController.BackRequested += HandleBackRequested;
        subscribedInputController = inputController;
    }

    private void UnsubscribeInputController()
    {
        if (subscribedInputController == null)
        {
            return;
        }

        subscribedInputController.OpenBuildingPanelRequested -= HandleOpenBuildingPanelRequested;
        subscribedInputController.OpenInventoryPanelRequested -= HandleOpenInventoryPanelRequested;
        subscribedInputController.BackRequested -= HandleBackRequested;
        subscribedInputController = null;
    }

    private void HandleOpenBuildingPanelRequested()
    {
        Show_Building();
    }

    private void HandleOpenInventoryPanelRequested()
    {
        Show_Inventory();
    }

    private void HandleBackRequested()
    {
        if (pausePanel != null && pausePanel.IsVisible)
        {
            Hide_Pause();
            return;
        }

        Show_Pause();
    }

    internal void Show_Pause()
    {
        GetReference();
        HideAllPanels();

        if (pausePanel != null)
        {
            pausePanel.Show();
        }
        else
        {
            Debug.LogWarning($"{nameof(UIPanel_Game)} has no pause panel assigned.", this);
        }
    }

    internal void Hide_Pause()
    {
        if (pausePanel != null)
        {
            pausePanel.Hide();
        }

        Show_HUD();
    }
}
