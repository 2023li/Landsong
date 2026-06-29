using System;
using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 建筑 prefab 根节点上的运行时基类。
    /// BuildingDefinition 只负责静态配置；具体建筑等级通过继承本类实现生命周期。
    /// </summary>
    public abstract class BuildingBase : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("该建筑实例对应的静态定义。由放置系统或场景初始化流程写入，子类不要直接修改。")]
        [SerializeField] private BuildingDefinition definition;

        [Tooltip("是否已经拥有有效的格子坐标。未放置或已清除占地时为 false。")]
        [SerializeField] private bool hasGridPosition;

        [Tooltip("建筑占地原点格子坐标。多格建筑以左下角/原点格作为占地起点。")]
        [SerializeField] private GridPosition gridPosition;

        [Tooltip("格子系统中的唯一占用 ID，用来识别和清理该建筑占用的一组格子。不是人口或居民 ID。")]
        [SerializeField] private string gridOccupancyId;

        [Tooltip("两次点击间隔小于等于该值时，第二次点击视为双击。")]
        [SerializeField, Min(0.05f)] private float doubleClickInterval = 0.3f;

      
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

        private float lastClickTime = float.NegativeInfinity;
        private int lastClickFrame = -1;

        public event Action<BuildingBase> StateChanged;
        public event Action<BuildingBase> Clicked;
        public event Action<BuildingBase> DoubleClicked;

        // 当前建筑实例对应的静态定义。
        public BuildingDefinition Definition => definition;

        // 当前建筑接入的游戏系统；未注册时为 null。
        public Landsong.GameSystem GameSystem => gameSystem;
        public GridMapBehaviour GridMap => gridMap;

        // 是否已经完成 Initialize。
        public bool IsInitialized => initialized;

        // 是否已经注册到 GameSystem。
        public bool IsRegistered => isRegistered;
        public bool IsDemolishing => isDemolishing;

        // 是否已经持有有效 BuildingDefinition。
        public bool HasDefinition => definition != null;

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

        // 根据 Definition 的尺寸和当前原点计算出的占地范围。
        public GridFootprint Footprint => definition == null ? new GridFootprint(gridPosition, Vector2Int.one) : definition.CreateFootprint(gridPosition);



      

        //--------------------------------架构类API-----------------------------------------
        #region 架构类API
        /// <summary>
        /// Unity Start 阶段自动注册到 GameSystem。子类重写 Start 时必须调用 base.Start()。
        /// </summary>
        protected virtual void Start()
        {
            Landsong.GameSystem.Instance.RegisterBuilding(this);
        }

        /// <summary>
        /// 由 GameSystem 注册流程调用。Definition 从 prefab 上读取，GameSystem 从单例获取。
        /// </summary>
        internal void Initialize()
        {
            if (definition == null)
            {
                throw new InvalidOperationException($"Building '{name}' has no BuildingDefinition assigned.");
            }

            var previousGameSystem = gameSystem;
            var newGameSystem = Landsong.GameSystem.Instance;
            var gameSystemChanged = previousGameSystem != newGameSystem;
            gameSystem = newGameSystem;
            isRegistered = gameSystem != null;

            if (!initialized)
            {
                initialized = true;
                OnInitialized();
            }

            if (gameSystemChanged)
            {
                if (previousGameSystem != null)
                {
                    OnUnregistered();
                }

                if (gameSystem != null)
                {
                    OnRegistered();
                }
            }

            NotifyPlacedIfReady();
            NotifyStateChanged();
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
            OnPlaced();
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

            gameSystem = null;
            isRegistered = false;
            OnUnregistered();
            NotifyStateChanged();
        }

        /// <summary>
        /// 回合系统入口。施工、运营、产出、失败处理都由具体建筑等级的 OnTurn 实现。
        /// </summary>
        public bool ProcessTurn()
        {
            if (definition == null)
            {
                throw new InvalidOperationException($"Building '{name}' has no BuildingDefinition assigned.");
            }

            if (!initialized)
            {
                throw new InvalidOperationException($"Building '{name}' has not been initialized.");
            }

            bool succeeded = OnTurn();
            NotifyStateChanged();
            return succeeded;
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
            OnDemolished();
            Destroy(gameObject);
        }

        /// <summary>
        /// 建筑销毁时执行架构清理。子类不要重写 OnDestroy；游戏拆除行为重写 OnDemolished。
        /// </summary>
        protected void OnDestroy()
        {
            ClearPlacement();

            if (gameSystem != null)
            {
                gameSystem.UnregisterBuilding(this);
            }
        }
        #endregion

        #region Click
        public void OnPointerClick(PointerEventData eventData)
        {
            if (lastClickFrame == Time.frameCount)
            {
                return;
            }

            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            DispatchBuildingClick(eventData != null && eventData.clickCount > 1);
        }

        protected virtual void OnMouseUpAsButton()
        {
            if (lastClickFrame == Time.frameCount)
            {
                return;
            }

            DispatchBuildingClick(Time.unscaledTime - lastClickTime <= doubleClickInterval);
        }

        private void DispatchBuildingClick(bool isDoubleClick)
        {
            lastClickTime = Time.unscaledTime;
            lastClickFrame = Time.frameCount;

            Clicked?.Invoke(this);
            OnClicked();

            if (!isDoubleClick)
            {
                return;
            }

            DoubleClicked?.Invoke(this);
            OnDoubleClicked();
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


        //--------------------------------GamePlay-----------------------------------------
        #region 游戏内容字段
        [SerializeField, LabelText("自动修复")]
        public bool autoRepair = false;
        [SerializeField, LabelText("自动升级")]
        public bool autpUpgrade = true;
        #endregion

        protected void NotifyStateChanged()
        {
            StateChanged?.Invoke(this);
        }

        /// <summary>
        /// Definition 第一次写入后调用一次。适合初始化本建筑等级的运行时默认值。
        /// </summary>
        protected virtual void OnInitialized()
        {
        }

        /// <summary>
        /// 建筑被点击时调用。默认无行为；具体建筑可按需重写。
        /// </summary>
        protected virtual void OnClicked()
        {
           
        }

        /// <summary>
        /// 建筑被双击时调用。默认无行为；具体建筑可按需重写。
        /// </summary>
        protected virtual void OnDoubleClicked()
        {
        }

        /// <summary>
        /// 建筑通过游戏行为被拆除时调用。普通 Destroy 不会触发该钩子。
        /// </summary>
        protected virtual void OnDemolished()
        {
        }

        /// <summary>
        /// 玩家放置完成后调用。此时建筑已经拥有格子位置、Definition 和 GameSystem。
        /// </summary>
        protected abstract void OnPlaced();

        /// <summary>
        /// 建筑被 GameSystem 纳入运行时管理后调用。适合注册全局人口、buff、事件监听等。
        /// </summary>
        protected abstract void OnRegistered();

        /// <summary>
        /// 建筑每回合逻辑。施工、运营、产出、升级触发都写在这里。返回 false 表示本回合执行失败。
        /// </summary>
        protected abstract bool OnTurn();

        /// <summary>
        /// 建筑从 GameSystem 移除时调用。适合注销事件监听或清理全局占用。
        /// </summary>
        protected virtual void OnUnregistered()
        {
        }

      

        public virtual bool CanRepair()
        {
            return false;
        }

        public virtual bool TryRepair()
        {
            return false;
        }
    }
}
