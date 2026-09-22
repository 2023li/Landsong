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
    public static class CoreRulesVerification
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

        static void Reject(Action action, string name)
        {
            var rejected = false;
            try
            {
                action();
            }
            catch (Exception e)when (e is InvalidDataException || e is IOException || e is ArgumentException)
            {
                rejected = true;
            }

            Check(rejected, name);
        }

        [MenuItem("Landsong/ECS/Verification/CoreRules")]
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
                File.WriteAllText("Library/LandsongEcs/core-rules-verification.txt", log.ToString());
            }
        }

        static void VerifyMap(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Wave one isolated verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var initialBytes = SnapshotCodec.Capture(em, root);
                var initial = SnapshotCodec.Decode(em, root, initialBytes);
                Rewards(em, root);
                SnapshotCodec.Restore(em, root, initial);
                Deaths(em, root);
                SnapshotCodec.Restore(em, root, initial);
                Quests(em, root);
                SnapshotCodec.Restore(em, root, initial);
                Reject(() => SnapshotCodec.Decode(em, root, initialBytes.Take(initialBytes.Length - 7).ToArray()), "Truncated node rejected");
                var invalid = SnapshotCodec.Decode(em, root, initialBytes);
                invalid.Records[0].Identity.Id = 0;
                var before = SnapshotCodec.Capture(em, root);
                Reject(() => SnapshotCodec.Restore(em, root, invalid), "Invalid stable ID rejected before restore");
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected restore preserves live world");
                Persistence(world, root);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void PhaseTo(EntityManager em, Entity root, Phase phase, NightKind kind = NightKind.Invasion)
        {
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            s.Phase = phase;
            sNight.Kind = kind;
            sPersistence.CheckpointPending = 0;
            sClock.PhaseTime = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }
        }

        static Entity Core(EntityManager em)
        {
            using var buildings = WorldQueries.Entities<Building>(em);
            foreach (var e in buildings)
                if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                    return e;
            throw new InvalidOperationException("No core");
        }

        static Entity Drop(EntityManager em, Entity root, ItemId item, int count)
        {
            var e = LootEntities.Spawn(em, root, LootId.FromIndex(0), EntityState.Position(em, Core(em)), false);
            EntityState.Set(em, e, new Loot { Item = item, Count = count });
            return e;
        }

        static void Rewards(EntityManager em, Entity root)
        {
            PhaseTo(em, root, Phase.Night);
            NightResultOps.Reset(em, root);
            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            var warehouse = BuildingCreation.Create(em, root, BuildingDefinitions.Find(em, root, "b仓库"), new int2(-500, -500), 0, 1, true);
            var provider = em.GetComponentData<Identity>(warehouse).Id;
            var slots = em.GetBuffer<InventorySlot>(root);
            var installed = false;
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].Provider == provider)
                {
                    var slot = slots[i];
                    slot.Item = gold;
                    slot.Count = 7;
                    slots[i] = slot;
                    installed = true;
                    break;
                }

            Check(installed, "Fixture has storage owned by a warehouse");
            var before = InventoryOps.Count(em, root, gold);
            var loot = Drop(em, root, gold, 3);
            var dropId = em.GetComponentData<Identity>(loot).Id;
            Check(NightOps.PickUp(em, root, loot) == ResultCode.Success, "Special drop accepted");
            Check(InventoryOps.Count(em, root, gold) == before && InventoryOps.PendingCount(em, root, gold) == 0, "Pickup does not touch live stock or pending pool");
            NightResultOps.RecordItem(em, root, dropId, 0, gold, 3);
            Check(em.GetBuffer<NightItemReward>(root).Length == 1, "Stable drop receipt prevents duplicate credit");
            BuildingLifecycle.Ruin(em, root, warehouse);
            Check(InventoryOps.Count(em, root, gold) == before - 7, "Ruin locks old stock without consuming night rewards");
            Drop(em, root, gold, 5);
            PhaseTo(em, root, Phase.Celebration);
            var clock = em.GetComponentData<GameClock>(root);
            clock.PhaseTime = em.GetComponentData<NightPlanState>(root).CombatElapsed + em.GetComponentData<NightSettings>(root).BattleAdvanceAt;
            em.SetComponentData(root, clock);
            var turn = em.GetComponentData<GameClock>(root).Turn;
            Check(NightOps.EndEarly(em, root) == ResultCode.Success, "Completed battle accepts next phase after the victory sequence");
            Check(em.GetComponentData<Session>(root).Phase == Phase.Day && em.GetBuffer<NightItemReward>(root).Length == 2, "Unclicked drop is collected and battle advances directly to dawn");
            Check(InventoryOps.Count(em, root, gold) + InventoryOps.PendingCount(em, root, gold) == before - 7 + 8, "Dawn loses ruined stock then grants every reward exactly once");
            using (var stock = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp))
                Check(!stock.Any(s => s.Provider == provider), "Lost warehouse slots removed before reward placement");
            var total = InventoryOps.Count(em, root, gold) + InventoryOps.PendingCount(em, root, gold);
            NightOps.Dawn(em, root);
            NightResultOps.Commit(em, root);
            Check(em.GetComponentData<GameClock>(root).Turn == turn + 1 && total == InventoryOps.Count(em, root, gold) + InventoryOps.PendingCount(em, root, gold), "Duplicate dawn and commit are harmless");
            PhaseTo(em, root, Phase.Night);
            NightResultOps.Reset(em, root);
            slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                slot.Item = gold;
                slot.Count = math.max(1, ItemDefinitions.Get(em, root, gold).MaximumStack);
                slots[i] = slot;
            }

            NightOps.PickUp(em, root, Drop(em, root, gold, 9));
            PhaseTo(em, root, Phase.Report);
            NightOps.Dawn(em, root);
            Check(InventoryOps.PendingCount(em, root, gold) == 9, "Full storage routes all night reward overflow to pending pool");
            PhaseTo(em, root, Phase.Night);
            NightResultOps.Reset(em, root);
            var blueprint = BuildingDefinitions.Find(em, root, "b仓库");
            var receipts = EntityIdentityAllocator.AllocateId(em, root);
            bool invalidLicenseRejected = false;
            try
            {
                RecordBlueprint(em, root, receipts, blueprint, 0);
            }
            catch (InvalidOperationException)
            {
                invalidLicenseRejected = true;
            }

            Check(invalidLicenseRejected && em.GetBuffer<NightBlueprintReward>(root).Length == 0, "Zero-level entitlement reward is rejected before writing a night receipt");
            RecordBlueprint(em, root, receipts, blueprint, 1);
            Check(em.GetBuffer<NightBlueprintReward>(root)[0].Level == 1, "Explicit level-one entitlement reward is recorded");
            PhaseTo(em, root, Phase.Report);
            NightOps.Dawn(em, root);
            Check(BuildingBlueprints.Has(em, root, blueprint), "Night entitlement reward commits through the same journal");
            PhaseTo(em, root, Phase.Night);
            NightResultOps.Reset(em, root);
            int soldiersBefore;
            using (var roster = WorldQueries.Entities<Soldier>(em))
                soldiersBefore = roster.Length;
            NightOps.PickUp(em, root, Drop(em, root, gold, 11));
            total = InventoryOps.Count(em, root, gold) + InventoryOps.PendingCount(em, root, gold);
            PhaseTo(em, root, Phase.GameOver);
            NightOps.Dawn(em, root);
            Check(total == InventoryOps.Count(em, root, gold) + InventoryOps.PendingCount(em, root, gold), "Core loss cannot commit rewards or advance dawn");
        }

        static void RecordBlueprint(EntityManager em, Entity root, ulong receipt, BuildingId building, int level)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalog = ref builder.ConstructRoot<OpportunityCatalogBlob>();
            var definitions = builder.Allocate(ref catalog.Definitions, 1);
            builder.Allocate(ref definitions[0].Rewards.Blueprints, 1)[0] = new BlueprintReward
            {
                Building = building,
                GrantedLevel = level
            };
            using var blob = builder.CreateBlobAssetReference<OpportunityCatalogBlob>(Allocator.Persistent);
            var original = em.GetComponentData<OpportunityCatalog>(root);
            try
            {
                em.SetComponentData(root, new OpportunityCatalog { Value = blob });
                NightResultOps.RecordOpportunityRewards(em, root, receipt, OpportunityId.FromIndex(0));
            }
            finally
            {
                em.SetComponentData(root, original);
            }
        }

        static void Deaths(EntityManager em, Entity root)
        {
            int soldiersBefore;
            using (var roster = WorldQueries.Entities<Soldier>(em))
                soldiersBefore = roster.Length;
            PhaseTo(em, root, Phase.Night);
            NightResultOps.Reset(em, root);
            var core = Core(em);
            var home = em.GetComponentData<Identity>(core).Id;
            var pos = EntityState.Position(em, core);
            var troop = SoldierEntities.Spawn(em, root, SoldierId.FromIndex(0), pos, true);
            SoldierCombatants.Configure(em, root, troop, true, home, pos);
            EntityState.Set(em, troop, new Soldier { Garrison = home, PopulationCost = 2 });
            var hero = HeroEntities.Spawn(em, root, HeroId.FromIndex(0), pos, true);
            HeroCombatants.Configure(em, root, hero, true, home, pos);
            EntityState.Set(em, hero, new Hero { Sanctum = home, Recruited = 1, Experience = 100 });
            var garrisonCount = GarrisonOps.GarrisonCount(em, home);
            var employed = PopulationOps.Employed(em);
            var heroCost = HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(hero).Definition).PopulationCost;
            CombatOps.ApplyDamage(em, root, new DamageRequest { Target = troop, Amount = 999999 });
            CombatOps.ApplyDamage(em, root, new DamageRequest { Target = hero, Amount = 999999 });
            Check(PopulationOps.Employed(em) == employed && GarrisonOps.GarrisonCount(em, home) == garrisonCount, "Dead troops and heroes retain population and slots until dawn");
            Check(em.GetComponentData<Hero>(hero).DeathPending != 0 && em.GetComponentData<Hero>(hero).CooldownUntil == 0, "Hero cooldown is pending during night");
            em.GetBuffer<NightWave>(root).Clear();
            NightOps.Tick(em, root, .1f);
            Check(em.Exists(troop) && !EntityState.Alive(em, troop), "Celebration does not delete or resurrect dead soldiers");
            var health = em.GetComponentData<Health>(core);
            health.Current = health.Maximum * .4f;
            em.SetComponentData(core, health);
            var turn = em.GetComponentData<GameClock>(root).Turn;
            NightOps.Dawn(em, root);
            Check(!em.Exists(troop) && PopulationOps.Employed(em) == employed - 2 - heroCost, "Dawn commits unit population release");
            var h = em.GetComponentData<Hero>(hero);
            Check(h.DeathPending == 0 && h.Experience == 0 && h.CooldownUntil == turn + 1 + math.max(1, HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(hero).Definition).RevivalCooldownTurns), "Hero full cooldown starts at next dawn");
            Check(em.GetComponentData<Health>(core).Current == health.Maximum, "Surviving PlayerHome recovers full durability at dawn");
            using var troops = WorldQueries.Entities<Soldier>(em);
            Check(troops.Length == soldiersBefore, "Dead soldier removed while existing starting soldiers remain in roster");
        }

        static void Quests(EntityManager em, Entity root)
        {
            var definition = QuestId.None;
            for (int i = 0; i < QuestDefinitions.Count(em, root); i++)
            {
                var candidate = QuestId.FromIndex(i);
                ref var source = ref QuestDefinitions.Get(em, root, candidate);
                if ((source.Behavior & QuestBehaviorFlags.Mainline) == 0 && QuestLifecycle.FailureCosts(em, root, candidate).Count > 0)
                {
                    definition = candidate;
                    break;
                }
            }

            Check(definition.IsValid, "Authored random quest has a failure cost");
            var costs = QuestLifecycle.FailureCosts(em, root, definition);
            var item = costs[0].Item;
            InventoryOps.Remove(em, root, item, InventoryOps.Count(em, root, item));
            InventoryOps.Add(em, root, item, 2);
            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = item, Amount = 100 });
            var occupiedBefore = QuestLifecycle.QuestCount(em);
            var quest = QuestLifecycle.CreateQuest(em, root, definition);
            var q = em.GetComponentData<Quest>(quest);
            Check(QuestLifecycle.BindQuestContainer(em, ref q), "Fixture binds completed quest to real container");
            q.Status = QuestStatus.Completed;
            em.SetComponentData(quest, q);
            Check(QuestLifecycle.QuestCount(em) == occupiedBefore + 1, "Completed random quests occupy accepted capacity");
            QuestLifecycle.EvaluateQuests(em, root);
            Check(em.GetComponentData<Quest>(quest).Status == QuestStatus.Completed, "Completed task cannot regress when requirements disappear");
            Check(QuestLifecycle.FailQuest(em, root, quest, "test") == ResultCode.Unavailable, "Completed task cannot be failed or penalized");
            q.Status = QuestStatus.Active;
            em.SetComponentData(quest, q);
            var id = em.GetComponentData<Identity>(quest).Id;
            Check(QuestLifecycle.QuestCount(em) == occupiedBefore + 1, "Active task consumes one accepted slot");
            Check(QuestLifecycle.ChangeStatus(em, root, id, QuestAction.Abandon) == ResultCode.Success, "Abandon commits failure");
            var expected = math.max(0, 2 - costs[0].Amount);
            Check(InventoryOps.Count(em, root, item) == expected && InventoryOps.PendingCount(em, root, item) == 100, "Penalty takes at most held normal stock, not pending resources");
            Check(QuestLifecycle.ChangeStatus(em, root, id, QuestAction.Abandon) == ResultCode.InvalidTarget && InventoryOps.Count(em, root, item) == expected, "Duplicate abandon cannot charge twice");
            InventoryOps.Add(em, root, item, 2);
            quest = QuestLifecycle.CreateQuest(em, root, definition);
            q = em.GetComponentData<Quest>(quest);
            Check(QuestLifecycle.BindQuestContainer(em, ref q), "Fixture binds timed quest");
            q.Status = QuestStatus.Active;
            q.Deadline = em.GetComponentData<GameClock>(root).Turn + 1;
            em.SetComponentData(quest, q);
            TurnSettlement.Settle(em, root);
            Check(!em.Exists(quest) && InventoryOps.Count(em, root, item) == math.max(0, expected + 2 - costs[0].Amount), "Timeout uses same partial-payment failure transaction");
        }

        static string Composition(EntityManager em, Entity root)
        {
            using var waves = em.GetBuffer<NightWave>(root).ToNativeArray(Allocator.Temp);
            return string.Join(";", waves.Select(w => $"{w.Definition}/{w.Count}/{w.Direction}/{w.Position}/{w.PowerScale}/{w.At}"));
        }

        static void Persistence(World world, Entity root)
        {
            var em = world.EntityManager;
            var settings = em.GetComponentData<NightSettings>(root);
            CurrencySettings settingsCurrency = em.GetComponentData<CurrencySettings>(root);
            settings.FirstInvasion = 1;
            settings.InvasionChance = 1;
            settings.FirstBoss = 99999;
            {
                em.SetComponentData(root, settings);
                em.SetComponentData(root, settingsCurrency);
            }

            EntityState.Set(em, root, new NightPlanState { BossDefinition = EnemyId.None });
            NightOps.Plan(em, root, false);
            Check(InventoryOps.Add(em, root, settingsCurrency.Gold, 20) == 20, "Persistence fixture has spendable gold");
            var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>();
            var directory = Path.GetFullPath(Path.Combine("Library/LandsongEcs/VerificationRuns", Guid.NewGuid().ToString("N")));
            var fixtureParent = Path.GetFullPath("Library/LandsongEcs/VerificationRuns");
            if (Path.GetDirectoryName(directory) != fixtureParent || Directory.Exists(directory))
                throw new InvalidOperationException("Expected a new isolated fixture directory.");
            var previousStore = checkpoint.Store;
            try
            {
                var store = new RunArchiveStore(directory);
                checkpoint.Store = store;
                SimulationEvents.Emit(em, root, EventKind.DayCheckpoint, default);
                checkpoint.Update();
                checkpoint.OpenNewRun(root);
                var original = checkpoint.Export(root);
                var gold = settingsCurrency.Gold;
                InventoryOps.Remove(em, root, gold, 1);
                var manualGold = InventoryOps.Count(em, root, gold);
                SimulationEvents.Emit(em, root, EventKind.Save, default);
                checkpoint.Update();
                var saved = store.ReadContinue(out var backup);
                Check(!backup && original.Day.SequenceEqual(saved.Day), "Manual save does not replace original day node");
                Check(saved.Manual != null && !saved.Manual.SequenceEqual(saved.Day), "Manual point is stored separately from day node");
                checkpoint.Import(root, RunArchiveCodec.Decode(RunArchiveCodec.Encode(saved)), true);
                Check(InventoryOps.Count(em, root, gold) == manualGold, "Cold envelope restore resumes manual white-day state");
                Check(NightOps.Begin(em, root, true) == ResultCode.Success, "Test reaches dusk");
                checkpoint.Update();
                var duskGold = InventoryOps.Count(em, root, gold);
                var originalStrength = em.GetComponentData<NightRuntimeState>(root).StartCombatStrength;
                PhaseTo(em, root, Phase.Night);
                NightResultOps.Reset(em, root);
                int soldiersBefore;
                using (var roster = WorldQueries.Entities<Soldier>(em))
                    soldiersBefore = roster.Length;
                NightOps.PickUp(em, root, Drop(em, root, gold, 19));
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = Core(em), Amount = 9999999 });
                checkpoint.Update();
                var lost = store.ReadContinue(out _);
                var ticket = lost.Recovery;
                Check(ticket.AwaitingDecision != 0 && ticket.LossCount == 1 && ticket.Seed != 0, "Core loss persists one recovery ticket before any player choice");
                var composition = Composition(em, root);
                checkpoint.Update();
                Check(em.GetComponentData<RecoveryState>(root).Seed == ticket.Seed, "Idle game-over updates do not reroll seed");
                checkpoint.Import(root, RunArchiveCodec.Decode(RunArchiveCodec.Encode(lost)), true);
                Check(em.GetComponentData<Session>(root).Phase == Phase.GameOver && em.GetComponentData<RecoveryState>(root).Seed == ticket.Seed, "Cold restore keeps pending game-over choice and seed");
                Check(Composition(em, root) == composition, "Game-over intelligence composition survives cold restore");
                checkpoint.Retry(root, false);
                var dayComposition = Composition(em, root);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Day && InventoryOps.Count(em, root, gold) == manualGold + 1, "Day retry returns to actual start, not manual point");
                Check(em.GetComponentData<NightRuntimeState>(root).StartCombatStrength == originalStrength && em.GetComponentData<RecoveryState>(root).LossCount == 1, "Day rewind preserves base strength and hidden assistance metadata");
                Check(em.GetBuffer<NightItemReward>(root).Length == 0 && em.GetBuffer<BattleReportEntry>(root).Length == 0 && em.GetBuffer<GameEvent>(root).Length == 0, "Rewind clears rewards, losses and withdrawn notifications");
                checkpoint.Import(root, lost, true);
                checkpoint.Retry(root, true);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Deployment && InventoryOps.Count(em, root, gold) == duskGold, "Dusk retry keeps settled day costs");
                Check(dayComposition == Composition(em, root), "Both nodes use identical seed, budget and enemy composition");
                checkpoint.Retry(root, true);
                Check(em.GetComponentData<RecoveryState>(root).LossCount == 1, "Repeated retry command outside game-over cannot increment count");
                PhaseTo(em, root, Phase.Night);
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = Core(em), Amount = 9999999 });
                checkpoint.Update();
                var second = store.ReadContinue(out _);
                Check(second.Recovery.LossCount == 2 && second.Recovery.Seed != ticket.Seed, "A new actual core loss generates exactly one new ticket");
                checkpoint.Retry(root, true);
                PhaseTo(em, root, Phase.Report);
                NightOps.Dawn(em, root);
                checkpoint.Update();
                Check(em.GetComponentData<RecoveryState>(root).LossCount == 0 && checkpoint.Export(root).Dusk == null, "Successful dawn clears assistance and replaces old nodes");
                var current = checkpoint.Export(root);
                var invalid = current.Copy();
                invalid.Day = new byte[]
                {
                    1,
                    2,
                    3
                };
                var before = SnapshotCodec.Capture(em, root);
                Reject(() => checkpoint.Import(root, invalid, true), "Bad embedded node rejected before world import");
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Bad archive leaves world unchanged");
                Storage(store, current);
            }
            finally
            {
                checkpoint.Store = previousStore;
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        static void Storage(RunArchiveStore store, RunArchive current)
        {
            var a = current.Copy();
            store.Write(a);
            var path = store.RunPath(a.RunId);
            var bytes = File.ReadAllBytes(path);
            store.BeforeCommit = p =>
            {
                if (p == path)
                    throw new IOException("Injected failure before replace");
            };
            Reject(() => store.Write(a), "Interrupted write surfaces failure");
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Interrupted write preserves valid primary");
            store.BeforeCommit = null;
            store.Write(a);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            var restored = store.Read(a.RunId, out var usedBackup);
            Check(usedBackup && restored.RunId == a.RunId, "Corrupt primary recovers owned backup");
            store.Write(restored);
            Check(Directory.GetFiles(store.RunDirectory(a.RunId), "state.lsrun.corrupt-*").Length == 1, "Corrupt primary retained as evidence, good backup not overwritten");
            Reject(() => store.RunPath("../another-run"), "Path traversal run ID rejected");
            var b = a.Copy();
            b.RunId = Guid.NewGuid().ToString("N");
            store.Write(b);
            var unsavedId = Guid.NewGuid().ToString("N");
            store.End(unsavedId, "未保存王朝", 1);
            Check(File.Exists(store.RunPath(a.RunId)) && File.Exists(store.RunPath(b.RunId)) && store.ReadContinue(out _).RunId == b.RunId, "Ending an unsaved run cannot delete either saved dynasty or its active pointer");
            File.WriteAllBytes(store.RunPath(b.RunId), RunArchiveCodec.Encode(a));
            Reject(() => store.Read(b.RunId, out _), "Cross-run file rejected by ownership check");
            store.Write(b);
            store.End(a.RunId, "甲王朝", 10);
            store.End(a.RunId, "甲王朝", 10);
            Check(!File.Exists(store.RunPath(a.RunId)) && !File.Exists(store.RunPath(a.RunId) + RunArchiveStore.BackupSuffix) && File.Exists(store.RunPath(b.RunId)), "End removes only owned primary and backup");
            Check(Directory.GetFiles(Path.Combine(store.DirectoryPath, "history"), a.RunId + ".txt").Length == 1, "Repeated end writes only one dynasty history record");
            Reject(() => store.Write(a), "Ended dynasty tombstone prevents resurrection");
        }
    }
}
#endif
