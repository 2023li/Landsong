using UnityEngine;

namespace Landsong.BuildingSystem
{
    public static class BuildingPlacementPreviewFactory
    {
        public static GameObject Create(
            BuildingBase buildingPrefab,
            Transform parent,
            string ghostName,
            string styleId,
            out BuildingView view)
        {
            view = null;
            if (buildingPrefab == null)
            {
                return null;
            }

            var definition = buildingPrefab.Definition;
            var instance = Object.Instantiate(buildingPrefab.gameObject, parent);
            instance.name = string.IsNullOrWhiteSpace(ghostName) ? $"{buildingPrefab.name}_PlacementGhost" : ghostName;
            instance.SetActive(false);

            var ghostBuilding = instance.GetComponentInChildren<BuildingBase>(true);
            if (ghostBuilding != null)
            {
                ghostBuilding.RestoreRuntimeIdentity(
                    ghostBuilding.InstanceId,
                    BuildingLifecycleStage.Operational,
                    1,
                    styleId,
                    0);
            }
            DisablePreviewBuildingRuntime(instance);
            view = ghostBuilding == null ? instance.GetComponentInChildren<BuildingView>(true) : ghostBuilding.View;
            if (view == null)
            {
                Debug.LogWarning($"Building preview '{definition?.DisplayName ?? buildingPrefab.name}' has no BuildingView.", instance);
                return instance;
            }

            if (!view.SetPlacementPreview(true))
            {
                Debug.LogWarning(
                    $"Building preview '{definition?.DisplayName ?? buildingPrefab.name}' cannot play preview animation key '{view.PlacementPreviewAnimationKey}'.",
                    view);
            }

            return instance;
        }

        public static void Destroy(ref GameObject instance, ref BuildingView view)
        {
            if (instance == null)
            {
                view = null;
                return;
            }

            if (view != null)
            {
                view.SetPlacementPreview(false);
            }

            Object.Destroy(instance);
            instance = null;
            view = null;
        }

        private static void DisablePreviewBuildingRuntime(GameObject previewInstance)
        {
            if (previewInstance == null)
            {
                return;
            }

            var buildings = previewInstance.GetComponentsInChildren<BuildingBase>(true);
            for (var i = 0; i < buildings.Length; i++)
            {
                buildings[i].enabled = false;
            }
        }
    }
}
