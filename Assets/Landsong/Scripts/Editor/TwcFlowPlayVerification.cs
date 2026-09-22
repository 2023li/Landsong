using System;
using System.IO;
using System.Linq;
using Landsong.Verification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.EditorTools
{
    [InitializeOnLoad]
    public static class TwcFlowPlayVerification
    {
        const string Active = "TWCFlow.PlayVerification";
        public const string ReportPath = "Library/LandsongEcs/twc-flow-play.txt";
        static int uphillSamples, downhillSamples;
        static float previousZ, maximumError;
        static double finishAt;
        static bool finished;
        static TwcFlowPlayVerification()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("TWCFlow.RestoreStart", false)) return;
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("TWCFlow.PreviousStart", ""));
                SessionState.SetBool("TWCFlow.RestoreStart", false); SessionState.SetBool(Active, false);
            };
        }

        [MenuItem("Landsong/地图验证/验证 TWC 实际单位上下坡 (Play)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            SessionState.SetString("TWCFlow.PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool("TWCFlow.RestoreStart", true); SessionState.SetBool(Active, true);
            SessionState.SetFloat("TWCFlow.Start", (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TwcFlowVerification.ScenePath);
            if (EditorSceneManager.playModeStartScene == null) throw new InvalidOperationException("请先创建 TWC 验证场景。");
            File.WriteAllText(ReportPath, "RUNNING\n");
            finished = false; uphillSamples = downhillSamples = 0; maximumError = 0; previousZ = 0;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
            if (finished)
            {
                if (EditorApplication.timeSinceStartup > finishAt) EditorApplication.isPlaying = false;
                return;
            }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat("TWCFlow.Start", 0) > 60) throw new Exception("Actual-agent traversal timed out");
                var lab = UnityEngine.Object.FindFirstObjectByType<TwcFlowLab>();
                if (lab == null || lab.SourceCount == 0) return;
                var p = lab.Agents[0].transform.position;
                if (p.z > 6.4f && p.z < 7.3f)
                {
                    if (!lab.Ground(p, out var hit)) throw new Exception("No actual TWC collision surface under moving ramp agent");
                    float error = Mathf.Abs(p.y - hit.point.y); maximumError = Mathf.Max(maximumError, error);
                    if (error > .12f) throw new Exception("Agent separates from rendered ramp: " + error);
                    if (p.z > previousZ) uphillSamples++; if (p.z < previousZ) downhillSamples++;
                }
                previousZ = p.z;
                if (!lab.Arrivals.All(v => v >= 2) || uphillSamples < 3 || downhillSamples < 3) return;
                File.WriteAllText(ReportPath, "PASS " + DateTimeOffset.Now.ToString("O")
                    + "\nAll three actual NavMeshAgents reached both ends; arrivals=" + string.Join(",", lab.Arrivals)
                    + "\nUphill samples=" + uphillSamples + "; downhill samples=" + downhillSamples
                    + "; maximum foot-to-TWC-mesh error=" + maximumError.ToString("F4")
                    + "\nNavigation used " + lab.SourceCount + " real TWC MeshColliders.\n");
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/twc-flow-play.png");
                finished = true; finishAt = EditorApplication.timeSinceStartup + 2;
            }
            catch (Exception e)
            {
                File.WriteAllText(ReportPath, "FAIL " + e); finished = true; finishAt = EditorApplication.timeSinceStartup;
            }
        }
    }

    public static partial class TwcFlowVerification
    {
        public static void Play() => TwcFlowPlayVerification.Run();
    }
}
