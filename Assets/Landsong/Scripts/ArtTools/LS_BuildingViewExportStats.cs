#if UNITY_EDITOR
using System.Collections.Generic;

namespace Landsong.VisualSystem
{
    internal sealed class LS_BuildingViewExportStats
    {
        public int SourceRenderers;
        public int SourceTriangles;
        public readonly int[] Renderers = new int[3];
        public readonly int[] Triangles = new int[3];
        public int DynamicRenderers;
        public int MirroredRenderers;
        public readonly List<string> Warnings = new List<string>();
    }
}
#endif
