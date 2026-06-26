using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [CreateAssetMenu(menuName = "Landsong/Building/Building Catalog", fileName = "BuildingCatalog")]
    public sealed class BuildingCatalog : ScriptableObject
    {
        [SerializeField] private BuildingDefinition[] definitions = Array.Empty<BuildingDefinition>();

        private Dictionary<string, BuildingDefinition> definitionsById;

        public IReadOnlyList<BuildingDefinition> Definitions => definitions ?? Array.Empty<BuildingDefinition>();

        private void OnEnable()
        {
            RebuildIndex();
        }

        private void OnValidate()
        {
            if (definitions == null)
            {
                definitions = Array.Empty<BuildingDefinition>();
            }

            RebuildIndex();
        }

        public bool TryGetDefinition(string buildingId, out BuildingDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                definition = null;
                return false;
            }

            EnsureIndex();
            return definitionsById.TryGetValue(buildingId, out definition);
        }

        public BuildingDefinition GetDefinition(string buildingId)
        {
            if (TryGetDefinition(buildingId, out var definition))
            {
                return definition;
            }

            throw new KeyNotFoundException($"Building definition '{buildingId}' was not found in catalog '{name}'.");
        }

        public bool Contains(string buildingId)
        {
            return TryGetDefinition(buildingId, out _);
        }

        public void RebuildIndex()
        {
            definitionsById = new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);

            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.BuildingId))
                {
                    continue;
                }

                if (definitionsById.ContainsKey(definition.BuildingId))
                {
                    Debug.LogWarning($"Duplicate building definition id '{definition.BuildingId}' in catalog '{name}'. The first entry will be used.", this);
                    continue;
                }

                definitionsById.Add(definition.BuildingId, definition);
            }
        }

        private void EnsureIndex()
        {
            if (definitionsById == null)
            {
                RebuildIndex();
            }
        }
    }
}
