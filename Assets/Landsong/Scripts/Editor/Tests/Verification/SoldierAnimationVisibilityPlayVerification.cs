#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Landsong.Animation;
using Landsong.AnimationPreview;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    // Uses a fresh Play world: a mesh registered by a previous visible unit would hide this regression.
    [InitializeOnLoad]
    public static class SoldierAnimationVisibilityPlayVerification
    {
        const string Key = "Landsong.Animation.VisibilityVerification";
        const string ReportPath = "Library/LandsongEcs/soldier-animation-visibility-play.txt";
        static readonly List<string> errors = new();
        static readonly StringBuilder report = new();
        static int stage, nextFrame, assertions;
        static double deadline;

        static SoldierAnimationVisibilityPlayVerification()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, trace, type) => {
                if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    errors.Add(message);
            };
        }

        [MenuItem("Landsong/动画/验证首次隐藏与显示 (Play)")]
        public static string Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出运行模式。");
            SessionState.SetBool(Key, true);
            try { return SoldierAnimationPreview.Start(); }
            catch { SessionState.EraseBool(Key); throw; }
        }

        static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                errors.Clear(); report.Clear(); stage = 0; assertions = 0; nextFrame = 0;
                deadline = EditorApplication.timeSinceStartup + 90;
                AnimationPreviewSystem.StartHiddenForVerification = true;
            }
            else if (mode == PlayModeStateChange.ExitingPlayMode)
            {
                AnimationPreviewSystem.StartHiddenForVerification = false;
                SessionState.EraseBool(Key);
                File.WriteAllText(ReportPath, "CANCELLED before completion\n" + report);
            }
        }

        static void Check(bool valid, string label)
        {
            if (!valid) throw new InvalidOperationException(label);
            assertions++; report.AppendLine("PASS " + label);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Animation visibility verification timed out.");
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || Time.frameCount < nextFrame) return;
                var em = world.EntityManager; em.CompleteAllTrackedJobs();
                using var query = em.CreateEntityQuery(typeof(AnimationPreviewUnit), typeof(SoldierAnimationBinding));
                using var units = query.ToEntityArray(Allocator.Temp);
                if (units.Length == 0) return;
                void Visible(bool expected)
                {
                    int count = 0;
                    foreach (var unit in units)
                    foreach (var renderer in em.GetBuffer<SoldierAnimationRenderer>(unit))
                    {
                        bool visible = em.IsComponentEnabled<MaterialMeshInfo>(renderer.Entity) && !em.HasComponent<DisableRendering>(renderer.Entity);
                        if (visible != expected) throw new InvalidOperationException("Renderer visibility=" + expected + ": " + em.GetName(renderer.Entity));
                        count++;
                    }
                    Check(count >= units.Length * 2, units.Length + " soldiers / " + count + " body and weapon render entities: visibility=" + expected);
                    using var registration = em.CreateEntityQuery(typeof(MaterialMeshInfo), typeof(DeformedMeshIndex), typeof(SkinnedMeshRendererComponent));
                    Check(registration.CalculateEntityCount() == units.Length, "Every soldier remains eligible for the actual Rukhanka registration query");
                }
                void SetVisible(bool value)
                {
                    foreach (var unit in units) SoldierAnimationSystem.SetRenderersVisible(em, unit, value);
                }
                switch (stage)
                {
                    case 0:
                        Check(units.Length == 9, "Fresh world starts with nine hidden soldiers");
                        Check(world.GetExistingSystemManaged<MeshDeformationSystem>()?.Enabled == true, "Real mesh deformation system is enabled");
                        using (var skins = em.CreateEntityQuery(typeof(SkinnedMeshRendererComponent)))
                        using (var meshes = skins.ToEntityArray(Allocator.Temp))
                        {
                            Check(meshes.Length == units.Length, "Nine real skinned meshes loaded");
                            foreach (var mesh in meshes)
                            {
                                var skin = em.GetComponentData<SkinnedMeshRendererComponent>(mesh);
                                report.AppendLine("SKIN " + em.GetName(mesh) + "; hash=" + skin.smrInfoBlob.Value.hash + "; MMI=" + em.IsComponentEnabled<MaterialMeshInfo>(mesh));
                            }
                        }
                        break;
                    case 1: Visible(false); SetVisible(true); break;
                    case 2: Visible(true); SoldierAnimationPreview.SetCount(200); break;
                    case 3: Check(units.Length == 200, "Two hundred soldiers created hidden"); Visible(false); SetVisible(true); break;
                    case 4: Visible(true); SetVisible(false); break;
                    case 5: Visible(false); SetVisible(true); break;
                    case 6: Visible(true); Check(errors.Count == 0, "Real Rukhanka deformation completes hidden/show/hide/show frames without errors"); Finish(true, ""); return;
                }
                stage++; nextFrame = Time.frameCount + 20;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        static void Finish(bool passed, string detail)
        {
            SessionState.EraseBool(Key);
            AnimationPreviewSystem.StartHiddenForVerification = false;
            report.AppendLine("Assertions: " + assertions);
            report.AppendLine((passed ? "PASS " : "FAIL ") + DateTimeOffset.Now.ToString("O") + "\n" + detail);
            Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText(ReportPath, report.ToString());
            SoldierAnimationPreview.Stop();
        }
    }
}
#endif
