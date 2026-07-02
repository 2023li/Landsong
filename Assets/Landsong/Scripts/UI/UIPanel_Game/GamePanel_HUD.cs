using System;
using System.Collections;
using Landsong;
using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.InventorySystem;
using Landsong.TurnSystem;
using Landsong.UISystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GamePanel_HUD : MonoBehaviour
{
    private UIPanel_Game gamePanel;
    private GameSystem gameSystem;
    private DynastyService dynasty;
    private InventoryService inventory;
    private TurnService turn;
    private bool subscribedToDynasty;
    private bool subscribedToInventory;
    private bool subscribedToTurn;
    private bool subscribedToBuildingSelection;
    private BuildingSelectionController subscribedBuildingSelection;
    private Coroutine turnProcessingDisplayRoutine;

    private void Awake()
    {
        gamePanel = GetComponentInParent<UIPanel_Game>();
        BindButtons();
    }
    private void OnEnable()
    {
        ResolveRuntimeServices();
        SubscribeRuntimeServices();
        SubscribeBottomBarSelection();
        RefreshTopBar();
        RefreshBottomBar();
        if (gameSystem != null && gameSystem.IsAdvancingTurn)
        {
            BeginTurnProcessingDisplay();
        }
    }

    private void Start()
    {
        SubscribeBottomBarSelection();
        RefreshBottomBar();
    }

    private void OnDisable()
    {
        UnsubscribeRuntimeServices();
        UnsubscribeBottomBarSelection();
        StopTurnProcessingDisplay();
    }

    private void OnDestroy()
    {
        UnsubscribeRuntimeServices();
        UnsubscribeBottomBarSelection();
        UnbindButtons();
    }

    #region 顶部栏
    //阶段 
    [SerializeField] private TMP_Text txt_Stage;
    //人口 
    [SerializeField] private TMP_Text txt_Population;
    //金币 从仓库获取 Item_金币的数量
    [SerializeField] private TMP_Text txt_Gold;

    [SerializeField] private TMP_Text txt_TurnCount;

    [SerializeField] private ItemDefinition goldItemDefinition;

    #endregion

    [SerializeField] private Button btn_建造;
    [SerializeField] private Button btn_仓库;
    [SerializeField] private Button btn_概览;
    [SerializeField] private Button btn_下回合;
    [SerializeField] private GameObject go_回合处理显示;
    [SerializeField, Min(0f)] private float minimumTurnProcessingDisplaySeconds = 1f;

    private void BindButtons()
    {
        if (gamePanel == null)
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} cannot bind buttons because {nameof(UIPanel_Game)} was not found.", this);
            return;
        }

        if (btn_建造 != null)
        {
            btn_建造.onClick.RemoveListener(gamePanel.Show_Building);
            btn_建造.onClick.AddListener(gamePanel.Show_Building);
        }
        else
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} has no build button assigned.", this);
        }

        if (btn_仓库 != null)
        {
            btn_仓库.onClick.RemoveListener(gamePanel.Show_Inventory);
            btn_仓库.onClick.AddListener(gamePanel.Show_Inventory);
        }
        else
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} has no inventory button assigned.", this);
        }


        if (btn_概览 != null)
        {
            btn_概览.onClick.RemoveListener(gamePanel.Show_Overview);
            btn_概览.onClick.AddListener(gamePanel.Show_Overview);
        }
        else
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} has no 概览 button assigned.", this);
        }

        if (btn_下回合 != null)
        {
            btn_下回合.onClick.RemoveListener(HandleNextTurnClicked);
            btn_下回合.onClick.AddListener(HandleNextTurnClicked);
        }
        else
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} has no 回合 button assigned.", this);
        }
    }

    private void UnbindButtons()
    {
        if (gamePanel != null && btn_建造 != null)
        {
            btn_建造.onClick.RemoveListener(gamePanel.Show_Building);
        }

        if (gamePanel != null && btn_仓库 != null)
        {
            btn_仓库.onClick.RemoveListener(gamePanel.Show_Inventory);
        }

        if (gamePanel != null && btn_概览 != null)
        {
            btn_概览.onClick.RemoveListener(gamePanel.Show_Overview);
        }

        if (btn_下回合 != null)
        {
            btn_下回合.onClick.RemoveListener(HandleNextTurnClicked);
        }
    }

    private void ResolveRuntimeServices()
    {
        gameSystem = GameSystem.Instance;
        dynasty = gameSystem == null ? null : gameSystem.Dynasty;
        inventory = gameSystem == null ? null : gameSystem.Inventory;
        turn = gameSystem == null ? null : gameSystem.Turn;
    }

    private void SubscribeRuntimeServices()
    {
        SubscribeDynasty();
        SubscribeInventory();
        SubscribeTurn();
    }

    private void UnsubscribeRuntimeServices()
    {
        UnsubscribeDynasty();
        UnsubscribeInventory();
        UnsubscribeTurn();
    }

    private void SubscribeDynasty()
    {
        if (subscribedToDynasty || dynasty == null)
        {
            return;
        }

        dynasty.PopulationChanged += HandlePopulationChanged;
        dynasty.StageChanged += HandleStageChanged;
        subscribedToDynasty = true;
    }

    private void UnsubscribeDynasty()
    {
        if (!subscribedToDynasty || dynasty == null)
        {
            subscribedToDynasty = false;
            return;
        }

        dynasty.PopulationChanged -= HandlePopulationChanged;
        dynasty.StageChanged -= HandleStageChanged;
        subscribedToDynasty = false;
    }

    private void SubscribeInventory()
    {
        if (subscribedToInventory || inventory == null)
        {
            return;
        }

        inventory.InventoryChanged += HandleInventoryChanged;
        subscribedToInventory = true;
    }

    private void UnsubscribeInventory()
    {
        if (!subscribedToInventory || inventory == null)
        {
            subscribedToInventory = false;
            return;
        }

        inventory.InventoryChanged -= HandleInventoryChanged;
        subscribedToInventory = false;
    }

    private void SubscribeTurn()
    {
        if (subscribedToTurn || turn == null)
        {
            return;
        }

        turn.BeforeTurnAdvanced += HandleBeforeTurnAdvanced;
        turn.TurnAdvanced += HandleTurnAdvanced;
        subscribedToTurn = true;
    }

    private void UnsubscribeTurn()
    {
        if (!subscribedToTurn || turn == null)
        {
            subscribedToTurn = false;
            return;
        }

        turn.BeforeTurnAdvanced -= HandleBeforeTurnAdvanced;
        turn.TurnAdvanced -= HandleTurnAdvanced;
        subscribedToTurn = false;
    }

    private void RefreshTopBar()
    {
        RefreshStage();
        RefreshPopulation();
        RefreshGold();
        RefreshTurnCount();
        RefreshTurnControls();
    }

    private void RefreshStage()
    {
        txt_Stage.text = dynasty == null ? string.Empty : dynasty.Stage.ToString();
    }

    private void RefreshPopulation()
    {
        txt_Population.text = dynasty == null
            ? "0/0"
            : $"{dynasty.EmployedPopulation}/{dynasty.Population}";
    }

    private void RefreshGold()
    {
        var itemId = goldItemDefinition == null ? string.Empty : goldItemDefinition.ItemId;
        var quantity = inventory == null || string.IsNullOrWhiteSpace(itemId)
            ? 0
            : inventory.GetQuantity(itemId);
        txt_Gold.text = quantity.ToString();
    }

    private void RefreshTurnCount()
    {
        if (txt_TurnCount != null)
        {
            txt_TurnCount.text = gameSystem == null ? string.Empty : gameSystem.CurrentTurn.ToString();
        }
    }

    private void RefreshTurnControls()
    {
        var isProcessing = gameSystem != null && gameSystem.IsAdvancingTurn;
        SetNextTurnButtonVisible(!isProcessing);
        SetActive(go_回合处理显示, isProcessing);
    }

    private void HandleNextTurnClicked()
    {
        ResolveRuntimeServices();
        if (gameSystem == null || gameSystem.IsGameOver || gameSystem.IsAdvancingTurn)
        {
            RefreshTurnControls();
            return;
        }

        BeginTurnProcessingDisplay();
        gameSystem.NextTurn();
    }

    private void HandleBeforeTurnAdvanced(TurnService changedTurn)
    {
        turn = changedTurn;
        BeginTurnProcessingDisplay();
    }

    private void HandleTurnAdvanced(TurnService changedTurn, TurnAdvanceSummary summary)
    {
        turn = changedTurn;
        RefreshTurnCount();
    }

    private void BeginTurnProcessingDisplay()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (turnProcessingDisplayRoutine != null)
        {
            StopCoroutine(turnProcessingDisplayRoutine);
        }

        turnProcessingDisplayRoutine = StartCoroutine(TurnProcessingDisplayRoutine());
    }

    private IEnumerator TurnProcessingDisplayRoutine()
    {
        SetNextTurnButtonVisible(false);
        SetActive(go_回合处理显示, true);

        var startedAt = Time.unscaledTime;
        while (gameSystem != null && gameSystem.IsAdvancingTurn)
        {
            yield return null;
        }

        while (Time.unscaledTime - startedAt < minimumTurnProcessingDisplaySeconds)
        {
            yield return null;
        }

        turnProcessingDisplayRoutine = null;
        SetActive(go_回合处理显示, false);
        SetNextTurnButtonVisible(gameSystem == null || !gameSystem.IsGameOver);
        RefreshTurnCount();
    }

    private void StopTurnProcessingDisplay()
    {
        if (turnProcessingDisplayRoutine != null)
        {
            StopCoroutine(turnProcessingDisplayRoutine);
            turnProcessingDisplayRoutine = null;
        }

        SetActive(go_回合处理显示, false);
        SetNextTurnButtonVisible(gameSystem == null || !gameSystem.IsAdvancingTurn);
    }

    private void SetNextTurnButtonVisible(bool visible)
    {
        if (btn_下回合 == null)
        {
            return;
        }

        btn_下回合.gameObject.SetActive(visible);
        btn_下回合.interactable = visible && gameSystem != null && !gameSystem.IsGameOver;
    }

    private void HandlePopulationChanged(DynastyService changedDynasty)
    {
        dynasty = changedDynasty;
        RefreshPopulation();
    }

    private void HandleStageChanged(DynastyService changedDynasty)
    {
        dynasty = changedDynasty;
        RefreshStage();
    }

    private void HandleInventoryChanged(InventoryService changedInventory)
    {
        inventory = changedInventory;
        RefreshGold();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    #region 底部栏

    [SerializeField, FoldoutGroup("底栏")] private GameObject go_底部栏信息根对象;
    [SerializeField, FoldoutGroup("底栏")] private TMP_Text txt_当前选中的建筑的名称;
    [SerializeField, FoldoutGroup("底栏")] private TMP_Text txt_选中的建筑的概览信息;

    private void HandleBuildingSelectionChanged(BuildingBase selectedBuilding)
    {
        RefreshBottomBar(selectedBuilding);
    }

    private void SubscribeBottomBarSelection()
    {
        var selectionController = FindFirstObjectByType<BuildingSelectionController>(FindObjectsInactive.Include);
        if (selectionController == null)
        {
            HideBottomBar();
            return;
        }

        if (subscribedToBuildingSelection && subscribedBuildingSelection == selectionController)
        {
            return;
        }

        UnsubscribeBottomBarSelection();
        selectionController.SelectionChanged += HandleBuildingSelectionChanged;
        subscribedBuildingSelection = selectionController;
        subscribedToBuildingSelection = true;
    }

    private void UnsubscribeBottomBarSelection()
    {
        if (!subscribedToBuildingSelection || subscribedBuildingSelection == null)
        {
            subscribedBuildingSelection = null;
            subscribedToBuildingSelection = false;
            return;
        }

        subscribedBuildingSelection.SelectionChanged -= HandleBuildingSelectionChanged;
        subscribedBuildingSelection = null;
        subscribedToBuildingSelection = false;
    }

    private void RefreshBottomBar()
    {
        RefreshBottomBar(subscribedBuildingSelection == null ? null : subscribedBuildingSelection.SelectedBuilding);
    }

    private void RefreshBottomBar(BuildingBase selectedBuilding)
    {
        if (selectedBuilding == null || !selectedBuilding.HasDefinition)
        {
            HideBottomBar();
            return;
        }

        SetActive(go_底部栏信息根对象, true);

        if (txt_当前选中的建筑的名称 != null)
        {
            txt_当前选中的建筑的名称.text = selectedBuilding.Definition.DisplayName;
        }

        if (txt_选中的建筑的概览信息 != null)
        {
            BuildingStatusDisplayData data = BuildingStatusUIFormatter.CreateDisplayData(selectedBuilding);
            txt_选中的建筑的概览信息.text = FormatBottomBarInfo(data);
        }
    }

    private static string FormatBottomBarInfo(BuildingStatusDisplayData data)
    {
        if (string.IsNullOrWhiteSpace(data.BaseInfoText))
        {
            return data.StatusInfoText;
        }

        if (string.IsNullOrWhiteSpace(data.StatusInfoText))
        {
            return data.BaseInfoText;
        }

        return $"{data.BaseInfoText}   {data.StatusInfoText}";
    }

    private void HideBottomBar()
    {
        SetActive(go_底部栏信息根对象, false);
    }

    #endregion

}
