using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.AudioSystem;
using Landsong.GameEventSystem;
using Landsong.GridSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{

    [Serializable]
    public abstract class BuildingDataBase
    {
        public List<BuildingModuleStateEntry> ModuleStates;
    }

    /// <summary>
    /// 建筑家族唯一运行时 Prefab 根节点上的 Facade。
    /// 静态数据来自 BuildingFamilyDefinition；施工和等级是同一实例的运行时状态。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BuildingBase : MonoBehaviour, IBuildingFunctionBlockSource
    {
        private static readonly IReadOnlyList<BuildingRuntimeStatus> EmptyRuntimeStatuses =
            Array.Empty<BuildingRuntimeStatus>();

        private static readonly IReadOnlyList<BuildingFunctionBlockEntry> EmptyFunctionBlockEntries =
            Array.Empty<BuildingFunctionBlockEntry>();

        private static readonly IReadOnlyList<BuildingModuleBase> EmptyBuildingModules =
            Array.Empty<BuildingModuleBase>();

        [Tooltip("建筑家族静态定义。一个家族只能对应一个运行时 Prefab。")]
        [SerializeField] private BuildingFamilyDefinition familyDefinition;

        [Tooltip("同一建筑实例跨施工和所有等级保持不变的身份与阶段。")]
        [SerializeField] private BuildingRuntimeIdentity runtimeIdentity = new BuildingRuntimeIdentity();

        [Tooltip("是否已经拥有有效的格子坐标。未放置或已清除占地时为 false。")]
        [SerializeField] private bool hasGridPosition;

        [Tooltip("建筑占地原点格子坐标。多格建筑以左下角/原点格作为占地起点。")]
        [SerializeField] private GridPosition gridPosition;

        [Tooltip("格子系统中的唯一占用 ID，用来识别和清理该建筑占用的一组格子。不是人口或居民 ID。")]
        [SerializeField] private string gridOccupancyId;

        [Tooltip("为 true 时，该建筑可作为居民房等建筑的资源连接点。")]
        [SerializeField] private bool isResourceProviderPoint;

        [Tooltip("资源消费者优先选择数值更高的可达资源提供点；同优先级时选择路径行动力代价更低的提供点。")]
        [SerializeField, LabelText("资源提供优先级")] private int resourceProviderPriority;

        private List<BuildingModuleBase> buildingModules = new List<BuildingModuleBase>();

        [Tooltip("建筑用于寻路和范围判断的行动力。普通格默认消耗 10 点，道路等地形由 GridMapBehaviour 配置。")]
        [SerializeField, Min(0)] private int buildingActionPower = 100;


        private float doubleClickInterval = 0.3f;

        //点击回调
        //是否播放点击缩放反馈
        private bool playClickScaleFeedback = true;
        //点击缩放倍率
        private float clickScaleMultiplier = 1.12f;
        //点击缩放时长
        private float clickScaleDuration = 0.18f;

        [Header("Click Audio")]
        [SerializeField, LabelText("点击时音效")] private AudioClip clickSound;
        [SerializeField, LabelText("双击时音效")] private AudioClip doubleClickSound;

        [Tooltip("建筑视觉控制器。BuildingBase 只持有引用，具体动画播放逻辑由 BuildingView 控制。")]
        [SerializeField] private BuildingView view;

        [SerializeField] private BuildingPresentationController presentationController;

      
        // 当前建筑接入的游戏系统。库存、回合、全局服务都从 GameSystem 获取。
        private Landsong.GameSystem gameSystem;
        private GridMapBehaviour gridMap;
       
        // 是否已经执行过 Initialize。用于防止未初始化建筑进入放置或回合流程。
        private bool initialized;

        // 是否已经注册到 GameSystem。用于触发 OnRegistered / OnUnregistered。
        private bool isRegistered;

        // 是否已经触发过 OnPlaced。格子信息可能早于 Start 写入，因此需要单独记录。
        private bool placedNotified;
        private bool isDemolishing;

        private Coroutine clickScaleCoroutine;
        private Transform clickScaleTarget;
        private Vector3 clickScaleOriginalScale;
        private BuildingModuleSetDefinition runtimeModuleSet;
        private string lastConstructionFailureStatusId = string.Empty;
        private string lastConstructionFailureStatusText = string.Empty;
        private IReadOnlyList<BuildingResourceChange> lastConstructionRewards =
            Array.Empty<BuildingResourceChange>();

        public event Action<BuildingBase> StateChanged;
        public event Action<BuildingBase> Clicked;
        public event Action<BuildingBase> DoubleClicked;
        public event Action<BuildingStageChangedEvent> StageChanged;
        public event Action<BuildingLevelChangedEvent> LevelChanged;

        public AudioClip ClickSound => clickSound;
        public AudioClip DoubleClickSound => doubleClickSound;
        internal float DoubleClickInterval => doubleClickInterval;

        public BuildingFamilyDefinition FamilyDefinition => familyDefinition;
        public BuildingDefinition Definition => familyDefinition?.Definition;
        public string FamilyId => Definition == null ? string.Empty : Definition.FamilyId;
        public string InstanceId => runtimeIdentity == null ? string.Empty : runtimeIdentity.InstanceId;
        public BuildingLifecycleStage Stage => runtimeIdentity == null
            ? BuildingLifecycleStage.Operational
            : runtimeIdentity.Stage;
        public int CurrentLevel => runtimeIdentity == null ? 1 : runtimeIdentity.Level;
        public string StyleId => runtimeIdentity == null ? string.Empty : runtimeIdentity.StyleId;
        public int ConstructionProgress => runtimeIdentity == null ? 0 : runtimeIdentity.ConstructionProgress;
        public int CurrentConstructionTurn => !IsUnderConstruction
            ? 0
            : Mathf.Clamp(
                ConstructionProgress + 1,
                1,
                Mathf.Max(1, familyDefinition?.Construction?.RequiredTurns ?? 1));
        public bool IsUnderConstruction => Stage == BuildingLifecycleStage.Construction;
        public bool IsOperational => Stage == BuildingLifecycleStage.Operational;
        public BuildingPresentationController PresentationController => presentationController;

        // 当前建筑的视觉控制器。
        public BuildingView View
        {
            get
            {
                ResolvePresentationController();
                ResolveView();
                return view;
            }
        }

        // 当前建筑接入的游戏系统；未注册时为 null。
        public Landsong.GameSystem GameSystem => gameSystem;
        public GridMapBehaviour GridMap => gridMap;

        // 是否已经完成 Initialize。
        public bool IsInitialized => initialized;

        // 是否已经注册到 GameSystem。
        public bool IsRegistered => isRegistered;
        public bool IsDemolishing => isDemolishing;

        // 是否已经持有有效 BuildingDefinition 数据。
        public bool HasDefinition => Definition != null
                                     && Definition.IsValid
                                     && (familyDefinition == null || familyDefinition.RuntimePrefab != null);

        // 是否已经写入格子坐标。
        public bool HasGridPosition => hasGridPosition;

        // 是否已经拥有放置信息。目前等价于 HasGridPosition。
        public bool HasPlacement => hasGridPosition;

        // 建筑占地原点格子坐标。
        public GridPosition GridPosition => gridPosition;

        // GridPosition 的语义化别名，用于强调这是占地原点。
        public GridPosition Origin => gridPosition;

        // 格子系统中的唯一占用 ID，用于清理该建筑占用的格子。
        public string GridOccupancyId => gridOccupancyId;

        // 是否可作为居民房等建筑的资源连接点。
        public bool IsResourceProviderPoint => isResourceProviderPoint;

        // 资源消费者选择提供点时使用的优先级。数值越高越优先。
        public int ResourceProviderPriority => resourceProviderPriority;

        // 当前建筑挂载的可选能力模块。
        public IReadOnlyList<BuildingModuleBase> BuildingModules => buildingModules ?? EmptyBuildingModules;

        // 建筑用于寻路和范围判断的行动力。
        public int BuildingActionPower => Mathf.Max(0, buildingActionPower);
        public IReadOnlyList<BuildingResourceChange> LastConstructionRewards =>
            lastConstructionRewards ?? Array.Empty<BuildingResourceChange>();

        /// <summary>
        /// 由建筑数值导表器更新 Runtime Prefab 上的三个纯数据字段。
        /// Prefab 结构、组件和表现引用仍由建筑编辑器维护。
        /// </summary>
        public void ConfigureNumericAuthoringData(
            bool resourceProviderPoint,
            int providerPriority,
            int actionPower)
        {
            isResourceProviderPoint = resourceProviderPoint;
            resourceProviderPriority = providerPriority;
            buildingActionPower = Mathf.Max(0, actionPower);
        }

        // 根据 Definition 的尺寸和当前原点计算出的占地范围。
        public GridFootprint Footprint => Definition == null
            ? new GridFootprint(gridPosition, Vector2Int.one)
            : Definition.CreateFootprint(gridPosition);

        public bool RequiresConnectionType(string connectionTypeId)
        {
            connectionTypeId = BuildingConnectionTypes.Normalize(connectionTypeId);
            if (string.IsNullOrEmpty(connectionTypeId))
            {
                return false;
            }

            if (IsUnderConstruction
                && string.Equals(connectionTypeId, BuildingConnectionTypes.Resource, StringComparison.Ordinal)
                && familyDefinition?.Construction?.HasAnyCost() == true)
            {
                return true;
            }

            if (buildingModules == null)
            {
                return false;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                if (buildingModules[i] is IBuildingConnectionConsumerModule module
                    && buildingModules[i].IsEnabled
                    && ContainsConnectionType(module.RequiredConnectionTypeIds, connectionTypeId))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ProvidesConnectionType(string connectionTypeId)
        {
            connectionTypeId = BuildingConnectionTypes.Normalize(connectionTypeId);
            if (TryGetCapability<IBuildingConnectionProviderSource>(out var source))
            {
                return source.ProvidesConnectionType(connectionTypeId);
            }

            return string.Equals(connectionTypeId, BuildingConnectionTypes.Resource, StringComparison.Ordinal)
                   && isResourceProviderPoint;
        }

        public int GetConnectionProviderPriority(string connectionTypeId)
        {
            return TryGetCapability<IBuildingConnectionProviderSource>(out var source)
                ? source.GetConnectionProviderPriority(BuildingConnectionTypes.Normalize(connectionTypeId))
                : resourceProviderPriority;
        }

        public bool IsConnectionProviderOperational(string connectionTypeId)
        {
            if (TryGetCapability<IBuildingConnectionProviderSource>(out var source))
            {
                return source.IsConnectionProviderOperational(BuildingConnectionTypes.Normalize(connectionTypeId));
            }

            return !string.Equals(
                       BuildingConnectionTypes.Normalize(connectionTypeId),
                       BuildingConnectionTypes.Resource,
                       StringComparison.Ordinal)
                   || !TryGetCapability<IBuildingResourceProviderOperationalState>(out var resourceState)
                   || resourceState.IsResourceProviderOperational;
        }

        #region


        #endregion



        //--------------------------------架构类API-----------------------------------------
        #region 架构类API
        private void Reset()
        {
            EnsureDefinition();
            ResolvePresentationController();
            ResolveView();
            NormalizeBuildingActionPower();
            NormalizeBuildingModules();
        }

        private void OnValidate()
        {
            //EnsureDefinition();
            //ResolveView();
            //NormalizeClickFeedback();
            //NormalizeBuildingActionPower();
            //NormalizeBuildingModules();
        }

        private void Awake()
        {
            EnsureDefinition();
            EnsureRuntimeIdentity();
            CreateRuntimeModulesFromFamily();
            ResolvePresentationController();
            ResolveView();
            NormalizeClickFeedback();
            NormalizeBuildingActionPower();
            NormalizeBuildingModules();
            ApplyCurrentLevelConfiguration();
            presentationController?.Bind(this);
        }

        /// <summary>
        /// Unity Start 阶段自动注册到 GameSystem。
        /// </summary>
        private void Start()
        {
            WarnIfMissingClickCollider();
            Landsong.GameSystem.Instance.RegisterBuilding(this);
        }

        /// <summary>
        /// 由 GameSystem 注册流程调用。静态定义从 FamilyDefinition 读取。
        /// </summary>
        internal void Initialize()
        {
            if (!HasDefinition)
            {
                throw new InvalidOperationException($"Building '{name}' has no valid BuildingDefinition data.");
            }

            var previousGameSystem = gameSystem;
            var newGameSystem = Landsong.GameSystem.Instance;
            var gameSystemChanged = previousGameSystem != newGameSystem;
            gameSystem = newGameSystem;
            isRegistered = gameSystem != null;

            if (!initialized)
            {
                initialized = true;
                DispatchModuleInitialized();
            }

            if (gameSystemChanged)
            {
                if (previousGameSystem != null)
                {
                    DispatchModuleUnregistered();
                }

                if (gameSystem != null)
                {
                    DispatchModuleRegistered();
                }
            }

            NotifyPlacedIfReady();
            presentationController?.RefreshImmediate();
            NotifyStateChanged();
        }

        internal void PrepareForNewConstruction(string selectedStyleId = "")
        {
            EnsureRuntimeIdentity();
            runtimeIdentity.BeginConstruction(selectedStyleId);
            lastConstructionFailureStatusId = string.Empty;
            lastConstructionFailureStatusText = string.Empty;
            lastConstructionRewards = Array.Empty<BuildingResourceChange>();
            presentationController?.Bind(this);
            presentationController?.RefreshImmediate();
            NotifyStateChanged();
        }

        internal void RestoreRuntimeIdentity(
            string restoredInstanceId,
            BuildingLifecycleStage restoredStage,
            int restoredLevel,
            string restoredStyleId,
            int restoredConstructionProgress)
        {
            EnsureRuntimeIdentity();
            runtimeIdentity.Restore(
                restoredInstanceId,
                restoredStage,
                restoredLevel,
                restoredStyleId,
                restoredConstructionProgress);
            ApplyCurrentLevelConfiguration();
            presentationController?.RefreshImmediate();
            NotifyStateChanged();
        }

        internal bool TryApplyOperationalLevel(int targetLevel)
        {
            if (familyDefinition == null
                || !IsOperational
                || !familyDefinition.TryGetLevel(targetLevel, out var target)
                || !target.IsConfigured)
            {
                return false;
            }

            var previous = runtimeIdentity.SetLevel(targetLevel);
            try
            {
                familyDefinition.ApplyLevel(this, targetLevel);
                DispatchModuleLevelApplied(previous, targetLevel);
            }
            catch (Exception exception)
            {
                runtimeIdentity.SetLevel(previous);
                familyDefinition.ApplyLevel(this, previous);
                Debug.LogException(exception, this);
                return false;
            }

            var change = new BuildingLevelChangedEvent(this, previous, targetLevel);
            LevelChanged?.Invoke(change);
            presentationController?.RefreshForEntry(BuildingViewEntryReason.Upgraded);
            NotifyStateChanged();
            return true;
        }

        /// <summary>
        /// 放置系统只负责写入格子信息，不负责注册建筑。
        /// </summary>
        internal void SetPlacement(GridPosition newGridPosition, string newGridOccupancyId, GridMapBehaviour newGridMap = null)
        {
            gridPosition = newGridPosition;
            gridOccupancyId = string.IsNullOrWhiteSpace(newGridOccupancyId) ? string.Empty : newGridOccupancyId;
            gridMap = newGridMap;
            hasGridPosition = true;
            NotifyPlacedIfReady();
            NotifyStateChanged();
        }

        internal void ClearPlacement()
        {
            if (!hasGridPosition)
            {
                return;
            }

            if (gridMap != null && !string.IsNullOrWhiteSpace(gridOccupancyId))
            {
                gridMap.ClearOccupant(gridOccupancyId);
            }

            DetachPlacement();
        }

        private void DetachPlacement()
        {
            gridPosition = default;
            gridOccupancyId = string.Empty;
            gridMap = null;
            hasGridPosition = false;
            placedNotified = false;
            NotifyStateChanged();
        }

        private void NotifyPlacedIfReady()
        {
            if (!initialized || placedNotified || !hasGridPosition)
            {
                return;
            }

            placedNotified = true;
            if (IsOperational)
            {
                DispatchModulePlaced();
            }
            else
            {
                DispatchModuleConstructionStarted();
            }
        }

        /// <summary>
        /// GameSystem 注销建筑时调用。初始化入口不负责注销，避免 Initialize 同时承担相反语义。
        /// </summary>
        internal void NotifyUnregisteredFromGame(Landsong.GameSystem sourceGameSystem)
        {
            if (gameSystem != sourceGameSystem)
            {
                return;
            }

            isRegistered = false;
            DispatchModuleUnregistered();
            gameSystem = null;
            NotifyStateChanged();
        }

        /// <summary>
        /// 回合系统入口。施工由核心处理；运营能力按 ModuleSet 顺序执行自动回合模块。
        /// </summary>
        public bool ProcessTurn()
        {
            if (!HasDefinition)
            {
                throw new InvalidOperationException($"Building '{name}' has no valid BuildingDefinition data.");
            }

            if (!initialized)
            {
                throw new InvalidOperationException($"Building '{name}' has not been initialized.");
            }

            if (IsUnderConstruction)
            {
                var constructionSucceeded = ProcessConstructionTurn();
                NotifyStateChanged();
                return constructionSucceeded;
            }

            var succeeded = ProcessAutomaticTurnModules();
            NotifyStateChanged();
            return succeeded;
        }

        private bool ProcessAutomaticTurnModules()
        {
            if (buildingModules == null)
            {
                return true;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                var module = buildingModules[i];
                if (module != null
                    && module.IsEnabled
                    && module is IBuildingAutomaticTurnModule automatic
                    && !automatic.ProcessAutomaticTurn(this))
                {
                    return false;
                }
            }

            return true;
        }

        private void DispatchModuleInitialized()
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModuleInitialized module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingInitialized(this);
                }
            }
        }

        private void DispatchModuleRegistered()
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModuleRegistered module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingRegistered(this);
                }
            }
        }

        private void DispatchModulePlaced()
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModulePlaced module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingPlaced(this);
                }
            }
        }

        private void DispatchModuleConstructionStarted()
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModuleConstructionStarted module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingConstructionStarted(this);
                }
            }
        }

        private void DispatchModuleConstructionCompleted()
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModuleConstructionCompleted module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingConstructionCompleted(this);
                }
            }
        }

        private void DispatchModuleLevelApplied(int previousLevel, int currentLevel)
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModuleLevelApplied module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingLevelApplied(this, previousLevel, currentLevel);
                }
            }
        }

        private void DispatchModuleUnregistered()
        {
            for (var i = BuildingModules.Count - 1; i >= 0; i--)
            {
                if (BuildingModules[i] is IBuildingModuleUnregistered module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingUnregistered(this);
                }
            }
        }

        private void DispatchModuleDemolished()
        {
            for (var i = BuildingModules.Count - 1; i >= 0; i--)
            {
                if (BuildingModules[i] is IBuildingModuleDemolished module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingDemolished(this);
                }
            }
        }

        private void DispatchModuleClicked()
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModuleClicked module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingClicked(this);
                }
            }
        }

        private void DispatchModuleDoubleClicked()
        {
            for (var i = 0; i < BuildingModules.Count; i++)
            {
                if (BuildingModules[i] is IBuildingModuleDoubleClicked module && BuildingModules[i].IsEnabled)
                {
                    module.OnBuildingDoubleClicked(this);
                }
            }
        }

        private bool ProcessConstructionTurn()
        {
            lastConstructionFailureStatusId = string.Empty;
            lastConstructionFailureStatusText = string.Empty;

            var construction = familyDefinition?.Construction;
            if (construction == null)
            {
                SetConstructionFailure(
                    BuildingRuntimeStatusCatalog.BS_消耗失败,
                    "施工配置缺失");
                return false;
            }

            var turnIndex = ConstructionProgress;
            var costs = construction.GetCosts(turnIndex);
            var rewards = construction.GetRewards(turnIndex);
            var hasCosts = HasAnyValidCost(costs);
            var hasRewards = HasAnyValidCost(rewards);
            ResourceProviderSelection providerSelection = default;
            if (hasCosts)
            {
                if (!BuildingResourceProviderSystem.TrySelectProvider(this, out providerSelection))
                {
                    SetConstructionFailure(
                        BuildingRuntimeStatusCatalog.BS_无法连接资源点,
                        "无法连接资源提供点");
                    return false;
                }

            }

            var inventory = GameSystem?.Services.Inventory;
            if ((hasCosts || hasRewards) && inventory == null)
            {
                SetConstructionFailure(
                    BuildingRuntimeStatusCatalog.BS_库存缺失,
                    "库存服务缺失");
                return false;
            }

            if ((hasCosts || hasRewards)
                && !inventory.TryExchangeItems(ToItemAmounts(costs), ToItemAmounts(rewards)))
            {
                SetConstructionFailure(
                    BuildingRuntimeStatusCatalog.BS_消耗失败,
                    hasCosts && !inventory.CanAffordBuildingCosts(costs)
                        ? "施工材料不足"
                        : "施工产出无法存入库存");
                return false;
            }

            if (hasCosts)
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    var cost = costs[i];
                    if (!cost.IsValid)
                    {
                        continue;
                    }

                    BuildingResourceProviderSystem.RecordProvidedResource(
                        providerSelection,
                        this,
                        new BuildingResourceChange(cost.ItemId, cost.Amount));
                }
            }

            lastConstructionRewards = ToResourceChanges(rewards);

            runtimeIdentity.AdvanceConstruction();
            if (ConstructionProgress >= construction.RequiredTurns)
            {
                CompleteConstruction();
            }
            else
            {
                presentationController?.RefreshForEntry(BuildingViewEntryReason.ConstructionAdvanced);
            }

            return true;
        }

        private void CompleteConstruction()
        {
            var previousStage = runtimeIdentity.CompleteConstruction();
            familyDefinition?.ApplyLevel(this, 1);
            DispatchModuleLevelApplied(0, 1);
            if (HasPlacement)
            {
                DispatchModulePlaced();
            }

            DispatchModuleConstructionCompleted();
            var change = new BuildingStageChangedEvent(
                this,
                previousStage,
                BuildingLifecycleStage.Operational);
            StageChanged?.Invoke(change);
            presentationController?.RefreshForEntry(BuildingViewEntryReason.ConstructionCompleted);
        }

        private void SetConstructionFailure(string statusId, string statusText)
        {
            lastConstructionFailureStatusId = string.IsNullOrWhiteSpace(statusId)
                ? BuildingRuntimeStatusCatalog.BS_消耗失败
                : statusId.Trim();
            lastConstructionFailureStatusText = string.IsNullOrWhiteSpace(statusText)
                ? lastConstructionFailureStatusId
                : statusText.Trim();
        }

        private static bool HasAnyValidCost(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null)
            {
                return false;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static ItemAmount[] ToItemAmounts(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return Array.Empty<ItemAmount>();
            }

            var result = new List<ItemAmount>(costs.Count);
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (cost.IsValid)
                {
                    result.Add(new ItemAmount(cost.ItemDefinition, cost.Amount));
                }
            }

            return result.ToArray();
        }

        private static BuildingResourceChange[] ToResourceChanges(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return Array.Empty<BuildingResourceChange>();
            }

            var result = new List<BuildingResourceChange>(costs.Count);
            for (var i = 0; i < costs.Count; i++)
            {
                var change = new BuildingResourceChange(costs[i].ItemId, costs[i].Amount);
                if (change.IsValid)
                {
                    result.Add(change);
                }
            }

            return result.ToArray();
        }

        public BuildingDataBase CaptureSaveData()
        {
            var data = HasCommonSaveData() ? new CommonBuildingData() : null;

            if (data != null)
            {
                CaptureCommonSaveData(data);
            }

            return data;
        }

        public void RestoreSaveData(BuildingDataBase data)
        {
            RestoreCommonSaveData(data);
            NotifyStateChanged();
        }

        private bool HasCommonSaveData()
        {
            return familyDefinition != null || HasSerializableModuleState();
        }

        private void CaptureCommonSaveData(BuildingDataBase data)
        {
            if (data == null)
            {
                return;
            }

            data.ModuleStates = CaptureModuleStates();
        }

        private void RestoreCommonSaveData(BuildingDataBase data)
        {
            if (data == null)
            {
                return;
            }

            RestoreModuleStates(data.ModuleStates);
        }

        private bool HasSerializableModuleState()
        {
            if (buildingModules == null)
            {
                return false;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                if (buildingModules[i] is IBuildingModuleStateSerializer && buildingModules[i].IsEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        private List<BuildingModuleStateEntry> CaptureModuleStates()
        {
            if (buildingModules == null)
            {
                return null;
            }

            List<BuildingModuleStateEntry> states = null;
            for (var i = 0; i < buildingModules.Count; i++)
            {
                var module = buildingModules[i];
                if (module == null
                    || !module.IsEnabled
                    || module is not IBuildingModuleStateSerializer serializer)
                {
                    continue;
                }

                string json;
                try
                {
                    if (!serializer.TryCaptureState(out json) || string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"采集建筑模块状态失败：{GetModuleStateId(module)}\n{e.Message}", this);
                    continue;
                }

                states ??= new List<BuildingModuleStateEntry>();
                states.Add(new BuildingModuleStateEntry
                {
                    ModuleId = GetModuleStateId(module),
                    Json = json
                });
            }

            return states;
        }

        private void RestoreModuleStates(IReadOnlyList<BuildingModuleStateEntry> moduleStates)
        {
            if (moduleStates == null || moduleStates.Count == 0 || buildingModules == null)
            {
                return;
            }

            for (var i = 0; i < moduleStates.Count; i++)
            {
                var entry = moduleStates[i];
                if (entry == null)
                {
                    continue;
                }

                entry.Normalize();
                if (!entry.IsValid || !TryGetModuleStateSerializer(entry, out var serializer))
                {
                    continue;
                }

                try
                {
                    serializer.RestoreState(entry.Json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"恢复建筑模块状态失败：{entry.ModuleId}\n{e.Message}", this);
                }
            }
        }

        private bool TryGetModuleStateSerializer(
            BuildingModuleStateEntry entry,
            out IBuildingModuleStateSerializer serializer)
        {
            serializer = null;
            if (entry == null || buildingModules == null)
            {
                return false;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                if (TryGetModuleStateSerializer(buildingModules[i], entry.ModuleId, out serializer))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetModuleStateSerializer(
            BuildingModuleBase module,
            string moduleId,
            out IBuildingModuleStateSerializer serializer)
        {
            serializer = null;
            if (module == null
                || !module.IsEnabled
                || !string.Equals(GetModuleStateId(module), moduleId, StringComparison.Ordinal))
            {
                return false;
            }

            serializer = module as IBuildingModuleStateSerializer;
            return serializer != null;
        }

        private static string GetModuleStateId(BuildingModuleBase module)
        {
            return module == null ? string.Empty : module.ModuleId;
        }

        [Serializable]
        [BuildingDataTypeId("building.common")]
        private sealed class CommonBuildingData : BuildingDataBase
        {
        }

        public void Demolish()
        {
            var buildingService = GameSystem == null ? null : GameSystem.Buildings;
            if (buildingService != null)
            {
                buildingService.Demolish(this);
                return;
            }

            RunDemolition();
        }

        internal void RunDemolition()
        {
            if (isDemolishing)
            {
                return;
            }

            isDemolishing = true;
            DispatchModuleDemolished();
            Destroy(gameObject);
        }

        /// <summary>
        /// 建筑销毁时执行架构清理。游戏拆除行为由模块生命周期处理。
        /// </summary>
        private void OnDestroy()
        {
            StopClickScaleFeedback();
            ClearPlacement();

            if (runtimeModuleSet != null)
            {
                Destroy(runtimeModuleSet);
                runtimeModuleSet = null;
            }

            if (gameSystem != null)
            {
                gameSystem.UnregisterBuilding(this);
            }
        }
        #endregion

        #region Click
        internal void DispatchPointerClick(bool isDoubleClick)
        {
            Clicked?.Invoke(this);
            PlayClickScaleFeedback();
            PlayPointerClickSound(isDoubleClick);
            DispatchModuleClicked();

            if (!isDoubleClick)
            {
                return;
            }

            DoubleClicked?.Invoke(this);
            DispatchModuleDoubleClicked();
        }

        private void PlayPointerClickSound(bool isDoubleClick)
        {
            AudioClip clip = isDoubleClick && doubleClickSound != null ? doubleClickSound : clickSound;
            if (clip == null)
            {
                return;
            }

            AudioPlayer.Instance.PlaySfx(clip);
        }
        #endregion
        //--------------------------------坐标读取------------------------------------------
        #region 位置坐标
        public bool TryGetGridPosition(out GridPosition position)
        {
            position = gridPosition;
            return hasGridPosition;
        }

        public bool TryGetFootprint(out GridFootprint footprint)
        {
            footprint = Footprint;
            return hasGridPosition;
        }
        #endregion


        internal void NotifyStateChanged()
        {
            StateChanged?.Invoke(this);
        }

        internal void SendBuildingEvent(string eventTypeId, string message)
        {
            GameSystem?.Events?.AddMessage(GameEventMessage.ForBuildingEvent(
                eventTypeId,
                this,
                message,
                GetEventTurnNumber()));
        }

        private int GetEventTurnNumber()
        {
            if (GameSystem == null)
            {
                return 0;
            }

            return GameSystem.IsAdvancingTurn ? GameSystem.CurrentTurn + 1 : GameSystem.CurrentTurn;
        }

        private static bool ContainsConnectionType(
            IReadOnlyList<string> connectionTypeIds,
            string expected)
        {
            if (connectionTypeIds == null)
            {
                return false;
            }

            for (var i = 0; i < connectionTypeIds.Count; i++)
            {
                if (string.Equals(
                        BuildingConnectionTypes.Normalize(connectionTypeIds[i]),
                        expected,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetModule<TModule>(out TModule module)
            where TModule : BuildingModuleBase
        {
            module = null;
            if (buildingModules == null)
            {
                return false;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                if (buildingModules[i] is TModule candidate && candidate.IsEnabled)
                {
                    module = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从建筑宿主或其启用模块中解析一项对外能力。外部系统不得依赖具体建筑派生类型。
        /// </summary>
        public bool TryGetCapability<TCapability>(out TCapability capability)
            where TCapability : class
        {
            if (this is TCapability directCapability)
            {
                capability = directCapability;
                return true;
            }

            if (buildingModules != null)
            {
                for (var i = 0; i < buildingModules.Count; i++)
                {
                    if (buildingModules[i] is TCapability moduleCapability
                        && buildingModules[i].IsEnabled)
                    {
                        capability = moduleCapability;
                        return true;
                    }
                }
            }

            capability = null;
            return false;
        }

        public List<TCapability> GetCapabilities<TCapability>(List<TCapability> results = null)
            where TCapability : class
        {
            results ??= new List<TCapability>();
            if (this is TCapability directCapability)
            {
                results.Add(directCapability);
            }

            if (buildingModules == null)
            {
                return results;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                if (buildingModules[i] is TCapability moduleCapability
                    && buildingModules[i].IsEnabled)
                {
                    results.Add(moduleCapability);
                }
            }

            return results;
        }

        public TModule GetRequiredModule<TModule>()
            where TModule : BuildingModuleBase
        {
            if (TryGetModule<TModule>(out var module))
            {
                return module;
            }

            throw new InvalidOperationException(
                $"Building family '{FamilyId}' requires module '{typeof(TModule).Name}', "
                + "but it is not declared in BuildingModuleSetDefinition.");
        }

        public List<TModule> GetModules<TModule>(List<TModule> results = null)
            where TModule : BuildingModuleBase
        {
            results ??= new List<TModule>();
            if (buildingModules == null)
            {
                return results;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                if (buildingModules[i] is TModule candidate && candidate.IsEnabled)
                {
                    results.Add(candidate);
                }
            }

            return results;
        }

        private void AppendBuildingModuleFunctionBlockEntries(ref List<BuildingFunctionBlockEntry> entries)
        {
            if (buildingModules == null)
            {
                return;
            }

            for (var i = 0; i < buildingModules.Count; i++)
            {
                var module = buildingModules[i];
                if (module == null || !module.IsEnabled)
                {
                    continue;
                }

                module.AppendFunctionBlockEntries(this, ref entries);
            }
        }

        private void ResolveView()
        {
            if (presentationController != null && presentationController.ViewAdapter != null)
            {
                view = presentationController.ViewAdapter;
                return;
            }

            if (view != null)
            {
                return;
            }

            view = GetComponentInChildren<BuildingView>(true);
            if (view == null)
            {
                view = GetComponentInParent<BuildingView>(true);
            }
        }

        private void EnsureDefinition()
        {
            if (familyDefinition != null)
            {
                familyDefinition.Definition?.Normalize();
            }
        }

        private void EnsureRuntimeIdentity()
        {
            runtimeIdentity ??= new BuildingRuntimeIdentity();
            runtimeIdentity.EnsureInitialized();
        }

        private void ResolvePresentationController()
        {
            if (presentationController == null)
            {
                presentationController = GetComponent<BuildingPresentationController>();
            }
        }

        private void CreateRuntimeModulesFromFamily()
        {
            if (familyDefinition?.ModuleSet == null)
            {
                return;
            }

            if (runtimeModuleSet != null)
            {
                Destroy(runtimeModuleSet);
            }

            runtimeModuleSet = familyDefinition.ModuleSet.CreateRuntimeClone();
            buildingModules = new List<BuildingModuleBase>(runtimeModuleSet.BuildingModules.Count);
            for (var i = 0; i < runtimeModuleSet.BuildingModules.Count; i++)
            {
                buildingModules.Add(runtimeModuleSet.BuildingModules[i]);
            }
        }

        private void ApplyCurrentLevelConfiguration()
        {
            if (familyDefinition == null || !IsOperational)
            {
                return;
            }

            familyDefinition.ApplyLevel(this, CurrentLevel);
        }

        public void AssignFamilyDefinition(BuildingFamilyDefinition family)
        {
            familyDefinition = family;
            EnsureDefinition();
        }

        private void NormalizeClickFeedback()
        {
            doubleClickInterval = Mathf.Max(0.05f, doubleClickInterval);
            clickScaleMultiplier = Mathf.Max(1f, clickScaleMultiplier);
            clickScaleDuration = Mathf.Max(0f, clickScaleDuration);
        }

        private void NormalizeBuildingActionPower()
        {
            buildingActionPower = Mathf.Max(0, buildingActionPower);
        }

        private void NormalizeBuildingModules()
        {
            buildingModules ??= new List<BuildingModuleBase>();

            for (var i = buildingModules.Count - 1; i >= 0; i--)
            {
                var module = buildingModules[i];
                if (module == null)
                {
                    buildingModules.RemoveAt(i);
                    continue;
                }

                module.Normalize();
            }
        }

        private void WarnIfMissingClickCollider()
        {
            if (BuildingPointerHitUtility.HasEnabledCollider(gameObject))
            {
                return;
            }

            Debug.LogWarning(
                $"建筑 '{name}' 缺少启用的 Collider/Collider2D，统一建筑点击链路无法命中它。",
                this);
        }

        private void PlayClickScaleFeedback()
        {
            if (!playClickScaleFeedback || clickScaleDuration <= 0f || Mathf.Approximately(clickScaleMultiplier, 1f))
            {
                return;
            }

            Transform target = View == null ? transform : View.transform;
            if (target == null)
            {
                return;
            }

            StopClickScaleFeedback();
            clickScaleTarget = target;
            clickScaleOriginalScale = target.localScale;
            clickScaleCoroutine = StartCoroutine(ClickScaleRoutine(target, clickScaleOriginalScale));
        }

        private IEnumerator ClickScaleRoutine(Transform target, Vector3 originalScale)
        {
            float halfDuration = Mathf.Max(0.01f, clickScaleDuration * 0.5f);
            Vector3 expandedScale = originalScale * clickScaleMultiplier;

            yield return ScaleOverTime(target, originalScale, expandedScale, halfDuration);
            yield return ScaleOverTime(target, expandedScale, originalScale, halfDuration);

            if (target != null)
            {
                target.localScale = originalScale;
            }

            clickScaleCoroutine = null;
            clickScaleTarget = null;
        }

        private static IEnumerator ScaleOverTime(
            Transform target,
            Vector3 fromScale,
            Vector3 toScale,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = toScale;
            }
        }

        private void StopClickScaleFeedback()
        {
            if (clickScaleCoroutine != null)
            {
                StopCoroutine(clickScaleCoroutine);
                clickScaleCoroutine = null;
            }

            if (clickScaleTarget != null)
            {
                clickScaleTarget.localScale = clickScaleOriginalScale;
                clickScaleTarget = null;
            }
        }

        /// <summary>
        /// 返回建筑当前需要显示的运行状态。空列表表示 UI 可视为正常。
        /// </summary>
        public IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
        {
            List<BuildingRuntimeStatus> statuses = null;
            if (IsOperational && buildingModules != null)
            {
                for (var i = 0; i < buildingModules.Count; i++)
                {
                    var module = buildingModules[i];
                    if (module != null && module.IsEnabled)
                    {
                        module.AppendRuntimeStatuses(this, ref statuses);
                    }
                }
            }

            AppendCommonRuntimeStatuses(ref statuses);
            return statuses ?? EmptyRuntimeStatuses;
        }

        private void AppendCommonRuntimeStatuses(ref List<BuildingRuntimeStatus> statuses)
        {
            if (IsUnderConstruction)
            {
                var requiredTurns = familyDefinition?.Construction?.RequiredTurns ?? 1;
                AppendRuntimeStatus(
                    ref statuses,
                    new BuildingRuntimeStatus(
                        "construction",
                        "施工中",
                        ConstructionProgress,
                        requiredTurns));
            }

            if (!string.IsNullOrWhiteSpace(lastConstructionFailureStatusId))
            {
                AppendRuntimeStatus(
                    ref statuses,
                    new BuildingRuntimeStatus(
                        lastConstructionFailureStatusId,
                        lastConstructionFailureStatusText));
            }

            AppendRuntimeStatus(ref statuses, CreateRoadBlockedStatus());
        }

        private static void AppendRuntimeStatus(ref List<BuildingRuntimeStatus> statuses, BuildingRuntimeStatus status)
        {
            if (!status.IsValid)
            {
                return;
            }

            statuses ??= new List<BuildingRuntimeStatus>();
            statuses.Add(status);
        }

        private BuildingRuntimeStatus CreateRoadBlockedStatus()
        {
            if (!HasPlacement || gridMap == null || !HasDefinition || HasPathFromAroundBuildingToMapBoundary())
            {
                return default;
            }

            return new BuildingRuntimeStatus(BuildingRuntimeStatusCatalog.BS_道路不通, "道路不通");
        }

        private bool HasPathFromAroundBuildingToMapBoundary()
        {
            var bounds = gridMap.BaseCellBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0)
            {
                return true;
            }

            var footprintCells = new HashSet<GridPosition>();
            foreach (var position in Footprint.Positions())
            {
                footprintCells.Add(position);
            }

            if (footprintCells.Count <= 0)
            {
                return true;
            }

            var startCells = new HashSet<GridPosition>();
            foreach (var position in footprintCells)
            {
                AddBoundaryConnectionCandidate(startCells, footprintCells, position.X + 1, position.Y);
                AddBoundaryConnectionCandidate(startCells, footprintCells, position.X - 1, position.Y);
                AddBoundaryConnectionCandidate(startCells, footprintCells, position.X, position.Y + 1);
                AddBoundaryConnectionCandidate(startCells, footprintCells, position.X, position.Y - 1);
            }

            foreach (var start in startCells)
            {
                if (CanReachMapBoundary(start, footprintCells, bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddBoundaryConnectionCandidate(
            HashSet<GridPosition> startCells,
            HashSet<GridPosition> footprintCells,
            int x,
            int y)
        {
            var candidate = new GridPosition(x, y);
            if (!footprintCells.Contains(candidate))
            {
                startCells.Add(candidate);
            }
        }

        private bool CanReachMapBoundary(
            GridPosition start,
            HashSet<GridPosition> footprintCells,
            BoundsInt bounds)
        {
            if (!IsBoundaryConnectionPassable(start, footprintCells))
            {
                return false;
            }

            var open = new Queue<GridPosition>();
            var visited = new HashSet<GridPosition> { start };
            open.Enqueue(start);

            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (IsMapBoundaryCell(current, bounds))
                {
                    return true;
                }

                TryEnqueueBoundaryConnectionNeighbor(open, visited, footprintCells, current.X + 1, current.Y);
                TryEnqueueBoundaryConnectionNeighbor(open, visited, footprintCells, current.X - 1, current.Y);
                TryEnqueueBoundaryConnectionNeighbor(open, visited, footprintCells, current.X, current.Y + 1);
                TryEnqueueBoundaryConnectionNeighbor(open, visited, footprintCells, current.X, current.Y - 1);
            }

            return false;
        }

        private void TryEnqueueBoundaryConnectionNeighbor(
            Queue<GridPosition> open,
            HashSet<GridPosition> visited,
            HashSet<GridPosition> footprintCells,
            int x,
            int y)
        {
            var neighbor = new GridPosition(x, y);
            if (visited.Contains(neighbor) || !IsBoundaryConnectionPassable(neighbor, footprintCells))
            {
                return;
            }

            visited.Add(neighbor);
            open.Enqueue(neighbor);
        }

        private bool IsBoundaryConnectionPassable(GridPosition position, HashSet<GridPosition> footprintCells)
        {
            if (gridMap == null || footprintCells.Contains(position) || !gridMap.HasBaseTileAt(position))
            {
                return false;
            }

            return gridMap.CanTraverse(position, gridOccupancyId);
        }

        private static bool IsMapBoundaryCell(GridPosition position, BoundsInt bounds)
        {
            return position.X <= bounds.xMin
                   || position.X >= bounds.xMax - 1
                   || position.Y <= bounds.yMin
                   || position.Y >= bounds.yMax - 1;
        }

        #region 信息
        /// <summary>
        /// 建筑在列表、选中栏、底栏中使用的一行基础摘要。
        /// </summary>
        public string GetOverviewInfo()
        {
            if (IsUnderConstruction)
            {
                var requiredTurns = familyDefinition?.Construction?.RequiredTurns ?? 1;
                return $"施工 {ConstructionProgress}/{requiredTurns}";
            }

            var fragments = new List<string> { $"等级 {CurrentLevel}" };
            if (buildingModules != null)
            {
                for (var i = 0; i < buildingModules.Count; i++)
                {
                    var module = buildingModules[i];
                    if (module == null || !module.IsEnabled)
                    {
                        continue;
                    }

                    var fragment = module.GetOverviewFragment(this);
                    if (!string.IsNullOrWhiteSpace(fragment))
                    {
                        fragments.Add(fragment.Trim());
                    }
                }
            }

            return string.Join("，", fragments);
        }

        public IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
        {
            List<BuildingFunctionBlockEntry> entries = null;
            AppendBuildingModuleFunctionBlockEntries(ref entries);
            return entries == null ? EmptyFunctionBlockEntries : entries;
        }
        #endregion

    }
}
