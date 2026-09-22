#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class IntelligenceVerification
    {
        static StringBuilder log;
        static int count;
        static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException("FAIL " + name);
            count++;
            log.AppendLine("PASS " + name);
        }

        static void Reject(Action action, string name)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            Check(rejected, name);
        }

        [MenuItem("Landsong/ECS/Verification/Intelligence")]
        public static string Run()
        {
            log = new StringBuilder();
            count = 0;
            try
            {
                Configuration();
                Simulation();
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/intelligence-verification.txt", log.ToString());
            }
        }

        static void Configuration()
        {
            var owner = new UnityEngine.GameObject("Intelligence configuration verification");
            try
            {
                var settings = owner.AddComponent<IntelligenceSettingsAuthoring>().Settings;
                IntelligenceSettingsAuthoring.Validate(settings);
                Check(true, "Formal intelligence content validates");
                var invalid = settings;
                invalid.LowIntel = invalid.MediumIntel;
                Reject(() => IntelligenceSettingsAuthoring.Validate(invalid), "Non-increasing intelligence tiers rejected");
                invalid = settings;
                invalid.HighIntelLead = float.NaN;
                Reject(() => IntelligenceSettingsAuthoring.Validate(invalid), "NaN forecast lead rejected");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        static void Simulation()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Wave twelve isolated intelligence", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var cfg = em.GetComponentData<NightSettings>(root);
                IntelligenceSettings cfgIntelligence = em.GetComponentData<IntelligenceSettings>(root);
                cfg.FirstInvasion = 1;
                cfg.InvasionChance = 1;
                cfg.FirstBoss = 99999;
                // This scenario requires distinct forecast windows, independent of content tuning.
                cfgIntelligence.MediumIntelLead = 12;
                cfgIntelligence.HighIntelLead = 24;
                {
                    em.SetComponentData(root, cfg);
                    em.SetComponentData(root, cfgIntelligence);
                }

                var session = em.GetComponentData<Session>(root);
                GameClock sessionClock = em.GetComponentData<GameClock>(root);
                sessionClock.Turn = 8;
                session.Phase = Phase.Day;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                }

                EntityState.Set(em, root, new NightPlanState { BossDefinition = EnemyId.None });
                NightOps.Plan(em, root, false);
                var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>();
                checkpoint.Update();
                var original = SnapshotCodec.Capture(em, root);
                BuildingId Def(string value) => BuildingDefinitions.Find(em, root, new FixedString128Bytes(value));
                void Level(int value)
                {
                    var s = em.GetComponentData<Session>(root);
                    GameClock sClock = em.GetComponentData<GameClock>(root);
                    IntelligenceModeState sIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
                    s.Phase = Phase.Day;
                    sIntelligenceMode.Enabled = 0;
                    {
                        em.SetComponentData(root, s);
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sIntelligenceMode);
                    }

                    EntityState.Set(em, root, new RecoveryState { Turn = sClock.Turn, KnownIntel = value });
                }

                var unknown = IntelOps.Read(em, root);
                Check(unknown.Tier == 0 && unknown.Areas.Count == 0 && unknown.Lines.Count == 1, "Zero intelligence reveals no night classification or region");
                Level(cfgIntelligence.LowIntel);
                var low = IntelOps.Read(em, root);
                Check(low.Tier == 1 && low.Areas.Count == 0 && low.WaveChoices == 0 && low.Lines.Count == 2, "Low tier exposes only classification and fixed threat");
                var first = em.GetBuffer<NightWave>(root)[0];
                NightRuntimeState s0Night = em.GetComponentData<NightRuntimeState>(root);
                var p0 = NightPlanOps.State(em, root);
                IntelOps.Refresh(em, root);
                var fingerprint = em.GetComponentData<RecoveryState>(root).IntelFingerprint;
                var changed = first;
                changed.Count = math.min(256, first.Count + 4);
                changed.Direction = first.Direction == 10 ? 20 : 10;
                NightPlanOps.SetWave(em, root, 0, changed);
                IntelOps.Refresh(em, root);
                Check(em.GetComponentData<RecoveryState>(root).IntelFingerprint == fingerprint, "Hidden wave counts/directions cannot produce low-tier unread signals");
                NightPlanOps.SetWave(em, root, 0, first);
                first.Count = 35;
                NightPlanOps.SetWave(em, root, 0, first);
                Level(cfgIntelligence.MediumIntel);
                IntelOps.Refresh(em, root);
                var medium = IntelOps.Read(em, root);
                Check(medium.Tier == 2 && medium.Lines.Any(l => l.Contains("约")) && medium.Areas.Any(a => !a.Target), "Medium tier supplies types ranges and legal approximate regions");
                foreach (var wave in em.GetBuffer<NightWave>(root))
                {
                    var range = IntelOps.CountRange(wave.Count);
                    Check(range.x <= wave.Count && range.y >= wave.Count && range.x < range.y, "Medium numerical interval includes actual count");
                    var highArea = IntelOps.SpawnArea(em, root, wave, 3);
                    var mediumArea = IntelOps.SpawnArea(em, root, wave, 2);
                    Check(highArea.Size.x <= mediumArea.Size.x + .01f && highArea.Size.z <= mediumArea.Size.z + .01f, "High area no wider than medium");
                    for (int i = 0; i < wave.Count; i++)
                    {
                        var preferred = wave.Position + new float3((i % 5 - 2) * .6f, 0, i / 5 * .6f);
                        if (NightSpatialOps.SpawnPoint(em, root, wave.Region, preferred, out var point))
                            Check(highArea.Contains(point) && mediumArea.Contains(point), "Both region tiers contain each actual deployment point");
                    }

                    var r = em.GetBuffer<SpawnRegion>(root)[wave.Region];
                    Check(NightSpatialOps.Inside(r, highArea.Center - highArea.Size * .5f) && NightSpatialOps.Inside(r, highArea.Center + highArea.Size * .5f), "High region stays inside authored invasion area");
                }

                Level(100);
                IntelOps.Refresh(em, root);
                var high = IntelOps.Read(em, root);
                Check(high.Tier == 3 && high.Lines.Any(l => l.Contains(" × ")) && !high.Lines.Any(l => l.Contains("约")), "High tier aggregates exact counts by type");
                using (var groupedWaves = em.GetBuffer<NightWave>(root).ToNativeArray(Allocator.Temp))
                    Check(high.Lines.Count(l => l.Contains(" × ")) == groupedWaves.ToArray().Select(w => w.Definition).Distinct().Count(), "High counts are grouped by type not leaked wave rows");
                Check(em.GetComponentData<NightRuntimeState>(root).Seed == s0Night.Seed && em.GetComponentData<NightRuntimeState>(root).Threat == s0Night.Threat && NightPlanOps.State(em, root).BaseThreat == p0.BaseThreat, "Viewing intelligence never consumes random state or recalculates budget");
                var before = em.GetBuffer<NightWave>(root).ToNativeArray(Allocator.Temp);
                BuildingChangeNotifications.Publish(em, root);
                var after = em.GetBuffer<NightWave>(root);
                for (int i = 0; i < before.Length; i++)
                    Check(before[i].Count == after[i].Count && before[i].Definition == after[i].Definition && before[i].At == after[i].At, "Layout reprojection preserves locked composition and schedule");
                before.Dispose();
                var recovery = em.GetComponentData<RecoveryState>(root);
                IntelOps.MarkRead(em, root, recovery.IntelFingerprint + 1);
                Check(IntelOps.Read(em, root).Unread, "Stale read token cannot clear new information");
                IntelOps.MarkRead(em, root, recovery.IntelFingerprint);
                Check(!IntelOps.Read(em, root).Unread, "Exact read token clears ordinary unread badge");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                Check(IntelOps.Known(em, root) == 100, "Rewinding day does not erase acquired knowledge");
                // Operational source failure combinations, without inventing a second rule evaluator.
                var tower = BuildingCreation.Create(em, root, Def("b瞭望塔"), new int2(-1200, -1200), 0, 1, true);
                var building = em.GetComponentData<Building>(tower);
                BuildingWorkforceState buildingWorkforce = em.GetComponentData<BuildingWorkforceState>(tower);
                BuildingMaintenanceState buildingMaintenance = em.GetComponentData<BuildingMaintenanceState>(tower);
                buildingWorkforce.Workers = 2;
                {
                    em.SetComponentData(tower, building);
                    em.SetComponentData(tower, buildingWorkforce);
                    em.SetComponentData(tower, buildingMaintenance);
                }

                var current = IntelOps.Current(em, root);
                Check(current > 0, "Active source contributes points");
                SourceConditions(em, root);
                buildingWorkforce.Workers = 1;
                {
                    em.SetComponentData(tower, building);
                    em.SetComponentData(tower, buildingWorkforce);
                    em.SetComponentData(tower, buildingMaintenance);
                }

                Check(IntelOps.Current(em, root) == 0 && IntelOps.Sources(em, root).Any(x => x.Reason.Contains("工人不足")), "Understaffed source explains required worker count");
                buildingWorkforce.Workers = 2;
                {
                    em.SetComponentData(tower, building);
                    em.SetComponentData(tower, buildingWorkforce);
                    em.SetComponentData(tower, buildingMaintenance);
                }

                buildingMaintenance.Maintained = 0;
                {
                    em.SetComponentData(tower, building);
                    em.SetComponentData(tower, buildingWorkforce);
                    em.SetComponentData(tower, buildingMaintenance);
                }

                Check(IntelOps.Current(em, root) == 0 && IntelOps.Sources(em, root).Any(x => x.Reason.Contains("维护")), "Unmaintained source explains why contribution is zero");
                buildingMaintenance.Maintained = 1;
                building.Stage = LifeStage.Ruined;
                {
                    em.SetComponentData(tower, building);
                    em.SetComponentData(tower, buildingWorkforce);
                    em.SetComponentData(tower, buildingMaintenance);
                }

                Check(IntelOps.Current(em, root) == 0, "Ruined source has no current function");
                Check(IntelOps.Known(em, root) == 100, "Source ruin does not erase highest knowledge");
                var s = em.GetComponentData<Session>(root);
                GameClock sClock = em.GetComponentData<GameClock>(root);
                SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
                NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
                sNight.Intelligence = 100;
                s.Phase = Phase.Night;
                sControl.Paused = 0;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                }

                var plan = NightPlanOps.State(em, root);
                plan.ClockStarted = 1;
                plan.AnySpawned = 1;
                plan.CombatElapsed = cfg.NightPreparationSeconds;
                EntityState.Set(em, root, plan);
                var night = IntelOps.Read(em, root);
                Check(night.Lines.Any(l => l.Contains("方向")) && !night.Lines.Any(l => l.Contains(" × ") || l.Contains("约") || l.Contains("秒")), "Real-time high tier reveals direction only");
                var scheduled = em.GetBuffer<NightWave>(root).ToNativeArray(Allocator.Temp);
                var leadWave = scheduled[0];
                // Wave fractions span the formal night; preparation is a separate clock offset.
                var firstLead = math.lerp(cfgIntelligence.MediumIntelLead, cfgIntelligence.HighIntelLead, .5f);
                leadWave.At = firstLead / cfg.NightSeconds;
                leadWave.Spawned = 0;
                leadWave.SpatiallyBlocked = 0;
                em.GetBuffer<NightWave>(root).Clear();
                em.GetBuffer<NightWave>(root).Add(leadWave);
                sNight.Intelligence = cfgIntelligence.MediumIntel;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                }

                EntityState.Set(em, root, new RecoveryState { Turn = sClock.Turn, KnownIntel = cfgIntelligence.MediumIntel });
                Check(IntelOps.Read(em, root).WaveChoices == 0, "Medium forecast does not name directions before its lead window");
                sNight.Intelligence = 100;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                }

                Check(IntelOps.Read(em, root).WaveChoices == 1, "High tier receives a longer lead window");
                var simultaneous = leadWave;
                simultaneous.Direction = leadWave.Direction == 10 ? 20 : 10;
                em.GetBuffer<NightWave>(root).Add(simultaneous);
                Check(IntelOps.Read(em, root).WaveChoices == 1 && IntelOps.Read(em, root).Lines.Any(l => l.Contains("、")), "Simultaneous multi-region arrivals share one directional group");
                var later = leadWave;
                later.At = math.lerp(firstLead, cfgIntelligence.HighIntelLead, .5f) / cfg.NightSeconds;
                em.GetBuffer<NightWave>(root).Add(later);
                Check(IntelOps.Read(em, root, 1).SelectedWave == 1 && IntelOps.Read(em, root, 1).Areas.Any(a => a.Secondary), "Switching known wave fades other entrances");
                for (int i = 0; i < em.GetBuffer<NightWave>(root).Length; i++)
                {
                    var w = em.GetBuffer<NightWave>(root)[i];
                    w.Spawned = 1;
                    NightPlanOps.SetWave(em, root, i, w);
                }

                var enemy = EnemyEntities.Spawn(em, root, leadWave.Definition, leadWave.Position, false);
                var core = WorldQueries.Find(em, leadWave.Target);
                Check(core != Entity.Null, "Dynamic target fixture has reachable core");
                EnemyCombatants.Configure(em, root, enemy, true, leadWave.Target, leadWave.Position);
                Check(IntelOps.Read(em, root).Areas.Any(a => a.Target && a.Contains(EntityState.Position(em, core))), "Live actor target controls red region after wave has spawned");
                var actor = em.GetComponentData<Combatant>(enemy);
                actor.HomeId = 0;
                em.SetComponentData(enemy, actor);
                Check(!IntelOps.Read(em, root).Areas.Any(a => a.Target), "Lost live target removes stale NightWave target region");
                em.DestroyEntity(enemy);
                em.GetBuffer<NightWave>(root).Clear();
                em.GetBuffer<NightWave>(root).AddRange(scheduled);
                scheduled.Dispose();
                Check(GameRequestExecution.Execute(em, root, new SetIntelligenceModeRequest { Enabled = true }) == ResultCode.Success && em.GetComponentData<SimulationControl>(root).Paused == 0, "Entering intelligence does not pause combat");
                void Blocked<T>(T request)
                    where T : unmanaged, IGameRequest => Check(GameRequestExecution.Execute(em, root, request) == ResultCode.Busy, "Authority blocks gameplay during intelligence: " + request.Kind);
                Blocked(new BuildRequest());
                Blocked(new MoveBuildingRequest());
                Blocked(new WakeHeroRequest());
                Blocked(new MoveHeroRequest());
                Blocked(new RingBellRequest());
                Blocked(new PickUpRequest());
                Blocked(new AdvanceRequest());
                Blocked(new SetNightSpeedRequest());
                var beforeCamera = SnapshotCodec.Capture(em, root);
                Check(GameRequestExecution.Execute(em, root, new CameraZoomedRequest()) == ResultCode.Success, "Camera commands remain allowed");
                Check(beforeCamera.SequenceEqual(SnapshotCodec.Capture(em, root)), "Intelligence camera motion does not mutate tutorial progress or gameplay");
                Check(GameRequestExecution.Execute(em, root, new SetIntelligenceModeRequest { Enabled = false }) == ResultCode.Success, "Exit clears intelligence command gate");
                {
                    s = em.GetComponentData<Session>(root);
                    sClock = em.GetComponentData<GameClock>(root);
                    sControl = em.GetComponentData<SimulationControl>(root);
                    sNight = em.GetComponentData<NightRuntimeState>(root);
                }

                s.Phase = Phase.Day;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                }

                var bytes = SnapshotCodec.Capture(em, root);
                var data = SnapshotCodec.Decode(em, root, bytes);
                data.Night.Intelligence = 101;
                Reject(() => SnapshotCodec.Restore(em, root, data), "Out of range checkpoint intelligence rejected");
                var live = SnapshotCodec.Capture(em, root);
                var originalRecovery = em.GetComponentData<RecoveryState>(root);
                Reject(() => SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original), probe: step =>
                {
                    if (step == "root-published")
                        throw new InvalidOperationException("Owned intel rollback probe");
                }), "Publication fault rolls back intelligence state");
                Check(originalRecovery.Equals(em.GetComponentData<RecoveryState>(root)) && live.SequenceEqual(SnapshotCodec.Capture(em, root)), "Knowledge/read state and world survive failed restore intact");
                var archive = checkpoint.Export(root);
                archive.Recovery = originalRecovery;
                var decoded = RunArchiveCodec.Decode(RunArchiveCodec.Encode(archive));
                Check(decoded.Recovery.Equals(originalRecovery), "Archive preserves highest known and read fingerprints");
                RetryKnowledge(em, root, checkpoint);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void SourceConditions(EntityManager em, Entity root)
        {
            var tower = BuildingDefinitions.Find(em, root, "b瞭望塔");
            var tech = TechnologyId.FromIndex(0);
            var buff = BuffId.FromIndex(0);
            var policy = PolicyId.FromIndex(0);
            var originalBuildings = em.GetComponentData<BuildingCatalog>(root);
            var originalTechnologies = em.GetComponentData<TechnologyCatalog>(root);
            var originalBuffs = em.GetComponentData<BuffCatalog>(root);
            var originalPolicies = em.GetComponentData<PolicyCatalog>(root);
            using var grants = em.GetBuffer<OwnedBuff>(root).ToNativeArray(Allocator.Temp);
            using var research = em.GetBuffer<TechnologyProgress>(root).ToNativeArray(Allocator.Temp);
            using var policies = em.GetBuffer<PolicyChoice>(root).ToNativeArray(Allocator.Temp);
            var opinion = em.GetComponentData<PublicOpinionState>(root);
            using var buildingBuilder = new BlobBuilder(Allocator.Temp);
            ref var buildings = ref buildingBuilder.ConstructRoot<BuildingCatalogBlob>();
            var buildingRows = buildingBuilder.Allocate(ref buildings.Definitions, BuildingDefinitions.Count(em, root));
            buildingRows[tower.Index].Capabilities.Defence.Enabled = true;
            var buildingIntel = buildingBuilder.Allocate(ref buildingRows[tower.Index].Capabilities.Defence.Intelligence, 1);
            buildingIntel[0] = new BuildingIntelligenceLevel
            {
                Level = 1,
                Technology = tech,
                Points = 40,
                RequiredWorkers = 2
            };
            using var technologyBuilder = new BlobBuilder(Allocator.Temp);
            ref var technologies = ref technologyBuilder.ConstructRoot<TechnologyCatalogBlob>();
            var techRows = technologyBuilder.Allocate(ref technologies.Definitions, TechnologyDefinitions.Count(em, root));
            var technologyIntel = technologyBuilder.Allocate(ref techRows[tech.Index].Effects.Intelligence, 1);
            technologyIntel[0] = new IntelligenceEffect
            {
                Level = 1,
                Points = 30
            };
            using var buffBuilder = new BlobBuilder(Allocator.Temp);
            ref var buffs = ref buffBuilder.ConstructRoot<BuffCatalogBlob>();
            var buffRows = buffBuilder.Allocate(ref buffs.Definitions, BuffDefinitions.Count(em, root));
            var buffIntel = buffBuilder.Allocate(ref buffRows[buff.Index].Effects.Intelligence, 1);
            buffIntel[0] = new IntelligenceEffect
            {
                Level = 1,
                Points = 40
            };
            using var policyBuilder = new BlobBuilder(Allocator.Temp);
            ref var policyCatalog = ref policyBuilder.ConstructRoot<PolicyCatalogBlob>();
            var policyRows = policyBuilder.Allocate(ref policyCatalog.Definitions, PolicyDefinitions.Count(em, root));
            policyRows[policy.Index].RequiredPublicOpinion = 10;
            var policyIntel = policyBuilder.Allocate(ref policyRows[policy.Index].Effects.Intelligence, 1);
            policyIntel[0] = new IntelligenceEffect
            {
                Points = 10
            };
            using var buildingBlob = buildingBuilder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
            using var technologyBlob = technologyBuilder.CreateBlobAssetReference<TechnologyCatalogBlob>(Allocator.Persistent);
            using var buffBlob = buffBuilder.CreateBlobAssetReference<BuffCatalogBlob>(Allocator.Persistent);
            using var policyBlob = policyBuilder.CreateBlobAssetReference<PolicyCatalogBlob>(Allocator.Persistent);
            try
            {
                em.SetComponentData(root, new BuildingCatalog { Value = buildingBlob });
                em.SetComponentData(root, new TechnologyCatalog { Value = technologyBlob });
                em.SetComponentData(root, new BuffCatalog { Value = buffBlob });
                em.SetComponentData(root, new PolicyCatalog { Value = policyBlob });
                em.GetBuffer<PolicyChoice>(root).Clear();
                em.GetBuffer<OwnedBuff>(root).Clear();
                em.GetBuffer<TechnologyProgress>(root).Clear();
                Check(IntelOps.Current(em, root) == 0 && IntelOps.Sources(em, root).Any(x => x.Reason.Contains("需要科技")), "Source technology prerequisite explains disabled contribution");
                em.GetBuffer<TechnologyProgress>(root).Add(new TechnologyProgress { Technology = tech, Completions = 1 });
                Check(IntelOps.Current(em, root) == 70, "Completed technology contributes and enables its building source");
                PermanentBuffs.Grant(em, root, buff, 1);
                Check(IntelOps.Current(em, root) == 100, "Event Buff plus technology and building clamp completeness at one hundred");
                em.GetBuffer<OwnedBuff>(root).Clear();
                Check(IntelOps.Current(em, root) == 70, "Expired/revoked event Buff no longer contributes");
                var activeOpinion = opinion;
                activeOpinion.Value = 20;
                em.SetComponentData(root, activeOpinion);
                Check(IntelOps.Current(em, root) == 70, "Unselected policy contributes nothing despite sufficient opinion");
                em.GetBuffer<PolicyChoice>(root).Add(new PolicyChoice { Definition = policy });
                Check(IntelOps.Current(em, root) == 80, "Selected active policy contributes via court authority");
                activeOpinion.Value = 0;
                em.SetComponentData(root, activeOpinion);
                Check(IntelOps.Current(em, root) == 70, "Policy opinion failure disables contribution");
            }
            finally
            {
                em.SetComponentData(root, originalBuildings);
                em.SetComponentData(root, originalTechnologies);
                em.SetComponentData(root, originalBuffs);
                em.SetComponentData(root, originalPolicies);
                em.GetBuffer<OwnedBuff>(root).CopyFrom(grants);
                em.GetBuffer<TechnologyProgress>(root).CopyFrom(research);
                em.GetBuffer<PolicyChoice>(root).CopyFrom(policies);
                em.SetComponentData(root, opinion);
            }
        }

        static void RetryKnowledge(EntityManager em, Entity root, CheckpointSystem checkpoint)
        {
            // Create a valid dusk in the same isolated world; no production archive writes.
            var s = em.GetComponentData<Session>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            IntelligenceModeState sIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
            s.Phase = Phase.Deployment;
            sNight.Intelligence = 100;
            sIntelligenceMode.Enabled = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sIntelligenceMode);
            }

            NightPlanOps.Prepare(em, root);
            SimulationEvents.Emit(em, root, EventKind.DuskCheckpoint, "Owned dusk fixture");
            checkpoint.Update();
            IntelOps.Refresh(em, root);
            var old = em.GetComponentData<RecoveryState>(root);
            IntelOps.MarkRead(em, root, old.IntelFingerprint);
            uint seed = sNight.Seed;
            {
                s = em.GetComponentData<Session>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
                sIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
            }

            s.Phase = Phase.GameOver;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sIntelligenceMode);
            }

            checkpoint.Update();
            checkpoint.Retry(root, true);
            var retry = em.GetComponentData<RecoveryState>(root);
            Check(em.GetComponentData<NightRuntimeState>(root).Seed != seed && retry.KnownIntel == 100, "Core recovery rerolls seed while preserving highest known");
            Check(retry.IntelFingerprint != old.IntelFingerprint && retry.IntelFingerprint != retry.IntelReadFingerprint, "Recovery immediately publishes an ordinary unread report");
            var archive = RunArchiveCodec.Decode(RunArchiveCodec.Encode(checkpoint.Export(root)));
            checkpoint.Import(root, archive, false);
            Check(em.GetComponentData<RecoveryState>(root).Equals(retry), "Cold archive import retains retry report and read state");
            Check(!IntelOps.Read(em, root).Lines.Any(l => l.Contains("尝试") || l.Contains("种子") || l.Contains("降难")), "Recovery presentation never labels attempts or assistance");
        }
    }
}
#endif
