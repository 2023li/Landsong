#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Landsong.GridSystem
{
    // Import settings retained only for existing pre-Layer maps, owned by that map's authoring component.
    // New maps use the shared Blueprint enum rules. Bounds and edge width are never stored here.
    [Serializable]
    public sealed class LegacyMapTerrainInput
    {
        public string baseTerrainKey="陆地";
        public bool baseBuildable=true,baseTraversable=true;
        public float elevationWorldStep=1;
        public List<TileWorldCreatorLayerBinding> layerBindings=new List<TileWorldCreatorLayerBinding>();
    }
}

#endif
