using System;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class MapBoundaryTools
    {
        public static string Inspect(MapContentAuthoring content)
        {
            var config=content.GetComponent<TileWorldCreatorManager>()?.configuration ?? content.TwcConfiguration as Configuration;
            var bounds=new MapBoundaryCompiler(config,content.EdgeWidth);
            var preview=TileWorldCreatorMapBaker.CreateValidationGrid(content);
            try
            {
                int entrances=preview.Cells.Count(c=>c.EdgeZone && c.Traversable);
                return $"TWC 范围：{config.width} × {config.height}，共 {bounds.Cells.Count} 格；实际地表 {preview.Cells.Count} 格；向内边缘 {content.EdgeWidth} 格，共 {bounds.Edge.Count} 格，其中可通行入场格 {entrances}。"
                    +(entrances==0?" 当前没有可通行边缘，完整玩法地图需要调整地形或边缘宽度。":"");
            }
            finally{UnityEngine.Object.DestroyImmediate(preview);}
        }
    }
}
