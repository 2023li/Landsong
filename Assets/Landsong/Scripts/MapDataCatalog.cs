using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Landsong/Map/Map Catalog", fileName = "MapCatalog")]
public sealed class MapDataCatalog : SingletonScriptableObject<MapDataCatalog>
{
    [SerializeField, LabelText("地图定义")]
    private MapDefinition[] maps = Array.Empty<MapDefinition>();

    private Dictionary<string, MapDefinition> mapsById;

    public IReadOnlyList<MapDefinition> Maps => maps ?? Array.Empty<MapDefinition>();
    public int Count => maps == null ? 0 : maps.Length;

    public static Task<MapDataCatalog> LoadAsync(string addressableKey)
    {
        return GetInstanceAsync(addressableKey);
    }

    private void OnEnable()
    {
        RebuildIndex();
    }

    private void OnValidate()
    {
        maps ??= Array.Empty<MapDefinition>();
        RebuildIndex();
    }

    public bool TryGetMapDefinition(int index, out MapDefinition definition)
    {
        if (maps == null || index < 0 || index >= maps.Length)
        {
            definition = null;
            return false;
        }

        definition = maps[index];
        return definition != null && definition.IsValid;
    }

    public bool TryGetMapDefinition(string mapId, out MapDefinition definition)
    {
        definition = null;
        mapId = string.IsNullOrWhiteSpace(mapId) ? string.Empty : mapId.Trim();
        if (string.IsNullOrEmpty(mapId))
        {
            return false;
        }

        EnsureIndex();
        return mapsById.TryGetValue(mapId, out definition) && definition != null && definition.IsValid;
    }

    public MapDefinition GetFirstValidMapDefinition()
    {
        foreach (var definition in GetValidMapDefinitions())
        {
            return definition;
        }

        return null;
    }

    public IEnumerable<MapDefinition> GetValidMapDefinitions()
    {
        if (maps == null)
        {
            yield break;
        }

        for (var i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].IsValid)
            {
                yield return maps[i];
            }
        }
    }

    public void RebuildIndex()
    {
        mapsById = new Dictionary<string, MapDefinition>(StringComparer.Ordinal);
        if (maps == null)
        {
            return;
        }

        for (var i = 0; i < maps.Length; i++)
        {
            var definition = maps[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.MapId))
            {
                continue;
            }

            if (mapsById.ContainsKey(definition.MapId))
            {
                Debug.LogError($"MapCatalog 中存在重复 MapId：{definition.MapId}", definition);
                continue;
            }

            mapsById.Add(definition.MapId, definition);
        }
    }

    private void EnsureIndex()
    {
        if (mapsById == null)
        {
            RebuildIndex();
        }
    }

#if UNITY_EDITOR
    [SerializeField, FolderPath(RequireExistingPath = true), LabelText("MapDefinition 目录")]
    private string mapDefinitionFolderPath = "Assets/Landsong/Objects/SO/Map";

    [Button("从目录登记 MapDefinition")]
    private void LoadDefinitionsFromFolder()
    {
        if (!AssetDatabase.IsValidFolder(mapDefinitionFolderPath))
        {
            Debug.LogError($"无效 MapDefinition 目录：{mapDefinitionFolderPath}", this);
            return;
        }

        var guids = AssetDatabase.FindAssets("t:MapDefinition", new[] { mapDefinitionFolderPath });
        var loaded = new List<MapDefinition>(guids.Length);
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var definition = AssetDatabase.LoadAssetAtPath<MapDefinition>(path);
            if (definition != null)
            {
                loaded.Add(definition);
            }
        }

        loaded.Sort((left, right) => string.Compare(left.MapId, right.MapId, StringComparison.Ordinal));
        maps = loaded.ToArray();
        RebuildIndex();
        EditorUtility.SetDirty(this);
    }

    public void RegisterDefinition(MapDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        var list = new List<MapDefinition>(maps ?? Array.Empty<MapDefinition>());
        var index = list.FindIndex(candidate => candidate != null
                                                && string.Equals(
                                                    candidate.MapId,
                                                    definition.MapId,
                                                    StringComparison.Ordinal));
        if (index >= 0)
        {
            list[index] = definition;
        }
        else
        {
            list.Add(definition);
        }

        list.Sort((left, right) => string.Compare(left?.MapId, right?.MapId, StringComparison.Ordinal));
        maps = list.ToArray();
        RebuildIndex();
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
#endif
}
