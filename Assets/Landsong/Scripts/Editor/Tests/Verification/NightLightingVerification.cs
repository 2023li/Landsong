#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class NightLightingVerification
    {
        [MenuItem("Landsong/ECS/Verification/Night lighting")]
        public static string Run()
        {
            var log = new StringBuilder();
            int checks = 0;
            void Check(bool value, string name)
            {
                if (!value) throw new InvalidOperationException("FAIL " + name);
                checks++;
                log.AppendLine("PASS " + name);
            }
            var scene = EditorSceneManager.OpenPreviewScene(EcsSceneFlow.Game);
            var go = new GameObject("Owned lighting verification");
            try
            {
                EcsGameHost host = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent<EcsGameHost>(out var found)) host = found;
                Check(host != null && host.Sun != null && host.NightLighting != null, "Game scene explicitly configures its sun and lighting settings");
                var sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.5f;
                sun.transform.rotation = Quaternion.Euler(50, -30, 0);
                var day = sun.transform.rotation;
                var settings = host.NightLighting;
                using (var controller = new NightLightingController(sun, settings))
                {
                    var timing = ContentAuthoringContext.Content().Night;
                    var preparationHalf = timing.NightPreparationSeconds * .5f;
                    var nightStart = timing.NightPreparationSeconds;
                    var closureStart = nightStart + timing.NightSeconds;
                    var closureHalf = closureStart + timing.NightClosureSeconds * .5f;
                    var closureEnd = closureStart + timing.NightClosureSeconds;
                    var dawnHalf = timing.DawnSeconds * .5f;
                    var night = new NightRuntimeState { Duration = timing.TotalNightSeconds, Kind = NightKind.Peaceful, Speed = 1 };
                    void Tick(Phase phase, float time, float remaining = 0, bool paused = false) => controller.Tick(phase, new GameClock { PhaseTime = time, DawnRemaining = remaining, DawnSourceNightTime = -1 }, timing, night, paused, 10);
                    bool Near(float value) => Mathf.Abs(sun.intensity - value) < .0001f;
                    Tick(Phase.Day, 0);
                    Check(Near(1.5f) && Quaternion.Angle(day, sun.transform.rotation) < .001f, "Ordinary day preserves authored lighting");
                    Tick(Phase.Deployment, 0);
                    Check(Near(1.5f), "Preparation starts at the day baseline");
                    Tick(Phase.Deployment, preparationHalf);
                    Check(sun.intensity < 1.5f && sun.intensity > 1.5f * settings.NightIntensityRatio && Quaternion.Angle(day, sun.transform.rotation) > 1, "Sunset dims and rotates over the preparation interval");
                    var middle = sun.intensity;
                    var middleRotation = sun.transform.rotation;
                    Tick(Phase.Deployment, preparationHalf, paused: true);
                    Check(Near(middle) && Quaternion.Angle(middleRotation, sun.transform.rotation) < .001f, "Paused preparation holds sunlight");
                    Tick(Phase.Deployment, nightStart);
                    var sunset = sun.transform.rotation;
                    Tick(Phase.Night, nightStart);
                    Check(Near(1.5f * settings.NightIntensityRatio) && Quaternion.Angle(sunset, sun.transform.rotation) < .001f, "Formal night joins sunset continuously");
                    Tick(Phase.Celebration, 150);
                    Check(Near(1.5f * settings.NightIntensityRatio), "Early victory stays dark");
                    var beforeClosure = sun.transform.rotation;
                    Tick(Phase.Retreat, closureStart);
                    Check(Near(1.5f * settings.NightIntensityRatio) && Quaternion.Angle(beforeClosure, sun.transform.rotation) < .001f, "Early closure preserves direction and stays dark");
                    Tick(Phase.Retreat, closureHalf);
                    Check(Near(1.5f * settings.ClosureIntensityRatio) && sun.intensity > 1.5f * settings.NightIntensityRatio, "Closure is brighter than formal night but below daylight");
                    Tick(Phase.Retreat, closureEnd);
                    var sunrise = sun.transform.rotation;
                    Tick(Phase.Day, 0, timing.DawnSeconds);
                    Check(Near(1.5f * settings.ClosureIntensityRatio) && Quaternion.Angle(sunrise, sun.transform.rotation) < .001f, "New-day dawn starts at the brighter closure endpoint");
                    Tick(Phase.Day, 0, dawnHalf);
                    Check(sun.intensity > 1.5f * settings.NightIntensityRatio && sun.intensity < 1.5f && Quaternion.Angle(sunrise, sun.transform.rotation) > 1, "Dawn brightens and rotates through its configured interval");
                    middle = sun.intensity;
                    middleRotation = sun.transform.rotation;
                    Tick(Phase.Day, 0, dawnHalf, true);
                    Check(Near(middle) && Quaternion.Angle(middleRotation, sun.transform.rotation) < .001f, "Pause holds sunrise");
                    // A recreated presenter must reconstruct a saved dawn from the clock alone.
                    var resumedObject = new GameObject("Restored dawn");
                    try
                    {
                        var resumed = resumedObject.AddComponent<Light>();
                        resumed.type = LightType.Directional;
                        resumed.intensity = 1.5f;
                        resumed.transform.rotation = day;
                        using var loaded = new NightLightingController(resumed, settings);
                        loaded.Tick(Phase.Day, new GameClock { DawnRemaining = dawnHalf, DawnSourceNightTime = -1 }, timing, night, false, 0);
                        Check(Mathf.Abs(resumed.intensity - middle) < .0001f && Quaternion.Angle(resumed.transform.rotation, middleRotation) < .001f, "Restored dawn reconstructs the same intensity and direction");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(resumedObject); }
                    Tick(Phase.Day, 0, 0);
                    Check(Near(1.5f) && Quaternion.Angle(day, sun.transform.rotation) < .001f, "Dawn ends at the authored day baseline");
                    Tick(Phase.Deployment, 0);
                    Tick(Phase.Deployment, nightStart);
                    Tick(Phase.Night, nightStart);
                    Tick(Phase.Night, 30);
                    var skippedNightRotation = sun.transform.rotation;
                    controller.Tick(Phase.Day, new GameClock { DawnRemaining = timing.DawnSeconds, DawnSourceNightTime = 30 }, timing, night, false, 0);
                    Check(Near(1.5f * settings.NightIntensityRatio) && Quaternion.Angle(skippedNightRotation, sun.transform.rotation) < .001f, "Peaceful skip starts sunrise from its actual night pose without a flash");
                    controller.Tick(Phase.Day, new GameClock { DawnRemaining = 0, DawnSourceNightTime = 30 }, timing, night, false, 0);
                    Check(Near(1.5f), "Skipped peaceful night reaches daylight after the configured dawn interval");
                    timing.NightPreparationSeconds = timing.DawnSeconds = 0;
                    Tick(Phase.Deployment, 0);
                    Check(Near(1.5f * settings.NightIntensityRatio), "Zero-length preparation immediately reaches night");
                    Tick(Phase.Day, 0);
                    Check(Near(1.5f), "Zero-length dawn immediately reaches day");
                    Tick(Phase.Night, 30);
                }
                Check(Mathf.Abs(sun.intensity - 1.5f) < .0001f && Quaternion.Angle(day, sun.transform.rotation) < .001f, "Unbinding restores the light and direction");
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception e) { log.AppendLine(e.ToString()); throw; }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                EditorSceneManager.ClosePreviewScene(scene);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/night-lighting-verification.txt", log.ToString());
            }
        }
    }
}
#endif
