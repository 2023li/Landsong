using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ExpeditionSystem
{
    [CreateAssetMenu(menuName = "Landsong/Expedition/Destination Catalog", fileName = "ExpeditionDestinationCatalog")]
    public sealed class ExpeditionDestinationCatalog : ScriptableObject
    {
        [SerializeField, LabelText("远征目的地")]
        private ExpeditionDestinationDefinition[] destinations = Array.Empty<ExpeditionDestinationDefinition>();

        private Dictionary<string, ExpeditionDestinationDefinition> destinationsById;

        public IReadOnlyList<ExpeditionDestinationDefinition> Destinations =>
            destinations ?? Array.Empty<ExpeditionDestinationDefinition>();

        private void OnEnable()
        {
            Normalize();
            RebuildIndex();
        }

        private void OnValidate()
        {
            Normalize();
            RebuildIndex();
        }

        public bool TryGetDestination(string destinationId, out ExpeditionDestinationDefinition destination)
        {
            destinationId = ExpeditionDestinationDefinition.NormalizeId(destinationId);
            if (string.IsNullOrWhiteSpace(destinationId))
            {
                destination = null;
                return false;
            }

            EnsureIndex();
            return destinationsById.TryGetValue(destinationId, out destination);
        }

        public void RebuildIndex()
        {
            destinationsById = new Dictionary<string, ExpeditionDestinationDefinition>(StringComparer.Ordinal);
            var source = Destinations;
            for (var i = 0; i < source.Count; i++)
            {
                var destination = source[i];
                if (destination == null || !destination.IsValid)
                {
                    continue;
                }

                if (destinationsById.ContainsKey(destination.DestinationId))
                {
                    Debug.LogWarning($"远征目的地 ID 重复，已忽略后续配置：{destination.DestinationId}", this);
                    continue;
                }

                destinationsById.Add(destination.DestinationId, destination);
            }
        }

        private void EnsureIndex()
        {
            if (destinationsById == null)
            {
                RebuildIndex();
            }
        }

        private void Normalize()
        {
            destinations ??= Array.Empty<ExpeditionDestinationDefinition>();
            for (var i = 0; i < destinations.Length; i++)
            {
                destinations[i]?.Normalize();
            }
        }
    }
}
