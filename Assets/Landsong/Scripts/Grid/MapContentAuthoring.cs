using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.GridSystem
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Landsong/Map/Map Content Authoring")]
    public sealed class MapContentAuthoring : MonoBehaviour
    {
        [SerializeField, LabelText("地图 Grid")]
        private UnityEngine.Grid unityGrid;

        [SerializeField, LabelText("Base Tilemap")]
        private Tilemap baseTilemap;

        [SerializeField, LabelText("地形 Tilemap 层")]
        private List<GridTerrainLayer> terrainLayers = new List<GridTerrainLayer>();

        public UnityEngine.Grid UnityGrid => unityGrid;
        public Tilemap BaseTilemap => baseTilemap;
        public IReadOnlyList<GridTerrainLayer> TerrainLayers => terrainLayers == null
            ? (IReadOnlyList<GridTerrainLayer>)Array.Empty<GridTerrainLayer>()
            : terrainLayers;
        public bool IsValid => unityGrid != null && baseTilemap != null && baseTilemap.GetUsedTilesCount() > 0;

        public InitialBuildingMarker[] GetInitialBuildingMarkers()
        {
            return GetComponentsInChildren<InitialBuildingMarker>(true);
        }

        private void Reset()
        {
            unityGrid = GetComponentInChildren<UnityEngine.Grid>(true);
            if (unityGrid != null && baseTilemap == null)
            {
                var tilemaps = unityGrid.GetComponentsInChildren<Tilemap>(true);
                for (var i = 0; i < tilemaps.Length; i++)
                {
                    if (tilemaps[i] != null
                        && tilemaps[i].name.IndexOf("Base", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        baseTilemap = tilemaps[i];
                        break;
                    }
                }
            }
        }

        private void OnValidate()
        {
            terrainLayers ??= new List<GridTerrainLayer>();
        }
    }
}
