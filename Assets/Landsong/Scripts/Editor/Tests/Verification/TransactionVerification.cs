#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
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
        static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException("FAIL " + name);
            checks++;
            log.AppendLine("PASS " + name);
        }

        [MenuItem("Landsong/ECS/Verification/Transaction")]
        public static string Run()
        {
            log = new StringBuilder();
            checks = 0;
            try
            {
                foreach (var path in Landsong.EditorTools.GameMapPaths.BakedScenes())
                    VerifyMap(path);
                log.AppendLine("Assertions: " + checks);
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
                File.WriteAllText("Library/LandsongEcs/transactions-verification.txt", log.ToString());
            }
        }

        static T[] Buffer<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(root))
                return Array.Empty<T>();
            using var array = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return array.ToArray();
        }

        static Entity Core(EntityManager em)
        {
            using var buildings = WorldQueries.Entities<Building>(em);
            foreach (var e in buildings)
                if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                    return e;
            throw new InvalidOperationException("No core");
        }

        static Entity[] Entities(EntityManager em)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc { Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab });
            using var all = query.ToEntityArray(Allocator.Temp);
            return all.ToArray().OrderBy(e => e.Index).ThenBy(e => e.Version).ToArray();
        }

        static void VerifyMap(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Wave two isolated verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                InvitationExpeditionVerification.FixturePermissions(em, root);
                var initial = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                Rollback(em, root, initial);
                SnapshotCodec.Restore(em, root, initial);
                // Rollback deliberately injects a pending-loss ticket. Node restore now correctly
                // retains it; start the independent entry/import fixtures with a fresh non-pending run.
                EntityState.Set(em, root, new RecoveryState { Turn = em.GetComponentData<GameClock>(root).Turn });
                Entry(em, root, initial);
                SnapshotCodec.Restore(em, root, initial);
                ImportFailure(world, root);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Rollback(EntityManager em, Entity root, SnapshotCodec.Snapshot initial)
        {
            var core = Core(em);
            var identity = em.GetComponentData<Identity>(core);
            var health = em.GetComponentData<Health>(core);
            health.Current *= .5f;
            em.SetComponentData(core, health);
            var missile = ProjectileEntities.Spawn(em, root, ProjectileId.FromIndex(0), EntityState.Position(em, core), false);
            var projectile = new Projectile
            {
                Source = core,
                Target = core,
                Damage = 13,
                Lifetime = 2,
                Speed = 5
            };
            EntityState.Set(em, missile, projectile);
            var state = em.GetComponentData<Session>(root);
            GameClock stateClock = em.GetComponentData<GameClock>(root);
            SimulationControl stateControl = em.GetComponentData<SimulationControl>(root);
            HeroSelection stateHeroSelection = em.GetComponentData<HeroSelection>(root);
            BellState stateBell = em.GetComponentData<BellState>(root);
            state.Phase = Phase.GameOver;
            stateControl.Paused = 1;
            stateHeroSelection.SelectedHero = core;
            stateBell.ActiveBell = identity.Id;
            {
                em.SetComponentData(root, state);
                em.SetComponentData(root, stateClock);
                em.SetComponentData(root, stateControl);
                em.SetComponentData(root, stateHeroSelection);
                em.SetComponentData(root, stateBell);
            }

            EntityState.Set(em, root, new RecoveryState { Turn = stateClock.Turn, LossCount = 2, Seed = 123, AwaitingDecision = 1 });
            EntityState.Set(em, root, new RunPersistence { RunId = Guid.NewGuid().ToString("N") });
            NightResultOps.Reset(em, root);
            NightResultOps.RecordItem(em, root, identity.Id, 0, em.GetComponentData<CurrencySettings>(root).Gold, 7);
            GameplayRequests.Enqueue(em, root, new RetryDuskRequest(), 123);
            em.GetBuffer<DamageRequest>(root).Add(new DamageRequest { Source = missile, Target = core, Amount = 8 });
            SimulationEvents.Emit(em, root, EventKind.Message, "未提交前的战场");
            var before = SnapshotCodec.Capture(em, root);
            var entities = Entities(em);
            var events = Buffer<GameEvent>(em, root);
            var commands = Buffer<QueuedGameplayRequest>(em, root);
            var damage = Buffer<DamageRequest>(em, root);
            var rewards = Buffer<NightItemReward>(em, root);
            var grid = em.GetComponentData<GridData>(root);
            var occupied = Buffer<Occupancy>(em, root);
            var recovery = em.GetComponentData<RecoveryState>(root);
            var io = em.GetComponentData<RunPersistence>(root);
            foreach (var failure in new[]
            {
                "root-reset",
                "record-created",
                "garrisons-prepared",
                "prepared",
                "root-published",
                "before-retire"
            }

            )
            {
                var rejected = false;
                var created = 0;
                try
                {
                    SnapshotCodec.Restore(em, root, initial, probe: step =>
                    {
                        if (step == "record-created")
                            created++;
                        if (step == failure && (failure != "record-created" || created >= 2))
                            throw new IOException("Injected " + step);
                    });
                }
                catch (IOException)
                {
                    rejected = true;
                }

                Check(rejected, failure + " fault reached");
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)) && state.Equals(em.GetComponentData<Session>(root)), failure + " preserves exact live session and persistent state");
                Check(entities.SequenceEqual(Entities(em)) && WorldQueries.Root(em) == root && WorldQueries.Find(em, identity.Id) == core, failure + " retains original Entity handles, no leaked staging or visual children");
                Check(em.GetComponentData<Projectile>(missile).Equals(projectile) && em.GetComponentData<Health>(core).Equals(health), failure + " retains in-flight projectile references and damaged core");
                Check(events.SequenceEqual(Buffer<GameEvent>(em, root)) && commands.SequenceEqual(Buffer<QueuedGameplayRequest>(em, root)) && damage.SequenceEqual(Buffer<DamageRequest>(em, root)), failure + " preserves messages and command/damage queues");
                Check(rewards.SequenceEqual(Buffer<NightItemReward>(em, root)) && grid.Equals(em.GetComponentData<GridData>(root)) && occupied.SequenceEqual(Buffer<Occupancy>(em, root)), failure + " preserves reward journal and navigation revision/occupancy");
                Check(recovery.Equals(em.GetComponentData<RecoveryState>(root)) && io.Equals(em.GetComponentData<RunPersistence>(root)), failure + " preserves recovery and save ownership");
            }

            SnapshotCodec.Restore(em, root, initial);
            Check(!em.Exists(core) && !em.Exists(missile) && WorldQueries.Root(em) == root, "Successful restore retires old runtime only, baked root stays stable");
            Check(WorldQueries.Find(em, identity.Id) != Entity.Null && em.GetComponentData<Session>(root).Phase == Phase.Day, "Successful restore publishes usable rebuilt entities");
        }

        static void Entry(EntityManager em, Entity root, SnapshotCodec.Snapshot initial)
        {
            var core = Core(em);
            var id = em.GetComponentData<Identity>(core).Id;
            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            var slots = em.GetBuffer<InventorySlot>(root);
            var full = math.max(1, ItemDefinitions.Get(em, root, gold).MaximumStack);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                slot.Item = gold;
                slot.Count = full;
                slots[i] = slot;
            }

            // A provisioned slot no longer supported by the provider is reconciled during settlement.
            slots.Add(new InventorySlot { Provider = id, Index = 100000, Item = gold, Count = full, SlotType = slots[0].SlotType });
            var soldier = SoldierEntities.Spawn(em, root, SoldierId.FromIndex(0), EntityState.Position(em, core), true);
            SoldierCombatants.Configure(em, root, soldier, false, id, EntityState.Position(em, core));
            EntityState.Set(em, soldier, new Soldier { Garrison = id, Slot = em.GetComponentData<BuildingGarrisonStats>(core).Capacity + 1, PopulationCost = 1 }); // Unsupported slot: settlement must preview eviction regardless of the core capacity.
            Check(em.GetComponentData<Soldier>(soldier).Slot > em.GetComponentData<BuildingGarrisonStats>(core).Capacity && SoldierOps.UnassignedCount(em) == 0 && Buffer<PendingItem>(em, root).Length == 0, "Entry fixture has no pre-existing unassigned/pending pool");
            var before = SnapshotCodec.Capture(em, root);
            var entities = Entities(em);
            var state = em.GetComponentData<Session>(root);
            GameClock stateClock = em.GetComponentData<GameClock>(root);
            Check(NightOps.Begin(em, root) == ResultCode.ConfirmationRequired, "Settlement-created overflow and invalid garrison trigger confirmation");
            var losses = Buffer<NightEntryLoss>(em, root);
            Check(losses.Any(l => l.Soldier == em.GetComponentData<Identity>(soldier).Id) && losses.Any(l => l.Soldier == 0 && l.Item == gold && l.Amount > 0), "Review lists actual post-settlement items and individual soldier IDs");
            Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)) && state.Equals(em.GetComponentData<Session>(root)), "Preview/cancel does not settle, mutate RNG, charge, despawn or advance");
            Check(NightOps.Begin(em, root, true) == ResultCode.ConfirmationRequired, "Blind confirmed flag cannot bypass reviewed consent");
            var token = em.GetComponentData<NightEntryReview>(root).Token;
            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 3 });
            Check(GameRequestExecution.Execute(em, root, new AdvanceRequest { ReviewedLossToken = token }) == ResultCode.ConfirmationRequired, "Stale UI command cannot discard a changed day");
            Check(em.GetComponentData<NightEntryReview>(root).Token != token && em.Exists(soldier), "Stale confirmation refreshes review and preserves roster");
            token = em.GetComponentData<NightEntryReview>(root).Token;
            var reviewed = Buffer<NightEntryLoss>(em, root);
            before = SnapshotCodec.Capture(em, root);
            entities = Entities(em);
            foreach (var failure in new[]
            {
                "day-settled",
                "night-prepared",
                "root-published",
                "before-retire"
            }

            )
            {
                var rejected = false;
                try
                {
                    NightEntryOps.Begin(em, root, true, token, step =>
                    {
                        if (step == failure)
                            throw new IOException("Injected " + step);
                    });
                }
                catch (IOException)
                {
                    rejected = true;
                }

                Check(rejected && before.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)), "Entry " + failure + " rolls back costs, roster, all entities");
                Check(reviewed.SequenceEqual(Buffer<NightEntryLoss>(em, root)) && em.GetComponentData<NightEntryReview>(root).Token == token, "Entry " + failure + " keeps original reviewed consent");
            }

            Check(GameRequestExecution.Execute(em, root, new AdvanceRequest { ReviewedLossToken = token }) == ResultCode.Success, "Exact reviewed loss set enters night through command validation");
            var dusk = em.GetComponentData<Session>(root);
            GameClock duskClock = em.GetComponentData<GameClock>(root);
            DaySettlementState duskSettlement = em.GetComponentData<DaySettlementState>(root);
            Check(dusk.Phase == Phase.Deployment && duskClock.Turn == stateClock.Turn && duskSettlement.LastSettledTurn == stateClock.Turn, "Settlement commits once without advancing turn at dusk");
            Check(Buffer<PendingItem>(em, root).Length == 0 && SoldierOps.UnassignedCount(em) == 0 && !em.Exists(soldier), "Only confirmed post-settlement pools are cleared before deployment");
            Check(Buffer<GameEvent>(em, root).Count(e => e.Kind == EventKind.DuskCheckpoint) == 1 && em.GetComponentData<NightEntryReview>(root).Token == 0, "Single dusk request and consent consumed");
            var bytes = SnapshotCodec.Capture(em, root);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
            Check(em.GetComponentData<DaySettlementState>(root).LastSettledTurn == stateClock.Turn && Buffer<PendingItem>(em, root).Length == 0, "Dusk restore does not repeat settlement or recover dismissed pools");
            SnapshotCodec.Restore(em, root, initial);
            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 1 });
            NightOps.Begin(em, root);
            token = em.GetComponentData<NightEntryReview>(root).Token;
            GameRequestExecution.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.StorePending, ExpectedInventory = InventoryLayout.Fingerprint(em, root) });
            Check(NightOps.Begin(em, root) == ResultCode.Success, "Sorting pending inventory can remove need for confirmation entirely");
        }

        static void ImportFailure(World world, Entity root)
        {
            var em = world.EntityManager;
            var settings = em.GetComponentData<NightSettings>(root);
            settings.FirstInvasion = 1;
            settings.FirstBoss = 99999;
            settings.InvasionChance = 1;
            em.SetComponentData(root, settings);
            EntityState.Set(em, root, new NightPlanState { BossDefinition = EnemyId.None });
            NightOps.Plan(em, root, false);
            var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>();
            var archive = checkpoint.Export(root);
            var applied = archive.Copy();
            applied.Recovery.LossCount = 1;
            applied.Recovery.Seed = 987;
            RetryState sessionRetry = em.GetComponentData<RetryState>(root);
            sessionRetry.Count = 1;
            {
                em.SetComponentData(root, sessionRetry);
            }

            var waves = em.GetBuffer<NightWave>(root);
            var wave = waves[0];
            wave.Position += new float3(3, 0, 4);
            waves[0] = wave;
            applied.Current = applied.Day = SnapshotCodec.Capture(em, root);
            var locked = Buffer<NightWave>(em, root);
            checkpoint.Import(root, applied, false);
            Check(locked.SequenceEqual(Buffer<NightWave>(em, root)), "Continuing an already recovered node preserves its exact locked intelligence plan");
            checkpoint.Import(root, archive, false);
            var data = archive.Copy();
            data.RunId = Guid.NewGuid().ToString("N");
            data.Recovery.LossCount = 1;
            data.Recovery.Seed = 987;
            // Region references are now preflight-validated; inject a true post-publication fault
            // rather than using an invalid region table to simulate a late import failure.
            var before = SnapshotCodec.Capture(em, root);
            var entities = Entities(em);
            var oldArchive = RunArchiveCodec.Encode(checkpoint.Export(root));
            var rejected = false;
            try
            {
                checkpoint.Import(root, data, false, step =>
                {
                    if (step == "root-published")
                        throw new InvalidOperationException("Owned late import fault");
                });
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Check(rejected, "Import fails during recovery-plan preparation, not merely decode preflight");
            Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)), "Late import failure leaves original active world intact");
            Check(oldArchive.SequenceEqual(RunArchiveCodec.Encode(checkpoint.Export(root))), "Late import failure cannot switch archive ownership or recovery metadata");
        }
    }
}
#endif
