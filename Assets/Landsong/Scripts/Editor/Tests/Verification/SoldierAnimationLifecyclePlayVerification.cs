#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Landsong.Animation;
using Landsong.AnimationPreview;
using Landsong.ECS;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [InitializeOnLoad]
    public static class SoldierAnimationLifecyclePlayVerification
    {
        const string Key = "Landsong.Animation.LifecycleVerification";
        const string ReportPath = "Library/LandsongEcs/soldier-animation-lifecycle-play.txt";
        static readonly List<string> errors = new();
        static readonly List<Entity> units = new();
        static readonly StringBuilder report = new();
        static Entity root;
        static int stage, nextFrame, assertions;
        static double deadline;
        static SoldierAnimationLifecyclePlayVerification()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    errors.Add(message);
            };
        }

        [MenuItem("Landsong/动画/验证按需创建与释放 (Play)")]
        public static string Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式。");
            SessionState.SetBool(Key, true);
            try
            {
                return SoldierAnimationPreview.Start();
            }
            catch
            {
                SessionState.EraseBool(Key);
                throw;
            }
        }

        static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false))
                return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                errors.Clear();
                units.Clear();
                report.Clear();
                root = Entity.Null;
                stage = assertions = nextFrame = 0;
                deadline = EditorApplication.timeSinceStartup + 120;
                AnimationPreviewSystem.SuppressPopulationForVerification = true;
            }
            else if (mode == PlayModeStateChange.ExitingPlayMode)
            {
                AnimationPreviewSystem.SuppressPopulationForVerification = false;
                SessionState.EraseBool(Key);
                File.WriteAllText(ReportPath, "CANCELLED before completion\n" + report);
            }
        }

        static void Check(bool valid, string label)
        {
            if (!valid)
                throw new InvalidOperationException(label);
            assertions++;
            report.AppendLine("PASS " + label);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying)
                return;
            try
            {
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join("\n", errors));
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Animation lifecycle verification timed out.");
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || Time.frameCount < nextFrame)
                    return;
                var em = world.EntityManager;
                em.CompleteAllTrackedJobs();
                using var settings = em.CreateEntityQuery(typeof(AnimationPreviewSettings));
                if (settings.IsEmptyIgnoreFilter)
                    return;
                void Population(int expected)
                {
                    using var views = em.CreateEntityQuery(typeof(SoldierAnimationViewOwner));
                    using var rigs = em.CreateEntityQuery(typeof(RigDefinitionComponent));
                    using var skins = em.CreateEntityQuery(typeof(SkinnedMeshRendererComponent));
                    using var registration = em.CreateEntityQuery(typeof(MaterialMeshInfo), typeof(DeformedMeshIndex), typeof(SkinnedMeshRendererComponent));
                    Check(views.CalculateEntityCount() == expected && rigs.CalculateEntityCount() == expected && skins.CalculateEntityCount() == expected && registration.CalculateEntityCount() == expected, "Expected " + expected + " actual view roots / rigs / skins / registration-eligible meshes");
                    Check(units.TrueForAll(unit => em.Exists(unit)), "All 200 lightweight gameplay fixtures remain alive");
                    using var instances = views.ToEntityArray(Allocator.Temp);
                    foreach (var view in instances)
                    {
                        var unit = em.GetComponentData<SoldierAnimationViewOwner>(view).Unit;
                        var actual = em.GetComponentData<LocalToWorld>(view).Position;
                        if (math.distance(actual, em.GetComponentData<LocalTransform>(unit).Position) > .01f)
                            throw new InvalidOperationException("Rendered view no longer follows its gameplay parent.");
                    }
                }

                void Deploy(int count)
                {
                    for (var i = 0; i < units.Count; i++)
                    {
                        var actor = em.GetComponentData<Combatant>(units[i]);
                        actor.Deployed = (byte)(i < count ? 1 : 0);
                        em.SetComponentData(units[i], actor);
                    }
                }

                switch (stage)
                {
                    case 0:
                        // The disabled fixture owner is invisible to gameplay queries. Only the real
                        // animation adapter and Rukhanka consume these minimal presentation inputs.
                        root = em.CreateEntity(typeof(Session), typeof(SimulationReady), typeof(Disabled));
                    {
                        em.SetComponentData(root, new Session { Initialized = 1 });
                        EntityState.Set(em, root, new GameClock() { });
                        EntityState.Set(em, root, new SimulationControl() { });
                        EntityState.Set(em, root, new PopulationState() { });
                        EntityState.Set(em, root, new PublicOpinionState() { });
                        EntityState.Set(em, root, new ResearchState() { });
                        EntityState.Set(em, root, new ExpeditionPenaltyState() { });
                        EntityState.Set(em, root, new NightRuntimeState() { });
                        EntityState.Set(em, root, new DaySettlementState() { });
                        EntityState.Set(em, root, new RetryState() { });
                        EntityState.Set(em, root, new HeroSelection() { });
                        EntityState.Set(em, root, new BellState() { });
                        EntityState.Set(em, root, new IntelligenceModeState() { });
                        EntityState.Set(em, root, new PersistenceGate() { });
                        EntityState.Set(em, root, new SimulationRandomState() { });
                        EntityState.Set(em, root, new IdentitySequence() { });
                        EntityState.Set(em, root, new DynastyIdentity() { });
                    }

                        var prefab = settings.GetSingleton<AnimationPreviewSettings>().Prefab;
                        for (var i = 0; i < 200; i++)
                        {
                            var unit = em.CreateEntity(typeof(SoldierAnimationPrefab), typeof(SoldierAnimationState), typeof(UnitAnimationSignals), typeof(SimulationOwner), typeof(Combatant), typeof(Health), typeof(LocalTransform), typeof(LocalToWorld));
                            em.SetComponentData(unit, new SoldierAnimationPrefab { Prefab = prefab, DeathSeconds = 1.5f, AttackSeconds = 1 });
                            em.SetComponentData(unit, new SimulationOwner { Root = root });
                            em.SetComponentData(unit, new Health { Current = 100, Maximum = 100 });
                            em.SetComponentData(unit, new Combatant { Speed = 2.5f, Interval = .8f });
                            em.SetComponentData(unit, LocalTransform.FromPosition(new float3((i % 15 - 7) * 2.6f, .5f, i / 15 * 3.2f)));
                            units.Add(unit);
                        }

                        break;
                    case 1:
                        Population(0);
                        Deploy(50);
                        break;
                    case 2:
                        Population(50);
                        Deploy(200);
                        break;
                    case 3:
                        Population(200);
                        Deploy(0);
                        break;
                    case 4:
                        Population(0);
                        Deploy(200);
                        break;
                    case 5:
                        Population(200);
                        Deploy(0);
                        break;
                    case 6:
                        Population(0);
                        Deploy(200);
                        break;
                    case 7:
                        Population(200);
                        em.DestroyEntity(root);
                        break;
                    case 8:
                        Check(SimulationLifetimeSystem.IsReleased(em), "Session removal releases gameplay fixtures and views");
                        using (var rigs = em.CreateEntityQuery(typeof(RigDefinitionComponent)))
                        using (var skins = em.CreateEntityQuery(typeof(SkinnedMeshRendererComponent)))
                            Check(rigs.CalculateEntityCount() == 0 && skins.CalculateEntityCount() == 0, "Session removal leaves zero rigs or skinned meshes");
                        Check(errors.Count == 0, "Real Rukhanka deformation completed repeated creation and release without errors");
                        Finish(true, "");
                        return;
                }

                stage++;
                nextFrame = Time.frameCount + 20;
            }
            catch (Exception error)
            {
                Finish(false, error.ToString());
            }
        }

        static void Finish(bool passed, string detail)
        {
            SessionState.EraseBool(Key);
            AnimationPreviewSystem.SuppressPopulationForVerification = false;
            report.AppendLine("Assertions: " + assertions);
            report.AppendLine((passed ? "PASS " : "FAIL ") + DateTimeOffset.Now.ToString("O") + "\n" + detail);
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText(ReportPath, report.ToString());
            SoldierAnimationPreview.Stop();
        }
    }
}
#endif
