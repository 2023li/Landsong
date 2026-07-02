using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.CameraSystem;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingStatusOverview : MonoBehaviour
    {
        private UIPanel_Game gamePanel;
        [SerializeField] private Button btn_关闭;

        [SerializeField] private Transform itemRoot;
        [SerializeField] private GamePanel_BuildingStatusOverviewItem itemPrefab;
        private Transform itemPoolRoot = null;
        [SerializeField] private bool showNormalBuildings = true;
        [SerializeField] private bool sortAbnormalBuildingsFirst = true;
        [SerializeField] private bool focusBuildingOnItemClick = true;
        private CameraController cameraController;

        private readonly List<GamePanel_BuildingStatusOverviewItem> activeItems = new List<GamePanel_BuildingStatusOverviewItem>();
        private readonly List<GamePanel_BuildingStatusOverviewItem> itemPool = new List<GamePanel_BuildingStatusOverviewItem>();
        private readonly HashSet<BuildingBase> subscribedBuildings = new HashSet<BuildingBase>();
        private Landsong.GameSystem gameSystem;
        private BuildingService buildings;
        private bool subscribedToBuildings;

        private void Reset()
        {
            itemRoot = transform;
         
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeBuildings();
            RefreshBuildingSubscriptions();
            Refresh();
        }

        private void Awake()
        {
            btn_关闭.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            UnsubscribeBuildings();
            UnsubscribeBuildingStates();
        }

        public void Refresh()
        {
            ReleaseActiveItems();

            if (itemRoot == null || itemPrefab == null || buildings == null)
            {
                return;
            }

            var visibleBuildings = CollectVisibleBuildings();
            for (var i = 0; i < visibleBuildings.Count; i++)
            {
                var building = visibleBuildings[i];
                var data = BuildingStatusUIFormatter.CreateDisplayData(building);
                var item = GetItemFromPool();
                item.Bind(i + 1, building, data, HandleItemClicked);
                activeItems.Add(item);
            }
        }

        private void ResolveReferences()
        {
            gamePanel = GetComponentInParent<UIPanel_Game>();
            gameSystem = Landsong.GameSystem.Instance;
            buildings = gameSystem == null ? null : gameSystem.Buildings;

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }

        }

        private List<BuildingBase> CollectVisibleBuildings()
        {
            var result = new List<BuildingBase>();
            var source = buildings.Buildings;
            for (var i = 0; i < source.Count; i++)
            {
                var building = source[i];
                if (!CanDisplayBuilding(building))
                {
                    continue;
                }

                var data = BuildingStatusUIFormatter.CreateDisplayData(building);
                if (!showNormalBuildings && !data.HasAbnormalStatus)
                {
                    continue;
                }

                result.Add(building);
            }

            if (sortAbnormalBuildingsFirst)
            {
                result.Sort(CompareBuildingsForDisplay);
            }

            return result;
        }

        private int CompareBuildingsForDisplay(BuildingBase left, BuildingBase right)
        {
            var leftData = BuildingStatusUIFormatter.CreateDisplayData(left);
            var rightData = BuildingStatusUIFormatter.CreateDisplayData(right);
            if (leftData.HasAbnormalStatus != rightData.HasAbnormalStatus)
            {
                return leftData.HasAbnormalStatus ? -1 : 1;
            }

            return string.Compare(leftData.BuildingName, rightData.BuildingName, StringComparison.Ordinal);
        }

        private static bool CanDisplayBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }

        private GamePanel_BuildingStatusOverviewItem GetItemFromPool()
        {
            GamePanel_BuildingStatusOverviewItem item;
            var lastIndex = itemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = itemPool[lastIndex];
                itemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(itemPrefab);
            }

            item.transform.SetParent(itemRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseActiveItems()
        {
            for (var i = 0; i < activeItems.Count; i++)
            {
                var item = activeItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                item.transform.SetParent(itemPoolRoot == null ? itemRoot : itemPoolRoot, false);
                itemPool.Add(item);
            }

            activeItems.Clear();
        }

        private void HandleItemClicked(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            if (focusBuildingOnItemClick)
            {
                if (cameraController == null)
                {
                    cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
                }

                cameraController?.FocusOnBuilding(building);
            }

        }

        private void SubscribeBuildings()
        {
            if (subscribedToBuildings || buildings == null)
            {
                return;
            }

            buildings.BuildingsChanged += HandleBuildingsChanged;
            subscribedToBuildings = true;
        }

        private void UnsubscribeBuildings()
        {
            if (!subscribedToBuildings || buildings == null)
            {
                subscribedToBuildings = false;
                return;
            }

            buildings.BuildingsChanged -= HandleBuildingsChanged;
            subscribedToBuildings = false;
        }

        private void RefreshBuildingSubscriptions()
        {
            UnsubscribeBuildingStates();
            if (buildings == null)
            {
                return;
            }

            var source = buildings.Buildings;
            for (var i = 0; i < source.Count; i++)
            {
                var building = source[i];
                if (building == null || !subscribedBuildings.Add(building))
                {
                    continue;
                }

                building.StateChanged += HandleBuildingStateChanged;
            }
        }

        private void UnsubscribeBuildingStates()
        {
            foreach (var building in subscribedBuildings)
            {
                if (building != null)
                {
                    building.StateChanged -= HandleBuildingStateChanged;
                }
            }

            subscribedBuildings.Clear();
        }

        private void HandleBuildingsChanged(BuildingService changedBuildings)
        {
            buildings = changedBuildings;
            RefreshBuildingSubscriptions();
            Refresh();
        }

        private void HandleBuildingStateChanged(BuildingBase changedBuilding)
        {
            Refresh();
        }

        internal void Show()
        {
          gameObject.SetActive(true);
        }
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
