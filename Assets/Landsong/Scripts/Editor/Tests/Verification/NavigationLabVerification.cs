using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.Verification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    public static class NavigationLabVerification
    {
        public const string ReportPath = "Library/LandsongEcs/navigation-lab-verification.txt";

        [MenuItem("Landsong/地图验证/校验多高度导航实验")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式运行校验。");
            if (!File.Exists(NavigationLabBuilder.ScenePath)) NavigationLabBuilder.Create();
            var lines = new List<string> { DateTimeOffset.Now.ToString("O") };
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(NavigationLabBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(NavigationLabBuilder.ScenePath, OpenSceneMode.Additive);
            NavigationLab lab = null;
            bool wasBridge = true, wasStairs = true;
            void Check(bool value, string description)
            { if (!value) throw new InvalidOperationException(description); lines.Add("PASS " + description); }
            try
            {
                lab = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NavigationLab>()).Single();
                wasBridge = lab.Bridge.activeSelf; wasStairs = lab.Stairs.activeSelf;
                var config = lab.TwcConfiguration as Configuration;
                var layers = config.blueprintLayerFolders.SelectMany(f => f.blueprintLayers).ToArray();
                var valley = layers.Single(l => l.layerName == "land_谷底");
                var bridge = layers.Single(l => l.layerName == "bridge_吊桥");
                Check(Mathf.Abs(valley.defaultLayerHeight - .35f) < .00001f && Mathf.Abs(bridge.defaultLayerHeight - 5.15f) < .00001f,
                    "TWC source retains non-integer heights 0.35 / 5.15");
                Check(bridge.allPositions.All(valley.allPositions.Contains), "Bridge and valley genuinely overlap in XZ in the TWC source");
                Check(layers.Count(l => l.layerName.StartsWith("stairs_")) == 16, "Sixteen authored stair heights retained");
                Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Count() == 1,
                    "One standalone camera; no game boot or map-menu dependency");

                lab.SetConstruction(true, true);
                Check(lab.CompleteRoutes.All(v => v), "Bridge, underpass and stairs all have complete navigation paths");
                Check(NavMesh.SamplePosition(new Vector3(16, .35f, 14), out var low, .22f, lab.Filter)
                    && Mathf.Abs(low.position.y - .35f) < .15f, "Same XZ: valley remains independently navigable at 0.35");
                Check(NavMesh.SamplePosition(new Vector3(16, 5.15f, 14), out var high, .22f, lab.Filter)
                    && Mathf.Abs(high.position.y - 5.15f) < .15f, "Same XZ: bridge remains independently navigable at 5.15");
                Check(!NavMesh.SamplePosition(new Vector3(16, 2.7f, 14), out _, .22f, lab.Filter), "Empty air between bridge and valley is not a third surface");
                lab.TryPath(lab.Starts[0].position, lab.Ends[0].position, out var bridgePath);
                Check(bridgePath.corners.All(p => p.y > 5), "Bridge route never drops onto the valley");
                lab.TryPath(lab.Starts[1].position, lab.Ends[1].position, out var valleyPath);
                Check(valleyPath.corners.All(p => p.y < .6f), "Underpass route never jumps onto the bridge");
                lab.TryPath(lab.Starts[2].position, lab.Ends[2].position, out var stairsPath);
                Check(stairsPath.corners.First().y < .6f && stairsPath.corners.Last().y > 5,
                    "Stair route connects distinct floating-point heights");

                lab.SetConstruction(false, true);
                Check(!lab.CompleteRoutes[0] && lab.CompleteRoutes[1] && lab.CompleteRoutes[2],
                    "Demolishing bridge disconnects plateaus while retaining valley and stairs");
                Check(!NavMesh.SamplePosition(new Vector3(16, 5.15f, 14), out _, .22f, lab.Filter), "Demolished bridge leaves no ghost navigation surface");
                lab.SetConstruction(true, false);
                Check(lab.CompleteRoutes[0] && lab.CompleteRoutes[1] && !lab.CompleteRoutes[2],
                    "Demolishing stairs disconnects valley from plateau; bridge and underpass remain");
                lab.SetConstruction(true, true);
                Check(lab.CompleteRoutes.All(v => v), "Rebuilding both structures restores all routes");
                lab.SetDamaged(true);
                Check(lab.CompleteRoutes.All(v => v) && lab.Filter.GetAreaCost(3) == 3 && lab.Filter.GetAreaCost(4) == 3,
                    "Damage changes travel cost without removing connectivity");
                lab.SetDamaged(false);
                Check(lab.Filter.GetAreaCost(3) == 1 && lab.Filter.GetAreaCost(4) == 1, "Repair restores normal cost");
                var generated = lab.BuildData();
                string navPath = NavigationLabBuilder.Data + "/Generated/Map_NavigationLab_NavMesh.asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                if (existing == null) AssetDatabase.CreateAsset(generated, navPath);
                else { EditorUtility.CopySerialized(generated, existing); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); }
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath));
                lines.Add("COMPLETE " + (lines.Count - 1) + " assertions");
            }
            catch (Exception e) { lines.Add("FAIL " + e); throw; }
            finally
            {
                if (lab != null)
                {
                    lab.ReleaseNavigation(); lab.Bridge.SetActive(wasBridge); lab.Stairs.SetActive(wasStairs);
                    foreach (var route in lab.Routes) route.positionCount = 0;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllLines(ReportPath, lines);
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
