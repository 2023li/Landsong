#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class TransactionVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("FAIL " + name); checks++; log.AppendLine("PASS " + name); }
        [MenuItem("Landsong/ECS/Verification/Transaction")]
        public static string Run()
        {
            log = new StringBuilder(); checks = 0;
            try
            {
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes/EntityMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) VerifyMap(path);
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/transactions-verification.txt", log.ToString()); }
        }
        static T[] Buffer<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData
        { if (!em.HasBuffer<T>(root)) return Array.Empty<T>(); using var array = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); return array.ToArray(); }
        static Entity Core(EntityManager em)
        { using var buildings = Sim.Entities<Building>(em); foreach (var e in buildings) if (em.GetComponentData<BuildingStats>(e).IsCore != 0) return e; throw new InvalidOperationException("No core"); }
        static Entity[] Entities(EntityManager em)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc { Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab });
            using var all = query.ToEntityArray(Allocator.Temp); return all.ToArray().OrderBy(e => e.Index).ThenBy(e => e.Version).ToArray();
        }
        static void VerifyMap(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var blobs = new BlobAssetStore(128); using var world = new World("Wave two isolated verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root); InvitationExpeditionVerification.FixturePermissions(em, root);
                var initial = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                Rollback(em, root, initial); SnapshotCodec.Restore(em, root, initial);
                // Rollback deliberately injects a pending-loss ticket. Node restore now correctly
                // retains it; start the independent entry/import fixtures with a fresh non-pending run.
                Sim.Set(em, root, new RecoveryState { Turn = em.GetComponentData<Session>(root).Turn });
                Entry(em, root, initial); SnapshotCodec.Restore(em, root, initial);
                ImportFailure(world, root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void Rollback(EntityManager em, Entity root, SnapshotCodec.Snapshot initial)
        {
            var core = Core(em); var identity = em.GetComponentData<Identity>(core);
            var health = em.GetComponentData<Health>(core); health.Current *= .5f; em.SetComponentData(core, health);
            var missile = Sim.Spawn(em, root, Sim.FirstDefinition(em, root, ContentKind.Projectile), Sim.Position(em, core), false);
            var projectile = new Projectile { Source = core, Target = core, Damage = 13, Lifetime = 2, Speed = 5 }; Sim.Set(em, missile, projectile);
            var state = em.GetComponentData<Session>(root); state.Phase = Phase.GameOver; state.Paused = 1; state.SelectedHero = core; state.ActiveBell = identity.Id; em.SetComponentData(root, state);
            Sim.Set(em, root, new RecoveryState { Turn = state.Turn, LossCount = 2, Seed = 123, AwaitingDecision = 1 });
            Sim.Set(em, root, new RunPersistence { RunId = Guid.NewGuid().ToString("N") });
            NightResultOps.Reset(em, root); NightResultOps.Record(em, root, identity.Id, 0, RuleKind.RewardItem, em.GetComponentData<GameSettings>(root).Gold, 7);
            em.GetBuffer<Command>(root).Add(new Command { Kind = CommandKind.RetryDusk, RequestId = 123 });
            em.GetBuffer<DamageRequest>(root).Add(new DamageRequest { Source = missile, Target = core, Amount = 8 });
            Sim.Emit(em, root, EventKind.Message, "未提交前的战场");
            var before = SnapshotCodec.Capture(em, root); var entities = Entities(em);
            var events = Buffer<GameEvent>(em, root); var commands = Buffer<Command>(em, root); var damage = Buffer<DamageRequest>(em, root);
            var rewards = Buffer<NightReward>(em, root); var grid = em.GetComponentData<GridData>(root); var occupied = Buffer<Occupancy>(em, root);
            var recovery = em.GetComponentData<RecoveryState>(root); var io = em.GetComponentData<RunPersistence>(root);
            foreach (var failure in new[] { "root-reset", "record-created", "garrisons-prepared", "prepared", "root-published", "before-retire" })
            {
                var rejected = false; var created = 0;
                try { SnapshotCodec.Restore(em, root, initial, probe: step => { if (step == "record-created") created++; if (step == failure && (failure != "record-created" || created >= 2)) throw new IOException("Injected " + step); }); }
                catch (IOException) { rejected = true; }
                Check(rejected, failure + " fault reached");
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)) && state.Equals(em.GetComponentData<Session>(root)), failure + " preserves exact live session and persistent state");
                Check(entities.SequenceEqual(Entities(em)) && Sim.Root(em) == root && Sim.Find(em, identity.Id) == core, failure + " retains original Entity handles, no leaked staging or visual children");
                Check(em.GetComponentData<Projectile>(missile).Equals(projectile) && em.GetComponentData<Health>(core).Equals(health), failure + " retains in-flight projectile references and damaged core");
                Check(events.SequenceEqual(Buffer<GameEvent>(em, root)) && commands.SequenceEqual(Buffer<Command>(em, root)) && damage.SequenceEqual(Buffer<DamageRequest>(em, root)), failure + " preserves messages and command/damage queues");
                Check(rewards.SequenceEqual(Buffer<NightReward>(em, root)) && grid.Equals(em.GetComponentData<GridData>(root)) && occupied.SequenceEqual(Buffer<Occupancy>(em, root)), failure + " preserves reward journal and navigation revision/occupancy");
                Check(recovery.Equals(em.GetComponentData<RecoveryState>(root)) && io.Equals(em.GetComponentData<RunPersistence>(root)), failure + " preserves recovery and save ownership");
            }
            SnapshotCodec.Restore(em, root, initial);
            Check(!em.Exists(core) && !em.Exists(missile) && Sim.Root(em) == root, "Successful restore retires old runtime only, baked root stays stable");
            Check(Sim.Find(em, identity.Id) != Entity.Null && em.GetComponentData<Session>(root).Phase == Phase.Day, "Successful restore publishes usable rebuilt entities");
        }
        static void Entry(EntityManager em, Entity root, SnapshotCodec.Snapshot initial)
        {
            var core = Core(em); var id = em.GetComponentData<Identity>(core).Id; var gold = em.GetComponentData<GameSettings>(root).Gold;
            var slots = em.GetBuffer<InventorySlot>(root); var full = math.max(1, Sim.Definition(em, root, gold).Capacity);
            for (var i = 0; i < slots.Length; i++) { var slot = slots[i]; slot.Item = gold; slot.Count = full; slots[i] = slot; }
            // A provisioned slot no longer supported by the provider is reconciled during settlement.
            slots.Add(new InventorySlot { Provider = id, Index = 100000, Item = gold, Count = full, SlotType = slots[0].SlotType });
            var soldier = Sim.Spawn(em, root, Sim.FirstDefinition(em, root, ContentKind.Soldier), Sim.Position(em, core), true);
            MilitaryOps.ConfigureCombatant(em, root, soldier, 0, false, false, id, Sim.Position(em, core));
            Sim.Set(em, soldier, new Soldier { Garrison = id, Slot = em.GetComponentData<BuildingStats>(core).Garrison + 1, PopulationCost = 1 }); // Unsupported slot: settlement must preview eviction regardless of the core capacity.
            Check(em.GetComponentData<Soldier>(soldier).Slot > em.GetComponentData<BuildingStats>(core).Garrison && MilitaryOps.UnassignedCount(em) == 0 && Buffer<PendingItem>(em, root).Length == 0, "Entry fixture has no pre-existing unassigned/pending pool");
            var before = SnapshotCodec.Capture(em, root); var entities = Entities(em); var state = em.GetComponentData<Session>(root);
            Check(NightOps.Begin(em, root) == ResultCode.ConfirmationRequired, "Settlement-created overflow and invalid garrison trigger confirmation");
            var losses = Buffer<NightEntryLoss>(em, root);
            Check(losses.Any(l => l.Soldier == em.GetComponentData<Identity>(soldier).Id) && losses.Any(l => l.Soldier == 0 && l.Item == gold && l.Amount > 0), "Review lists actual post-settlement items and individual soldier IDs");
            Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)) && state.Equals(em.GetComponentData<Session>(root)), "Preview/cancel does not settle, mutate RNG, charge, despawn or advance");
            Check(NightOps.Begin(em, root, true) == ResultCode.ConfirmationRequired, "Blind confirmed flag cannot bypass reviewed consent");
            var token = em.GetComponentData<NightEntryReview>(root).Token;
            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 3 });
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Advance, Argument = 1, Other = token }) == ResultCode.ConfirmationRequired, "Stale UI command cannot discard a changed day");
            Check(em.GetComponentData<NightEntryReview>(root).Token != token && em.Exists(soldier), "Stale confirmation refreshes review and preserves roster");
            token = em.GetComponentData<NightEntryReview>(root).Token; var reviewed = Buffer<NightEntryLoss>(em, root);
            before = SnapshotCodec.Capture(em, root); entities = Entities(em);
            foreach (var failure in new[] { "day-settled", "night-prepared", "root-published", "before-retire" })
            {
                var rejected = false;
                try { NightEntryOps.Begin(em, root, true, token, step => { if (step == failure) throw new IOException("Injected " + step); }); }
                catch (IOException) { rejected = true; }
                Check(rejected && before.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)), "Entry " + failure + " rolls back costs, roster, all entities");
                Check(reviewed.SequenceEqual(Buffer<NightEntryLoss>(em, root)) && em.GetComponentData<NightEntryReview>(root).Token == token, "Entry " + failure + " keeps original reviewed consent");
            }
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Advance, Argument = 1, Other = token }) == ResultCode.Success, "Exact reviewed loss set enters night through command validation");
            var dusk = em.GetComponentData<Session>(root);
            Check(dusk.Phase == Phase.Deployment && dusk.Turn == state.Turn && dusk.LastSettledTurn == state.Turn, "Settlement commits once without advancing turn at dusk");
            Check(Buffer<PendingItem>(em, root).Length == 0 && MilitaryOps.UnassignedCount(em) == 0 && !em.Exists(soldier), "Only confirmed post-settlement pools are cleared before deployment");
            Check(Buffer<GameEvent>(em, root).Count(e => e.Kind == EventKind.DuskCheckpoint) == 1 && em.GetComponentData<NightEntryReview>(root).Token == 0, "Single dusk request and consent consumed");
            var bytes = SnapshotCodec.Capture(em, root);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
            Check(em.GetComponentData<Session>(root).LastSettledTurn == state.Turn && Buffer<PendingItem>(em, root).Length == 0, "Dusk restore does not repeat settlement or recover dismissed pools");
            SnapshotCodec.Restore(em, root, initial);
            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 1 });
            NightOps.Begin(em, root); token = em.GetComponentData<NightEntryReview>(root).Token;
            GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.StorePending });
            Check(NightOps.Begin(em, root) == ResultCode.Success, "Sorting pending inventory can remove need for confirmation entirely");
        }
        static void ImportFailure(World world, Entity root)
        {
            var em = world.EntityManager; var settings = em.GetComponentData<GameSettings>(root);
            settings.FirstInvasion = 1; settings.FirstBoss = 99999; settings.InvasionChance = 1; em.SetComponentData(root, settings); Sim.Set(em, root, new NightPlanState { BossDefinition = -1 }); NightOps.Plan(em, root, false);
            var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>(); var archive = checkpoint.Export(root);
            var applied = archive.Copy(); applied.Recovery.LossCount = 1; applied.Recovery.Seed = 987;
            var session = em.GetComponentData<Session>(root); session.RetryCount = 1; em.SetComponentData(root, session);
            var waves = em.GetBuffer<NightWave>(root); var wave = waves[0]; wave.Position += new float3(3, 0, 4); waves[0] = wave;
            applied.Current = applied.Day = SnapshotCodec.Capture(em, root);
            var locked = Buffer<NightWave>(em, root);
            checkpoint.Import(root, applied, false);
            Check(locked.SequenceEqual(Buffer<NightWave>(em, root)), "Continuing an already recovered node preserves its exact locked intelligence plan");
            checkpoint.Import(root, archive, false);
            var data = archive.Copy(); data.RunId = Guid.NewGuid().ToString("N"); data.Recovery.LossCount = 1; data.Recovery.Seed = 987;
            // Region references are now preflight-validated; inject a true post-publication fault
            // rather than using an invalid region table to simulate a late import failure.
            var before = SnapshotCodec.Capture(em, root); var entities = Entities(em); var oldArchive = RunArchiveCodec.Encode(checkpoint.Export(root));
            var rejected = false;
            try { checkpoint.Import(root, data, false, step => { if (step == "root-published") throw new InvalidOperationException("Owned late import fault"); }); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Import fails during recovery-plan preparation, not merely decode preflight");
            Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)), "Late import failure leaves original active world intact");
            Check(oldArchive.SequenceEqual(RunArchiveCodec.Encode(checkpoint.Export(root))), "Late import failure cannot switch archive ownership or recovery metadata");
        }
    }
}
#endif
