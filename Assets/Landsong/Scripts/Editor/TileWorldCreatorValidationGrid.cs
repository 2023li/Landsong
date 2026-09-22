using System;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.EditorTools
{
    internal static class TileWorldCreatorValidationGrid
    {
        internal static GridMapDefinition CreateValidationGrid(MapContentAuthoring content)
        {
            var manager = content.GetComponent<TileWorldCreatorManager>();
            if (manager == null || manager.configuration == null || !content.UsesLegacyTerrainInput && content.TerrainRules == null)
                throw new InvalidOperationException("请先初始化并配置地图。");
            var profile = ScriptableObject.CreateInstance<TileWorldCreatorMapBakeProfile>();
            GridMapDefinition map = null;
            try
            {
                profile.ApplyContent(content);
                if (!TileWorldCreatorLayerResolver.SyncLayerReferences(profile, manager.configuration, out var error) || !TileWorldCreatorCellCompiler.TryBakeCells(profile, manager.configuration, out var cells, out error))
                    throw new InvalidOperationException(error);
                map = ScriptableObject.CreateInstance<GridMapDefinition>();
                var config = manager.configuration;
                map.ReplaceBakedData(Vector2Int.zero, new Vector2Int(config.width, config.height), config.cellSize, profile.ElevationWorldStep, cells, "", config.name, "validation", "");
                return map;
            }
            catch
            {
                if (map != null)
                    UnityEngine.Object.DestroyImmediate(map);
                throw;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
