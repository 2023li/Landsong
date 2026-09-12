#if UNITY_EDITOR
using System;
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
        static StringBuilder log; static int count;
        static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("FAIL " + name); count++; log.AppendLine("PASS " + name); }
        static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } catch (InvalidDataException) { rejected = true; } Check(rejected, name); }
        [MenuItem("Landsong/ECS/Verification/Intelligence")]
        public static string Run()
        {
            log = new StringBuilder(); count = 0;
            try { Configuration(); Simulation(); log.AppendLine("Assertions: " + count); return log.ToString(); }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/intelligence-verification.txt", log.ToString()); }
        }
        static void Configuration()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            IntelligenceValidation.Validate(catalog); Check(true, "Formal intelligence content validates");
            var copy = UnityEngine.Object.Instantiate(catalog);
            try
            {
                var settings = copy.Settings; var invalid = settings; invalid.LowIntel = invalid.MediumIntel; copy.Settings = invalid;
                Reject(() => IntelligenceValidation.Validate(copy), "Non-increasing intelligence tiers rejected");
                invalid = settings; invalid.HighIntelLead = float.NaN; copy.Settings = invalid;
                Reject(() => IntelligenceValidation.Validate(copy), "NaN forecast lead rejected");
                copy.Settings = settings;
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static void Simulation()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");
            using var blobs = new BlobAssetStore(128); using var world = new World("Wave twelve isolated intelligence", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var cfg = em.GetComponentData<GameSettings>(root); cfg.FirstInvasion = 1; cfg.InvasionChance = 1; cfg.FirstBoss = 99999; em.SetComponentData(root, cfg);
                var session = em.GetComponentData<Session>(root); session.Turn = 8; session.Phase = Phase.Day; em.SetComponentData(root, session);
                Sim.Set(em, root, new NightPlanState { BossDefinition = -1 }); NightOps.Plan(em, root, false);
                var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>(); checkpoint.Update();
                var original = SnapshotCodec.Capture(em, root);
                int Def(string value) => Sim.FindDefinition(em, root, new FixedString128Bytes(value));
                void Level(int value)
                {
                    var s = em.GetComponentData<Session>(root); s.Phase = Phase.Day; s.IntelligenceMode = 0; em.SetComponentData(root, s);
                    Sim.Set(em, root, new RecoveryState { Turn = s.Turn, KnownIntel = value });
                }
                var unknown = IntelOps.Read(em, root); Check(unknown.Tier == 0 && unknown.Areas.Count == 0 && unknown.Lines.Count == 1, "Zero intelligence reveals no night classification or region");
                Level(cfg.LowIntel); var low = IntelOps.Read(em, root);
                Check(low.Tier == 1 && low.Areas.Count == 0 && low.WaveChoices == 0 && low.Lines.Count == 2, "Low tier exposes only classification and fixed threat");
                var first = em.GetBuffer<NightWave>(root)[0]; var s0 = em.GetComponentData<Session>(root); var p0 = NightPlanOps.State(em, root);
                IntelOps.Refresh(em, root); var fingerprint = em.GetComponentData<RecoveryState>(root).IntelFingerprint;
                var changed = first; changed.Count = math.min(256, first.Count + 4); changed.Direction = first.Direction == 10 ? 20 : 10; NightPlanOps.SetWave(em, root, 0, changed);
                IntelOps.Refresh(em, root);
                Check(em.GetComponentData<RecoveryState>(root).IntelFingerprint == fingerprint, "Hidden wave counts/directions cannot produce low-tier unread signals");
                NightPlanOps.SetWave(em, root, 0, first);
                first.Count = 35; NightPlanOps.SetWave(em, root, 0, first);
                Level(cfg.MediumIntel); IntelOps.Refresh(em, root); var medium = IntelOps.Read(em, root);
                Check(medium.Tier == 2 && medium.Lines.Any(l => l.Contains("约")) && medium.Areas.Any(a => !a.Target), "Medium tier supplies types ranges and legal approximate regions");
                foreach (var wave in em.GetBuffer<NightWave>(root))
                {
                    var range = IntelOps.CountRange(wave.Count); Check(range.x <= wave.Count && range.y >= wave.Count && range.x < range.y, "Medium numerical interval includes actual count");
                    var highArea = IntelOps.SpawnArea(em, root, wave, 3); var mediumArea = IntelOps.SpawnArea(em, root, wave, 2);
                    Check(highArea.Size.x <= mediumArea.Size.x + .01f && highArea.Size.z <= mediumArea.Size.z + .01f, "High area no wider than medium");
                    for (int i = 0; i < wave.Count; i++)
                    {
                        var preferred = wave.Position + new float3((i % 5 - 2) * .6f, 0, i / 5 * .6f);
                        if (NightSpatialOps.SpawnPoint(em, root, wave.Region, preferred, out var point)) Check(highArea.Contains(point) && mediumArea.Contains(point), "Both region tiers contain each actual deployment point");
                    }
                    var r = em.GetBuffer<SpawnRegion>(root)[wave.Region]; Check(NightSpatialOps.Inside(r, highArea.Center - highArea.Size * .5f) && NightSpatialOps.Inside(r, highArea.Center + highArea.Size * .5f), "High region stays inside authored invasion area");
                }
                Level(100); IntelOps.Refresh(em, root); var high = IntelOps.Read(em, root);
                Check(high.Tier == 3 && high.Lines.Any(l => l.Contains(" × ")) && !high.Lines.Any(l => l.Contains("约")), "High tier aggregates exact counts by type");
                using (var groupedWaves = em.GetBuffer<NightWave>(root).ToNativeArray(Allocator.Temp)) Check(high.Lines.Count(l => l.Contains(" × ")) == groupedWaves.ToArray().Select(w => w.Definition).Distinct().Count(), "High counts are grouped by type not leaked wave rows");
                Check(em.GetComponentData<Session>(root).NightSeed == s0.NightSeed && em.GetComponentData<Session>(root).Threat == s0.Threat && NightPlanOps.State(em, root).BaseThreat == p0.BaseThreat, "Viewing intelligence never consumes random state or recalculates budget");
                var before = em.GetBuffer<NightWave>(root).ToNativeArray(Allocator.Temp);
                BuildingOps.Changed(em, root); var after = em.GetBuffer<NightWave>(root);
                for (int i = 0; i < before.Length; i++) Check(before[i].Count == after[i].Count && before[i].Definition == after[i].Definition && before[i].At == after[i].At, "Layout reprojection preserves locked composition and schedule");
                before.Dispose();
                var recovery = em.GetComponentData<RecoveryState>(root); IntelOps.MarkRead(em, root, recovery.IntelFingerprint + 1); Check(IntelOps.Read(em, root).Unread, "Stale read token cannot clear new information");
                IntelOps.MarkRead(em, root, recovery.IntelFingerprint); Check(!IntelOps.Read(em, root).Unread, "Exact read token clears ordinary unread badge");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); Check(IntelOps.Known(em, root) == 100, "Rewinding day does not erase acquired knowledge");
                // Operational source failure combinations, without inventing a second rule evaluator.
                var tower = BuildingOps.Create(em, root, Def("b瞭望塔"), new int2(-1200, -1200), 0, 1, true);
                var building = em.GetComponentData<Building>(tower); building.Workers = 2; em.SetComponentData(tower, building);
                var current = IntelOps.Current(em, root); Check(current > 0, "Active source contributes points");
                SourceConditions(em, root);
                building.Workers = 1; em.SetComponentData(tower, building); Check(IntelOps.Current(em, root) == 0 && IntelOps.Sources(em, root).Any(x => x.Reason.Contains("工人不足")), "Understaffed source explains required worker count");
                building.Workers = 2; em.SetComponentData(tower, building);
                building.Maintained = 0; em.SetComponentData(tower, building); Check(IntelOps.Current(em, root) == 0 && IntelOps.Sources(em, root).Any(x => x.Reason.Contains("维护")), "Unmaintained source explains why contribution is zero");
                building.Maintained = 1; building.Stage = LifeStage.Ruined; em.SetComponentData(tower, building); Check(IntelOps.Current(em, root) == 0, "Ruined source has no current function");
                Check(IntelOps.Known(em, root) == 100, "Source ruin does not erase highest knowledge");
                var s = em.GetComponentData<Session>(root); s.IntelAtNight = 100; s.Phase = Phase.Night; s.Paused = 0; em.SetComponentData(root, s);
                var plan = NightPlanOps.State(em, root); plan.ClockStarted = 1; plan.AnySpawned = 1; plan.CombatElapsed = 0; Sim.Set(em, root, plan);
                var night = IntelOps.Read(em, root);
                Check(night.Lines.Any(l => l.Contains("方向")) && !night.Lines.Any(l => l.Contains(" × ") || l.Contains("约") || l.Contains("秒")), "Real-time high tier reveals direction only");
                var scheduled = em.GetBuffer<NightWave>(root).ToNativeArray(Allocator.Temp);
                var leadWave = scheduled[0]; leadWave.At = 20f / s.NightDuration; leadWave.Spawned = 0; leadWave.SpatiallyBlocked = 0;
                em.GetBuffer<NightWave>(root).Clear(); em.GetBuffer<NightWave>(root).Add(leadWave);
                s.IntelAtNight = cfg.MediumIntel; em.SetComponentData(root, s); Sim.Set(em, root, new RecoveryState { Turn = s.Turn, KnownIntel = cfg.MediumIntel });
                Check(IntelOps.Read(em, root).WaveChoices == 0, "Medium forecast does not name directions before its lead window");
                s.IntelAtNight = 100; em.SetComponentData(root, s);
                Check(IntelOps.Read(em, root).WaveChoices == 1, "High tier receives a longer lead window");
                var simultaneous = leadWave; simultaneous.Direction = leadWave.Direction == 10 ? 20 : 10; em.GetBuffer<NightWave>(root).Add(simultaneous);
                Check(IntelOps.Read(em, root).WaveChoices == 1 && IntelOps.Read(em, root).Lines.Any(l => l.Contains("、")), "Simultaneous multi-region arrivals share one directional group");
                var later = leadWave; later.At = 23f / s.NightDuration; em.GetBuffer<NightWave>(root).Add(later);
                Check(IntelOps.Read(em, root, 1).SelectedWave == 1 && IntelOps.Read(em, root, 1).Areas.Any(a => a.Secondary), "Switching known wave fades other entrances");
                for (int i = 0; i < em.GetBuffer<NightWave>(root).Length; i++) { var w = em.GetBuffer<NightWave>(root)[i]; w.Spawned = 1; NightPlanOps.SetWave(em, root, i, w); }
                var enemy = Sim.Spawn(em, root, leadWave.Definition, leadWave.Position, false);
                var core = Sim.Find(em, leadWave.Target); Check(core != Entity.Null, "Dynamic target fixture has reachable core");
                MilitaryOps.ConfigureCombatant(em, root, enemy, 1, false, true, leadWave.Target, leadWave.Position);
                Check(IntelOps.Read(em, root).Areas.Any(a => a.Target && a.Contains(Sim.Position(em, core))), "Live actor target controls red region after wave has spawned");
                var actor = em.GetComponentData<Combatant>(enemy); actor.HomeId = 0; em.SetComponentData(enemy, actor);
                Check(!IntelOps.Read(em, root).Areas.Any(a => a.Target), "Lost live target removes stale NightWave target region"); em.DestroyEntity(enemy);
                em.GetBuffer<NightWave>(root).Clear(); em.GetBuffer<NightWave>(root).AddRange(scheduled); scheduled.Dispose();
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.IntelligenceMode, Argument = 1 }) == ResultCode.Success && em.GetComponentData<Session>(root).Paused == 0, "Entering intelligence does not pause combat");
                foreach (var kind in new[] { CommandKind.Build, CommandKind.MoveBuilding, CommandKind.WakeHero, CommandKind.MoveHero, CommandKind.Bell, CommandKind.PickUp, CommandKind.Advance, CommandKind.NightSpeed })
                    Check(GameLoopSystem.Execute(em, root, new Command { Kind = kind }) == ResultCode.Busy, "Authority blocks gameplay during intelligence: " + kind);
                var beforeCamera = SnapshotCodec.Capture(em, root);
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.CameraZoomed }) == ResultCode.Success, "Camera commands remain allowed");
                Check(beforeCamera.SequenceEqual(SnapshotCodec.Capture(em, root)), "Intelligence camera motion does not mutate tutorial progress or gameplay");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.IntelligenceMode, Argument = 0 }) == ResultCode.Success, "Exit clears intelligence command gate");
                s = em.GetComponentData<Session>(root); s.Phase = Phase.Day; em.SetComponentData(root, s);
                var bytes = SnapshotCodec.Capture(em, root); var data = SnapshotCodec.Decode(em, root, bytes); data.Session.IntelAtNight = 101;
                Reject(() => SnapshotCodec.Restore(em, root, data), "Out of range checkpoint intelligence rejected");
                var live = SnapshotCodec.Capture(em, root); var originalRecovery = em.GetComponentData<RecoveryState>(root);
                Reject(() => SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original), probe: step => { if (step == "root-published") throw new InvalidOperationException("Owned intel rollback probe"); }), "Publication fault rolls back intelligence state");
                Check(originalRecovery.Equals(em.GetComponentData<RecoveryState>(root)) && live.SequenceEqual(SnapshotCodec.Capture(em, root)), "Knowledge/read state and world survive failed restore intact");
                var archive = checkpoint.Export(root); archive.Recovery = originalRecovery; var decoded = RunArchiveCodec.Decode(RunArchiveCodec.Encode(archive));
                Check(decoded.Recovery.Equals(originalRecovery), "Archive preserves highest known and read fingerprints");
                RetryKnowledge(em, root, checkpoint);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void SourceConditions(EntityManager em, Entity root)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            var copy = CatalogFixture.Clone(catalog);
            int tower = copy.Find("b瞭望塔"), tech = Array.FindIndex(copy.Definitions, d => d.Data.Kind == ContentKind.Technology), buff = Array.FindIndex(copy.Definitions, d => d.Data.Kind == ContentKind.Buff), policy = Array.FindIndex(copy.Definitions, d => d.Data.Kind == ContentKind.Policy);
            var original = em.GetComponentData<ContentCatalog>(root); using var grants = em.GetBuffer<Entitlement>(root).ToNativeArray(Allocator.Temp);
            using var research = em.GetBuffer<ResearchEntry>(root).ToNativeArray(Allocator.Temp);
            using var policies = em.GetBuffer<PolicyChoice>(root).ToNativeArray(Allocator.Temp); var originalSession = em.GetComponentData<Session>(root);
            BlobAssetReference<ContentBlob> blob = default;
            try
            {
                copy.Definitions[tower].Data.Modules.Defence.Intelligence[0].Technology = copy.Definitions[tech];
                copy.Definitions[tech].Data.Configuration.Modifiers.Enabled=true; copy.Definitions[tech].Data.Configuration.Modifiers.Intelligence=new[]{new PassiveIntelligence{Level=1,Points=30}};
                copy.Definitions[buff].Data.Configuration.Modifiers.Enabled=true; copy.Definitions[buff].Data.Configuration.Modifiers.Intelligence=new[]{new PassiveIntelligence{Level=1,Points=40}};
                copy.Definitions[policy].Data.Configuration.Modifiers = new ModifiersContentModule{Enabled=true,Intelligence=new[]{new PassiveIntelligence{Points=10}}}; copy.Definitions[policy].Data.Cost = 10;
                blob = GameWorldAuthoring.BuildCatalog(copy); em.SetComponentData(root, new ContentCatalog { Value = blob });
                em.GetBuffer<PolicyChoice>(root).Clear();
                var g = em.GetBuffer<Entitlement>(root); for (int i = g.Length - 1; i >= 0; i--) if (g[i].Definition == tech || g[i].Definition == buff) g.RemoveAt(i);
                var entries = em.GetBuffer<ResearchEntry>(root); for (int i = entries.Length - 1; i >= 0; i--) if (entries[i].Definition == tech) entries.RemoveAt(i);
                Check(IntelOps.Current(em, root) == 0 && IntelOps.Sources(em, root).Any(x => x.Reason.Contains("需要科技")), "Source technology prerequisite explains disabled contribution");
                entries.Add(new ResearchEntry { Definition = tech, Completions = 1 });
                g.Add(new Entitlement { Definition = tech, Level = 1 });
                Check(IntelOps.Current(em, root) == 70, "Completed technology contributes and enables its building source");
                PermanentBuffOps.Grant(em, root, buff); Check(IntelOps.Current(em, root) == 100, "Event Buff plus technology and building clamp completeness at one hundred");
                g = em.GetBuffer<Entitlement>(root); for (int i = g.Length - 1; i >= 0; i--) if (g[i].Definition == buff) g.RemoveAt(i);
                Check(IntelOps.Current(em, root) == 70, "Expired/revoked event Buff no longer contributes");
                var s = originalSession; s.PublicOpinion = 20; em.SetComponentData(root, s);
                Check(IntelOps.Current(em, root) == 70, "Unselected policy contributes nothing despite sufficient opinion");
                em.GetBuffer<PolicyChoice>(root).Add(new PolicyChoice { Definition = policy }); Check(IntelOps.Current(em, root) == 80, "Selected active policy contributes via court authority");
                s.PublicOpinion = 0; em.SetComponentData(root, s); Check(IntelOps.Current(em, root) == 70, "Policy opinion failure disables contribution");
            }
            finally
            {
                em.SetComponentData(root, original); em.GetBuffer<Entitlement>(root).Clear(); em.GetBuffer<Entitlement>(root).AddRange(grants);
                em.GetBuffer<ResearchEntry>(root).Clear(); em.GetBuffer<ResearchEntry>(root).AddRange(research);
                em.GetBuffer<PolicyChoice>(root).Clear(); em.GetBuffer<PolicyChoice>(root).AddRange(policies); em.SetComponentData(root, originalSession);
                if (blob.IsCreated) blob.Dispose(); CatalogFixture.Destroy(copy);
            }
        }
        static void RetryKnowledge(EntityManager em, Entity root, CheckpointSystem checkpoint)
        {
            // Create a valid dusk in the same isolated world; no production archive writes.
            var s = em.GetComponentData<Session>(root); s.Phase = Phase.Deployment; s.IntelAtNight = 100; s.IntelligenceMode = 0; em.SetComponentData(root, s);
            NightPlanOps.Prepare(em, root); Sim.Emit(em, root, EventKind.DuskCheckpoint, "Owned dusk fixture"); checkpoint.Update();
            IntelOps.Refresh(em, root); var old = em.GetComponentData<RecoveryState>(root); IntelOps.MarkRead(em, root, old.IntelFingerprint);
            uint seed = s.NightSeed; s = em.GetComponentData<Session>(root); s.Phase = Phase.GameOver; em.SetComponentData(root, s); checkpoint.Update();
            checkpoint.Retry(root, true);
            var retry = em.GetComponentData<RecoveryState>(root);
            Check(em.GetComponentData<Session>(root).NightSeed != seed && retry.KnownIntel == 100, "Core recovery rerolls seed while preserving highest known");
            Check(retry.IntelFingerprint != old.IntelFingerprint && retry.IntelFingerprint != retry.IntelReadFingerprint, "Recovery immediately publishes an ordinary unread report");
            var archive = RunArchiveCodec.Decode(RunArchiveCodec.Encode(checkpoint.Export(root))); checkpoint.Import(root, archive, false);
            Check(em.GetComponentData<RecoveryState>(root).Equals(retry), "Cold archive import retains retry report and read state");
            Check(!IntelOps.Read(em, root).Lines.Any(l => l.Contains("尝试") || l.Contains("种子") || l.Contains("降难")), "Recovery presentation never labels attempts or assistance");
        }
    }
}
#endif
