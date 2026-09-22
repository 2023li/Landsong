using System;
using System.Collections.Generic;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.GridSystem;
using Unity.Mathematics;

namespace Landsong.EditorTools
{
    public static class MapNavigationBaker
    {
        public static void Bake(MapContentAuthoring content, MapAsset map)
        {
            var config = content.TwcConfiguration as Configuration;
            var output = new List<NavigationSurface>();
            var links = new List<AuthoredConnection>();
            if (!content.UsesLegacyTerrainInput && content.TerrainRules != null && content.TerrainRules.useLayerBlueprintRules)
            {
                var compiled = LayerTerrainCompiler.Compile(config, content);
                output.AddRange(compiled.Additional);
                links.AddRange(compiled.Slopes.Select(s => s.Connection));
                foreach (var source in content.GetComponentsInChildren<MapNavigationAuthoring>(true).Where(s => s.isActiveAndEnabled))
                {
                    if ((source.Surfaces?.Length ?? 0) > 0 || (source.Connections?.Length ?? 0) > 0)
                        throw new InvalidOperationException("Layer 模式只接受 Layer 端点连接，请移除旧的手填地表高度/连接后重新配置。");
                    foreach (var link in source.LayerConnections ?? Array.Empty<LayerTerrainConnection>())
                        links.Add(LayerTerrainCompiler.Connection(compiled, link));
                }

                map.ElevationStep = 1;
                map.NavigationSurfaces = output.OrderBy(s => s.Surface).ThenBy(s => s.Cell.y).ThenBy(s => s.Cell.x).ToArray();
                map.Connections = links.OrderBy(c => c.Id).ToArray();
                return;
            }

            var ids = new HashSet<int>();
            foreach (var source in content.GetComponentsInChildren<MapNavigationAuthoring>(true))
            {
                if (!source.isActiveAndEnabled)
                    continue;
                foreach (var layer in source.Surfaces)
                {
                    if (layer == null || layer.SurfaceId <= 0 || !ids.Add(layer.SurfaceId) || config == null)
                        throw new InvalidOperationException("导航地表 ID 重复、无效或缺少 TWC 配置。");
                    var blueprint = config.blueprintLayerFolders.SelectMany(f => f.blueprintLayers).SingleOrDefault(b => b.guid == layer.BlueprintGuid);
                    if (blueprint == null)
                        throw new InvalidOperationException("找不到导航地表绑定的 TWC 蓝图层：" + layer.BlueprintGuid);
                    foreach (var p in blueprint.allPositions.OrderBy(p => p.y).ThenBy(p => p.x))
                    {
                        if (math.abs(p.x - math.round(p.x)) > .001f || math.abs(p.y - math.round(p.y)) > .001f)
                            throw new InvalidOperationException("导航地表 XZ 必须是整数逻辑格。");
                        output.Add(new NavigationSurface { Cell = new int2((int)math.round(p.x), (int)math.round(p.y)), Surface = layer.SurfaceId, Elevation = layer.Elevation });
                    }
                }

                links.AddRange(source.Connections ?? Array.Empty<AuthoredConnection>());
            }

            map.NavigationSurfaces = output.OrderBy(s => s.Surface).ThenBy(s => s.Cell.y).ThenBy(s => s.Cell.x).ToArray();
            map.Connections = links.OrderBy(c => c.Id).ToArray();
        }
    }
}
