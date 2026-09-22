using System;
using System.IO;
using System.Linq;
using System.Text;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class SlopeAssetTools
    {
        public const string Root = "Assets/TileWorldCreator/Tiles URP/MyTile";
        [Serializable]
        public sealed class ReferenceMesh
        {
            public string tile, name, material;
            public Vector3[] vertices, normals;
            public Vector2[] uv;
            public int[] triangles;
        }

        [Serializable]
        public sealed class ReferenceData
        {
            public ReferenceMesh[] meshes;
        }

        [MenuItem("Landsong/地图/导出现有陆地斜坡建模参考")]
        static void ExportMenu() => Debug.Log(ExportReferences());
        public static string ExportReferences()
        {
            var result = new System.Collections.Generic.List<ReferenceMesh>();
            foreach (string tile in new[]
            {
                "中心",
                "边"
            }

            )
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/陆地/" + tile + ".prefab");
                var instance = UnityEngine.Object.Instantiate(asset);
                try
                {
                    foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh = filter.sharedMesh;
                        var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                        for (int s = 0; s < mesh.subMeshCount; s++)
                            result.Add(new ReferenceMesh { tile = tile, name = filter.name, material = AssetDatabase.GetAssetPath(materials[s]), vertices = mesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray(), normals = mesh.normals.Select(v => filter.transform.TransformDirection(v).normalized).ToArray(), uv = mesh.uv, triangles = mesh.GetTriangles(s) });
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            Directory.CreateDirectory("ArtSource/MyTileSlope");
            File.WriteAllText("ArtSource/MyTileSlope/land_reference_meshes.json", JsonUtility.ToJson(new ReferenceData { meshes = result.ToArray() }, true));
            return string.Join("\n", result.Select(m => m.name + ": " + m.vertices.Length + " vertices; " + m.material));
        }

        [MenuItem("Landsong/地图/导入并验证斜坡模型")]
        static void ImportMenu() => Debug.Log(ImportAndVerify());
        public static string ImportAndVerify()
        {
            var log = new StringBuilder();
            Directory.CreateDirectory(Root + "/Prefabs/斜坡");
            AssetDatabase.Refresh();
            var grass = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/材质_草地.mat");
            var originalEdge = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/陆地/边.prefab");
            var rock = originalEdge.GetComponentsInChildren<MeshRenderer>().First(r => r.name == "cliff_edge_A").sharedMaterial;
            var source = JsonUtility.FromJson<ReferenceData>(File.ReadAllText("ArtSource/MyTileSlope/land_reference_meshes.json"));
            var topPoints = new System.Collections.Generic.Dictionary<string, Vector3[]>();
            foreach (var role in new[]
            {
                "Middle",
                "Left",
                "Right"
            }

            )
            {
                string path = Root + "/Mesh_Slope/Slope_" + role + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.isReadable = true;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                var wrapper = new GameObject("Slope_" + role);
                try
                {
                    instance.transform.SetParent(wrapper.transform, false);
                    var filters = instance.GetComponentsInChildren<MeshFilter>();
                    var points = filters.Where(f => role == "Middle" || f.name.StartsWith("Grass_Slope_")).SelectMany(f => f.sharedMesh.GetTriangles(0).Select(i => f.transform.TransformPoint(f.sharedMesh.vertices[i]))).ToArray();
                    Require(points.Length > 0, "找不到草面网格：" + role + " / " + string.Join(",", filters.Select(f => f.name)));
                    topPoints.Add(role, points);
                    var bounds = new Bounds(points[0], Vector3.zero);
                    foreach (var p in points)
                        bounds.Encapsulate(p);
                    log.AppendLine(role + " top min=" + bounds.min.ToString("F5") + " max=" + bounds.max.ToString("F5"));
                    Require(bounds.size.x <= 1.031f && Mathf.Abs(bounds.size.z - 1) < .0001f, role + " 必须保持 1 格深，端帽肩部最多外扩 0.03。");
                    Require(bounds.min.y >= -1.001f && Mathf.Abs(bounds.max.y - .5f) < .0001f, role + " 上下口高度错误。");
                    if (role == "Middle")
                        Require(points.All(p => Mathf.Abs(p.y - p.z) < .0001f), "中段必须朝 Unity +Z 上坡，45 度；当前顶点=" + string.Join(",", points.Select(p => p.ToString("F3"))));
                    float innerX = role == "Left" ? .5f : -.5f;
                    if (role != "Middle")
                        Require(points.Where(p => Mathf.Abs(p.x - innerX) < .0001f).All(p => Mathf.Abs(p.y - p.z) < .0001f), role + " 内侧必须与中段相接。");
                    if (role != "Middle")
                        Require(points.Where(p => Mathf.Abs(p.z - .5f) < .0001f).All(p => Mathf.Abs(p.y - .5f) < .0001f), role + " 上口肩部必须接平平台，不能留下裸露岩柱缺口。");
                    foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                        renderer.sharedMaterials = new[]
                        {
                            renderer.name.StartsWith("Cliff_") ? rock : grass
                        };
                    if (role != "Middle")
                    {
                        foreach (var part in new[]
                        {
                            ("Grass_Edge", "Grass_Slope_"),
                            ("cliff_edge_A", "Cliff_Slope_")
                        }

                        )
                        {
                            var original = source.meshes.Single(m => m.name == part.Item1);
                            var filter = filters.Single(f => f.name.StartsWith(part.Item2));
                            var actual = filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
                            var expected = original.vertices.Select(v => new Vector3(-v.x, v.y, -v.z)).Select(v =>
                            {
                                float z = Mathf.Clamp(role == "Left" ? -v.x : v.x, -.5f, .5f);
                                float x = role == "Left" ? v.z : -v.z, y = v.y;
                                if (part.Item1 == "Grass_Edge")
                                {
                                    float t = Mathf.Clamp01((z + .1f) / .6f), blend = t * t * (3 - 2 * t), sign = role == "Left" ? 1 : -1;
                                    float lo = original.vertices.Where(p => Mathf.Abs(p.x + .5f) < .0001f).Max(p => p.z);
                                    float hi = original.vertices.Where(p => Mathf.Abs(p.x - .5f) < .0001f).Max(p => p.z);
                                    float lip = Mathf.Lerp(lo, hi, .5f - v.x), outer = (.5f - sign * x) / (.5f + lip);
                                    x -= sign * (.53f - lip) * outer * blend;
                                    y = .5f + (y - .5f) * (1 - blend);
                                }

                                return new Vector3(x, y - .5f + z, z);
                            }).ToArray();
                            float error = expected.Max(p => actual.Min(q => Vector3.Distance(p, q)));
                            Require(error < .00001f, role + "/" + part.Item1 + " 原网格变换不符：" + error);
                            log.AppendLine("PASS " + role + "/" + part.Item1 + " 改造原网格 " + expected.Length + " 顶点，变换最大误差 " + error.ToString("G4"));
                        }
                    }

                    PrefabUtility.SaveAsPrefabAsset(wrapper, Root + "/Prefabs/斜坡/Slope_" + role + ".prefab");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(wrapper);
                }
            }

            foreach (int width in new[]
            {
                3,
                6
            }

            )
            {
                for (int i = 0; i < width - 1; i++)
                {
                    var a = topPoints[i == 0 ? "Left" : "Middle"].Where(p => Mathf.Abs(p.x - .5f) < .0001f).Select(p => new Vector2(p.y, p.z)).Distinct().ToArray();
                    var b = topPoints[i + 1 == width - 1 ? "Right" : "Middle"].Where(p => Mathf.Abs(p.x + .5f) < .0001f).Select(p => new Vector2(p.y, p.z)).Distinct().ToArray();
                    foreach (var seam in new[]
                    {
                        a,
                        b
                    }

                    )
                        Require(seam.Length >= 2 && seam.All(p => Mathf.Abs(p.x - p.y) < .0001f) && Mathf.Abs(seam.Min(p => p.y) + .5f) < .0001f && Mathf.Abs(seam.Max(p => p.y) - .5f) < .0001f, width + " 格拼接边界未完整覆盖同一条 45 度线段。");
                }

                log.AppendLine("PASS " + width + " 格左右拼接、上下口高差 1、45 度坡面。");
            }

            log.AppendLine("PASS Layer n + 0.5 upper mouth, Layer n - 0.5 lower mouth; prefab root scale=1.");
            File.WriteAllText("Library/LandsongEcs/slope-assets-verification.txt", log.ToString());
            return log.ToString();
        }

        static void Require(bool ok, string error)
        {
            if (!ok)
                throw new InvalidOperationException(error);
        }

        [MenuItem("Landsong/地图/生成斜坡拼接预览")]
        static void PreviewMenu() => Debug.Log(CreatePreview());
        public static string CreatePreview() => ProtrudingSlopePreview.Run();
        public static string InspectReferences()
        {
            var text = new StringBuilder();
            foreach (var name in new[]
            {
                "中心",
                "边"
            }

            )
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/陆地/" + name + ".prefab");
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    foreach (var mesh in instance.GetComponentsInChildren<MeshFilter>())
                    {
                        var points = mesh.sharedMesh.vertices.Select(v => mesh.transform.TransformPoint(v)).ToArray();
                        var bounds = new Bounds(points[0], Vector3.zero);
                        foreach (var p in points)
                            bounds.Encapsulate(p);
                        text.AppendLine(name + "/" + mesh.name + " min=" + bounds.min.ToString("F5") + " max=" + bounds.max.ToString("F5"));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            foreach (var manager in UnityEngine.Object.FindObjectsByType<TileWorldCreatorManager>(FindObjectsSortMode.None))
            {
                var c = manager.configuration;
                text.AppendLine("Manager=" + manager.name + " cellSize=" + c.cellSize + " position=" + manager.transform.position + " scale=" + manager.transform.lossyScale);
                foreach (var build in c.buildLayerFolders.SelectMany(f => f.buildLayers).OfType<TilesBuildLayer>())
                {
                    text.AppendLine(build.layerName + " scale=" + build.scaleOffset + " YOffset=" + build.layerYOffset + " dual=" + build.useDualGrid + " enabled=" + build.isEnabled + " tileLayers=" + string.Join(",", build.tileLayers.Select(t => t.heightOffset)));
                    text.AppendLine("  presets=" + string.Join(",", build.tilePresetsTop.Select(t => t.preset != null ? t.preset.name : "null")));
                    text.AppendLine("  placeOnTop=" + build.placeOnTop + " topOffset=" + build.topOffset + " root=" + (build.LayerObject != null ? build.LayerObject.transform.position.ToString() : "null"));
                    if (build.LayerObject != null)
                    {
                        var mesh = build.LayerObject.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m => m.sharedMesh != null && m.sharedMesh.isReadable && m.sharedMesh.vertexCount == 4);
                        if (mesh != null)
                            text.AppendLine("  center surface Y=" + mesh.sharedMesh.vertices.Select(v => mesh.transform.TransformPoint(v).y).Max() + " rootLocal=" + mesh.transform.localPosition);
                    }
                }

                foreach (var build in c.buildLayerFolders.SelectMany(f => f.buildLayers).OfType<ProtrudingSlopeBuildLayer>())
                    text.AppendLine("Custom slope=" + build.layerName + " enabled=" + build.isEnabled + " blueprint=" + build.assignedBlueprintLayerGuid);
            }

            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText("Library/LandsongEcs/slope-references.txt", text.ToString());
            return text.ToString();
        }
    }
}
