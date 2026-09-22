using System;
using System.Linq;
using GiantGrey.TileWorldCreator;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class MapEdgeLayerUtility
    {
        public static int RemoveObsolete(Configuration configuration)
        {
            int removed=0;
            foreach(var folder in configuration.blueprintLayerFolders.Where(f=>f!=null).ToArray())
                foreach(var layer in folder.blueprintLayers.Where(b=>b!=null && b.layerName=="边缘区").ToArray())
                {
                    if(configuration.buildLayerFolders.SelectMany(f=>f.buildLayers).Any(b=>b!=null && b.assignedBlueprintLayerGuid==layer.guid))
                        throw new InvalidOperationException("旧边缘区仍被 Build Layer 使用，请先解除引用。");
                    folder.blueprintLayers.Remove(layer);folder.assignedBlueprintLayers.Remove(layer.guid);
                    AssetDatabase.RemoveObjectFromAsset(layer);UnityEngine.Object.DestroyImmediate(layer,true);removed++;
                }
            configuration.blueprintLayerFolders.RemoveAll(f=>f!=null && f.folderName=="地图逻辑" && f.blueprintLayers.Count==0);
            if(removed>0)EditorUtility.SetDirty(configuration);return removed;
        }
    }
}
