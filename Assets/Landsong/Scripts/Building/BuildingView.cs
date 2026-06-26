using System;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        [SerializeField] private BuildingBehaviour building;
        [SerializeField] private BuildingVisualBinding[] visualBindings = Array.Empty<BuildingVisualBinding>();
        [SerializeField] private bool refreshOnEnable = true;

        private void Reset()
        {
            building = GetComponent<BuildingBehaviour>();
        }

        private void OnValidate()
        {
            if (building == null)
            {
                building = GetComponent<BuildingBehaviour>();
            }

            visualBindings ??= Array.Empty<BuildingVisualBinding>();
        }

        private void OnEnable()
        {
            Subscribe();

            if (refreshOnEnable)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        [ContextMenu("Refresh Building View")]
        public void Refresh()
        {
            var activeRoot = GetActiveVisualRoot();

            foreach (var binding in visualBindings)
            {
                if (binding.VisualRoot == null)
                {
                    continue;
                }

                binding.VisualRoot.SetActive(binding.VisualRoot == activeRoot);
            }
        }

        private GameObject GetActiveVisualRoot()
        {
            if (building == null || building.Definition == null || visualBindings == null)
            {
                return null;
            }

            foreach (var binding in visualBindings)
            {
                if (binding.Definition == building.Definition)
                {
                    return binding.VisualRoot;
                }
            }

            return null;
        }

        private void Subscribe()
        {
            if (building == null)
            {
                return;
            }

            building.StateChanged += HandleBuildingStateChanged;
            building.DefinitionChanged += HandleBuildingDefinitionChanged;
        }

        private void Unsubscribe()
        {
            if (building == null)
            {
                return;
            }

            building.StateChanged -= HandleBuildingStateChanged;
            building.DefinitionChanged -= HandleBuildingDefinitionChanged;
        }

        private void HandleBuildingStateChanged(BuildingBehaviour changedBuilding)
        {
            Refresh();
        }

        private void HandleBuildingDefinitionChanged(
            BuildingBehaviour changedBuilding,
            BuildingDefinition previousDefinition,
            BuildingDefinition nextDefinition)
        {
            Refresh();
        }
    }

    [Serializable]
    public sealed class BuildingVisualBinding
    {
        [SerializeField] private BuildingDefinition definition;
        [SerializeField] private GameObject visualRoot;

        public BuildingDefinition Definition => definition;
        public GameObject VisualRoot => visualRoot;
    }
}
