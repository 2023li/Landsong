using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GiantGrey.TileWorldCreator;
using Landsong.Verification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    public static partial class TwcFlowVerification
    {
        public const string ReportPath = "Library/LandsongEcs/twc-flow-verification.txt";
        [MenuItem("Landsong/地图验证/验证 TWC 生成、斜坡与保存重开")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式运行。");
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
                throw new InvalidOperationException("请先关闭 TWC 实验场景，再运行保存重开校验；防止覆盖场景中的手工编辑。");
            if (!File.Exists(ScenePath))
                Create();
            var previous = SceneManager.GetActiveScene();
            Scene scene = default;
            var results = new List<string>
            {
                DateTimeOffset.Now.ToString("O")
            };
            int assertions = 0, seamFailures = 0;
            void Check(bool condition, string description)
            {
                if (!condition)
                    throw new InvalidOperationException(description);
                results.Add("PASS " + description);
                assertions++;
            }

            TwcFlowLab Load()
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TwcFlowLab>()).Single();
            }

            void Close(TwcFlowLab lab)
            {
                lab.Release();
                EditorSceneManager.CloseScene(scene, true);
                scene = default;
            }

            void GeometryChecks(TwcFlowLab lab, string stage)
            {
                var manager = lab.Geometry.GetComponent<TileWorldCreatorManager>();
                Check(manager != null && manager.configuration != null, stage + ": real TWC manager and saved configuration");
                var builds = manager.configuration.buildLayerFolders.SelectMany(f => f.buildLayers).OfType<TilesBuildLayer>().ToArray();
                Check(builds.Length == 3 && builds.All(b => b.useDualGrid && b.mergeTiles && b.colliderType == Configuration.ColliderType.meshCollider), stage + ": three actual TWC dual-grid Tiles Build layers with merged mesh colliders");
                Check(lab.Geometry.GetComponentsInChildren<BoxCollider>(true).Length == 0, stage + ": no box collider surrogate anywhere in generated terrain");
                var colliders = lab.Geometry.GetComponentsInChildren<MeshCollider>();
                Check(colliders.Length > 0 && colliders.All(c => c.sharedMesh != null && c.sharedMesh.isReadable), stage + ": every collision mesh is present and readable");
                Check(colliders.All(c => c.GetComponent<MeshFilter>() != null && c.GetComponent<MeshFilter>().sharedMesh == c.sharedMesh), stage + ": navigation collision triangles are the actual rendered TWC mesh triangles");
                lab.BuildNavigation();
                Check(lab.SourceCount == colliders.Length, stage + ": navigation consumes only TWC generated mesh colliders");
                for (int i = 0; i < 3; i++)
                {
                    Check(lab.Path(lab.Starts[i].position, lab.Ends[i].position, out _), stage + ": route " + i + " forward complete");
                    Check(lab.Path(lab.Ends[i].position, lab.Starts[i].position, out _), stage + ": route " + i + " reverse complete");
                }

                Check(lab.Ground(new Vector3(11.5f, 0, 5), out var foot) && Mathf.Abs(foot.point.y - .35f) < .002f, stage + ": actual ramp foot surface is 0.35 (not blueprint -0.15)");
                Check(lab.Ground(new Vector3(11.5f, 0, 9), out var top) && Mathf.Abs(top.point.y - 1.35f) < .002f, stage + ": actual plateau surface is 1.35 (not blueprint 0.85)");
                float last = .35f;
                int slopeSamples = 0;
                for (float z = 6.35f; z <= 7.45f; z += .1f)
                {
                    Check(lab.Ground(new Vector3(11.5f, 0, z), out var hit) && hit.point.y >= last - .015f, stage + ": continuous uphill mesh at z=" + z.ToString("F2"));
                    last = hit.point.y;
                    if (hit.normal.y > .5f && hit.normal.y < .99f)
                        slopeSamples++;
                    Check(NavMesh.SamplePosition(hit.point, out var nav, .18f, lab.Filter) && Mathf.Abs(nav.position.y - hit.point.y) < .12f, stage + ": navigation height follows sloped mesh at z=" + z.ToString("F2"));
                }

                Check(slopeSamples >= 7, stage + ": at least seven samples on real inclined triangles (not staircase boxes)");
                var seamPoints = new List<Vector3>();
                bool continuousSeam = true;
                for (float x = 10; x <= 13; x += .25f)
                {
                    if (!lab.Ground(new Vector3(x, 0, 6.9f), out var hit))
                        throw new InvalidOperationException("Missing ramp seam mesh");
                    if (!NavMesh.SamplePosition(hit.point, out var sample, .18f, lab.Filter))
                    {
                        continuousSeam = false;
                        results.Add("DETAIL " + stage + ": missing lateral ramp navigation at x=" + x + ", z=6.9, mesh height=" + hit.point.y);
                    }
                    else
                        seamPoints.Add(sample.position);
                }

                continuousSeam &= seamPoints.Skip(1).Select((p, i) => !NavMesh.Raycast(seamPoints[i], p, out _, lab.Filter)).All(v => v);
                if (continuousSeam)
                    Check(true, stage + ": adjacent ramps form a continuous lateral walkable surface");
                else
                {
                    seamFailures++;
                    results.Add("FAIL " + stage + ": adjacent official Ramp meshes have lateral grooves; cannot treat them as a continuous wide ramp");
                }

                Check(NavMesh.SamplePosition(new Vector3(17, .35f, 12), out var outside, .18f, lab.Filter) && NavMesh.SamplePosition(new Vector3(14, 1.35f, 12), out var inside, .18f, lab.Filter) && NavMesh.Raycast(outside.position, inside.position, out _, lab.Filter), stage + ": direct route through vertical cliff is blocked");
                Check(NavMesh.SamplePosition(new Vector3(24, .35f, 11), out var low, .18f, lab.Filter) && NavMesh.SamplePosition(new Vector3(24, 3.35f, 11), out var high, .18f, lab.Filter) && high.position.y - low.position.y > 2.8f, stage + ": TWC upper deck and underpass coexist at identical XZ");
                lab.Release();
            }

            try
            {
                var lab = Load();
                GeometryChecks(lab, "Saved/reopened generation");
                Check(lab.Geometry.GetComponentsInChildren<MeshCollider>().All(c => AssetDatabase.Contains(c.sharedMesh)), "Merged meshes are persisted assets, not editor-memory-only objects");
                string initial = GeometryHash(lab);
                Close(lab);
                AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                lab = Load();
                var manager = lab.Geometry.GetComponent<TileWorldCreatorManager>();
                Generate(manager);
                GeometryChecks(lab, "Regenerated from reimported TWC source");
                Check(initial == GeometryHash(lab), "Actual generated world-space mesh geometry is stable across full TWC regeneration");
                var hill = manager.configuration.buildLayerFolders.SelectMany(f => f.buildLayers).OfType<TilesBuildLayer>().Single(b => b.layerName == "坡顶与斜坡瓦片");
                var overrides = hill.tileLayers[0].layerOverrides.ToArray();
                try
                {
                    hill.tileLayers[0].layerOverrides.Clear();
                    Generate(manager);
                    lab.BuildNavigation();
                    Check(!lab.Path(lab.Starts[0].position, lab.Ends[0].position, out _), "Negative control: removing TWC Ramp override actually disconnects low ground from plateau");
                    Check(lab.Path(lab.Starts[1].position, lab.Ends[1].position, out _) && lab.Path(lab.Starts[2].position, lab.Ends[2].position, out _), "Negative control: deck and underpass unaffected by ramp removal");
                    var connection = lab.gameObject.AddComponent<TwcLabConnection>();
                    try
                    {
                        connection.Register();
                        Check(lab.Path(lab.Starts[0].position, lab.Ends[0].position, out _) && lab.Path(lab.Ends[0].position, lab.Starts[0].position, out _), "Explicit connection: uphill/downhill complete without any Ramp prefab geometry");
                        connection.Unregister();
                        Check(!lab.Path(lab.Starts[0].position, lab.Ends[0].position, out _), "Explicit connection: removing connection disconnects terrain again");
                        connection.LocalExit.y += 1;
                        bool rejected = false;
                        try
                        {
                            connection.Register();
                        }
                        catch (InvalidOperationException)
                        {
                            rejected = true;
                        }

                        Check(rejected, "Explicit connection: wrong-height endpoint is rejected instead of snapping to another surface");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(connection);
                    }
                }
                finally
                {
                    lab.Release();
                    hill.tileLayers[0].layerOverrides.Clear();
                    hill.tileLayers[0].layerOverrides.AddRange(overrides);
                    Generate(manager);
                }

                GeometryChecks(lab, "Restored TWC Ramp override");
                Check(initial == GeometryHash(lab), "Restoring Ramp preset restores original geometry");
                PersistMeshes(manager);
                SaveConfig(manager.configuration);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Failed saving regenerated TWC scene");
                Close(lab);
                lab = Load();
                GeometryChecks(lab, "Reopened after regenerate/save");
                Check(initial == GeometryHash(lab), "Saved regenerated scene preserves exact world-space geometry");
                results.Add("COMPLETE: " + assertions + " passing assertions; " + seamFailures + " lateral seam failures across regeneration stages");
                Close(lab);
                if (seamFailures > 0)
                    Debug.LogWarning("TWC 核心生成与通行检查完成，但并排 Ramp 横向接缝未通过。详见 " + ReportPath);
            }
            catch (Exception e)
            {
                results.Add("FAIL " + e);
                throw;
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                File.WriteAllLines(ReportPath, results);
            }
        }

        static string GeometryHash(TwcFlowLab lab)
        {
            var vertices = new List<string>();
            foreach (var filter in lab.Geometry.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var p = filter.transform.TransformPoint(vertex);
                    vertices.Add(Mathf.RoundToInt(p.x * 10000) + "," + Mathf.RoundToInt(p.y * 10000) + "," + Mathf.RoundToInt(p.z * 10000));
                }

            vertices.Sort(StringComparer.Ordinal);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join(";", vertices))));
        }
    }
}
