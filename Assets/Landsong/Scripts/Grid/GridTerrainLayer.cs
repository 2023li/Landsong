using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.GridSystem
{
    [Serializable]
    public sealed class GridTerrainLayer
    {
        [SerializeField, LabelText("地形 Key")] private string key;
        [SerializeField, LabelText("Tilemap")] private Tilemap tilemap;
        [SerializeField, LabelText("替换默认地形")] private bool replaceDefaultTerrainKey = true;

        public string Key => GridTerrainKeys.Normalize(key);
        public Tilemap Tilemap => tilemap;
        public bool ReplaceDefaultTerrainKey => replaceDefaultTerrainKey;
        public bool IsValid => tilemap != null && !string.IsNullOrWhiteSpace(Key);

        public void Configure(string terrainKey, Tilemap terrainTilemap, bool replaceDefault)
        {
            key = GridTerrainKeys.Normalize(terrainKey);
            tilemap = terrainTilemap;
            replaceDefaultTerrainKey = replaceDefault;
        }
    }
}
