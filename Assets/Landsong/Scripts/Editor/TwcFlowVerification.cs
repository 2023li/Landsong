using System;
using System.IO;
using System.Linq;
using GiantGrey.TileWorldCreator;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static partial class TwcFlowVerification
    {
        public static void Inspect()
        {
            var lines = new System.Collections.Generic.List<string>();
            foreach (string suffix in new[] { "BaseBlockPresetGreen", "BaseBlockPresetGreenRamp" })
            {
                var preset = AssetDatabase.LoadAssetAtPath<TilePreset>("Assets/TileWorldCreator/Tiles URP/BaseBlockTiles/" + suffix + ".asset");
                foreach (var prefab in new[] { preset.DUALGRD_fillTile, preset.DUALGRD_edgeTile, preset.DUALGRD_cornerTile })
                    foreach (var mesh in prefab.GetComponentsInChildren<MeshFilter>())
                    {
                        var points = mesh.sharedMesh.vertices.Select(v => mesh.transform.TransformPoint(v)).ToArray();
                        lines.Add(suffix + " / " + prefab.name + " readable=" + mesh.sharedMesh.isReadable + " vertices=" + points.Length
                            + " min=" + points.Aggregate(Vector3.Min) + " max=" + points.Aggregate(Vector3.Max));
                    }
            }
            File.WriteAllLines("Library/LandsongEcs/twc-flow-inspect.txt", lines);
        }
    }
}
