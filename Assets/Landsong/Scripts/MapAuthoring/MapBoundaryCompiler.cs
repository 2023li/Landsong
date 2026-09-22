using System;
using System.Collections.Generic;
using GiantGrey.TileWorldCreator;
using UnityEngine;

namespace Landsong.EditorTools
{
    // Canvas extent is independent of which Blueprint cells contain terrain.
    public sealed class MapBoundaryCompiler
    {
        public readonly HashSet<Vector2Int> Cells=new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> Edge=new HashSet<Vector2Int>();
        public MapBoundaryCompiler(Configuration config,int width)
        {
            if(config==null || config.width<=0 || config.height<=0)throw new InvalidOperationException("TWC Settings 的宽和高必须大于 0。");
            if(width<0)throw new InvalidOperationException("边缘格数不能为负数。");
            for(int z=0;z<config.height;z++)for(int x=0;x<config.width;x++)
            {
                var cell=new Vector2Int(x,z);Cells.Add(cell);
                if(x<width || z<width || config.width-1-x<width || config.height-1-z<width)Edge.Add(cell);
            }
        }
        public void RequireInside(Vector2Int cell,string layer)
        {if(!Cells.Contains(cell))throw new InvalidOperationException(layer+" 包含 TWC Settings 宽高范围外的格子："+cell);}
    }
}
