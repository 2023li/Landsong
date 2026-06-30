using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Landsong.GridSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Landsong/Map/Map Catalog", fileName = "MapCatalog")]
public class MapDataCatalog : SingletonScriptableObject<MapDataCatalog>
{
    [Serializable]
    public class MapData
    {
        [FormerlySerializedAs("name")]
        [SerializeField, LabelText("地图名称")]
        private string displayName;

        [SerializeField, LabelText("地图图标")]
        private Sprite icon;

        [SerializeField, TextArea, LabelText("地图描述")]
        private string description;

        [SerializeField, LabelText("地图预制体")]
        private GridMapBehaviour map;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName.Trim();
                }

                return map == null ? "未命名地图" : map.name;
            }
        }

        public string Description => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        public Sprite Icon => icon;
        public GridMapBehaviour Map => map;
        public bool IsValid => map != null;

        public static MapData Create(string displayName, Sprite icon, string description, GridMapBehaviour map)
        {
            return new MapData
            {
                displayName = displayName,
                icon = icon,
                description = description,
                map = map
            };
        }
    }

    [SerializeField, LabelText("地图列表")]
    private MapData[] mapDatas = Array.Empty<MapData>();

    private Dictionary<string, MapData> mapDatasByName;

    public MapData[] MapDatas => mapDatas ?? Array.Empty<MapData>();
    public IReadOnlyList<MapData> Maps => MapDatas;
    public int Count => mapDatas == null ? 0 : mapDatas.Length;

    public static Task<MapDataCatalog> LoadAsync(string addressableKey)
    {
        return GetInstanceAsync(addressableKey);
    }

    private void OnEnable()
    {
        ValidateEntries();
    }

    private void OnValidate()
    {
        ValidateEntries();
    }

    public bool TryGetMapData(int index, out MapData mapData)
    {
        MapData[] maps = MapDatas;
        if (index < 0 || index >= maps.Length)
        {
            mapData = null;
            return false;
        }

        mapData = maps[index];
        return mapData != null && mapData.IsValid;
    }

    public bool TryGetMapData(string mapName, out MapData mapData)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            mapData = null;
            return false;
        }

        EnsureIndex();
        return mapDatasByName.TryGetValue(mapName.Trim(), out mapData);
    }

    public GridMapBehaviour GetMapPrefab(string mapName)
    {
        return TryGetMapData(mapName, out var mapData) ? mapData.Map : null;
    }

    public MapData GetFirstValidMapData()
    {
        foreach (MapData mapData in GetValidMapDatas())
        {
            return mapData;
        }

        return null;
    }

    public IEnumerable<MapData> GetValidMapDatas()
    {
        if (mapDatas == null)
        {
            yield break;
        }

        for (int i = 0; i < mapDatas.Length; i++)
        {
            MapData mapData = mapDatas[i];

            if (mapData != null && mapData.IsValid)
            {
                yield return mapData;
            }
        }
    }

    private void ValidateEntries()
    {
        if (mapDatas == null)
        {
            mapDatas = Array.Empty<MapData>();
        }

        RebuildIndex();
    }

    public void RebuildIndex()
    {
        mapDatasByName = new Dictionary<string, MapData>(StringComparer.Ordinal);

        foreach (var mapData in MapDatas)
        {
            if (mapData == null || string.IsNullOrWhiteSpace(mapData.DisplayName))
            {
                continue;
            }

            string mapName = mapData.DisplayName;
            if (mapDatasByName.ContainsKey(mapName))
            {
                Debug.LogWarning($"Duplicate map data name '{mapName}' in catalog '{name}'. The first entry will be used.", this);
                continue;
            }

            mapDatasByName.Add(mapName, mapData);
        }
    }

    private void EnsureIndex()
    {
        if (mapDatasByName == null)
        {
            RebuildIndex();
        }
    }

#if UNITY_EDITOR
    [SerializeField, FolderPath(RequireExistingPath = true), LabelText("地图预制体目录")]
    private string mapPrefabFolderPath = "Assets/Landsong/Objects/Prefabs/Map";

    [Button("从目录加载地图预制体")]
    private void LoadMapsFromFolder()
    {
        if (string.IsNullOrWhiteSpace(mapPrefabFolderPath))
        {
            Debug.LogWarning("Map prefab folder path is empty.", this);
            return;
        }

        if (!AssetDatabase.IsValidFolder(mapPrefabFolderPath))
        {
            Debug.LogWarning($"Map prefab folder path '{mapPrefabFolderPath}' is not a valid asset folder.", this);
            return;
        }

        var existingMapDatas = new Dictionary<GridMapBehaviour, MapData>();
        foreach (var mapData in MapDatas)
        {
            if (mapData != null && mapData.Map != null && !existingMapDatas.ContainsKey(mapData.Map))
            {
                existingMapDatas.Add(mapData.Map, mapData);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { mapPrefabFolderPath });
        var loadedMapDatas = new List<MapData>(guids.Length);

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                continue;
            }

            GridMapBehaviour map = prefab.GetComponentInChildren<GridMapBehaviour>(true);
            if (map == null)
            {
                continue;
            }

            existingMapDatas.TryGetValue(map, out var existingMapData);
            loadedMapDatas.Add(MapData.Create(
                string.IsNullOrWhiteSpace(existingMapData?.DisplayName) ? prefab.name : existingMapData.DisplayName,
                existingMapData?.Icon,
                existingMapData?.Description ?? string.Empty,
                map));
        }

        mapDatas = loadedMapDatas.ToArray();
        RebuildIndex();
        EditorUtility.SetDirty(this);

        Debug.Log($"Loaded {mapDatas.Length} map prefabs into '{name}'.", this);
    }
#endif
}
