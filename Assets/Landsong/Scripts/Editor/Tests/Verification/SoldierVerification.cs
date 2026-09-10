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
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class SoldierVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL " + name); checks++; log.AppendLine("PASS " + name); }
        static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, name); }
        [MenuItem("Landsong/ECS/Verification/Soldier")]
        public static string Run()
        {
            log = new StringBuilder(); checks = 0;
            try { Configuration(); Verify(); log.AppendLine("Assertions: " + checks); return log.ToString(); }
            catch (Exception e) { log.AppendLine(e.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/soldiers-verification.txt", log.ToString()); }
        }
        static void Configuration()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            var catalog = UnityEngine.Object.Instantiate(source); int at = Array.FindIndex(source.Definitions, d => d.Data.Kind == ContentKind.Soldier); var soldier = UnityEngine.Object.Instantiate(source.Definitions[at]);
            catalog.Definitions = (GameDefinitionAsset[])source.Definitions.Clone(); catalog.Definitions[at] = soldier;
            void Invalid(Action mutate, string name)
            { var growth = soldier.Data.SoldierGrowth; bool invalid = false; mutate(); try { using var blob = GameWorldAuthoring.BuildCatalog(catalog); } catch (InvalidOperationException) { invalid = true; } finally { soldier.Data.SoldierGrowth = growth; } Check(invalid, name); }
            try
            {
                Check(soldier.Data.SoldierGrowth.MaxLevel == 10 && soldier.Data.SoldierGrowth.BattleExperience == 10, "Existing soldier asset gets editable growth defaults");
                Invalid(() => soldier.Data.SoldierGrowth.HealthPerLevel = float.NaN, "Nonfinite growth rejected during baking");
                Invalid(() => soldier.Data.SoldierGrowth.FirstLevelExperience = 0, "Zero experience threshold rejected during baking");
                Invalid(() => { soldier.Data.SoldierGrowth.MaxLevel = 100; soldier.Data.SoldierGrowth.ExperienceStep = 1000000; }, "Overflowing cumulative experience rejected during baking");
                var rules = soldier.Data.Rules; soldier.Data.Rules = new[] { new RuleSource { Kind = RuleKind.RecruitCost, Target = "missing-resource", Amount = 1 } };
                Invalid(() => { }, "Missing recruitment resource rejected during baking"); soldier.Data.Rules = rules;
            }
            finally { UnityEngine.Object.DestroyImmediate(soldier); UnityEngine.Object.DestroyImmediate(catalog); }
        }
        static void Verify()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity"); using var store = new BlobAssetStore(128); using var world = new World("Wave eleven soldiers isolated", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                var state = em.GetComponentData<Session>(root); state.BasePopulation += 100; state.CheckpointPending = 0; em.SetComponentData(root, state);
                int soldierDefinition = Sim.FirstDefinition(em, root, ContentKind.Soldier), gold = em.GetComponentData<GameSettings>(root).Gold;
                var core = Entity.Null; using (var sites = Sim.Entities<Building>(em)) foreach (var e in sites) if (em.GetComponentData<BuildingStats>(e).IsCore != 0) core = e;
                int otherItem = -1; var catalog = em.GetComponentData<ContentCatalog>(root).Value;
                for (int i = 0; i < catalog.Value.Definitions.Length; i++) if (catalog.Value.Definitions[i].Kind == ContentKind.Item && i != gold) { otherItem = i; break; }
                void Fund(int item)
                {
                    var slots = em.GetBuffer<InventorySlot>(root);
                    for (int i = 0; i < 8; i++) slots.Add(new InventorySlot { Provider = Id(core), Index = 5000 + slots.Length, SlotType = -1, Item = item, Count = Sim.Definition(em, root, item).Capacity });
                }
                Fund(gold); Fund(otherItem);
                Entity Site(RuleKind kind)
                {
                    int definition = -1;
                    for (int i = 0; i < catalog.Value.Definitions.Length; i++) if (catalog.Value.Definitions[i].Kind == ContentKind.Building && Sim.Rule(em, root, i, kind).Level >= 0 && Sim.Rule(em, root, i, RuleKind.Population).B == 0) { definition = i; break; }
                    var grid = em.GetComponentData<GridData>(root);
                    for (int y = 15; y < grid.Value.Value.Size.y - 15; y++) for (int x = 15; x < grid.Value.Value.Size.x - 15; x++) if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0)) return BuildingOps.Create(em, root, definition, new int2(x, y), 0, 1, true);
                    throw new InvalidOperationException("No military fixture footprint");
                }
                var home = Site(RuleKind.Garrison); var secondHome = Site(RuleKind.Garrison); var homeId = Id(home); var secondHomeId = Id(secondHome);
                ResultCode Cmd(CommandKind kind, ulong target = 0, ulong other = 0, int amount = 0, int argument = 0, string text = "") => GameLoopSystem.Execute(em, root, new Command { Kind = kind, Target = target, Other = other, Amount = amount, Argument = argument, Definition = soldierDefinition, Text = new FixedString128Bytes(text) });
                var q = MilitaryOps.RecruitQuote(em, root, homeId, soldierDefinition, 2);
                var pendingQuote=MilitaryOps.RecruitQuote(em,root,secondHomeId,soldierDefinition,1,true);
                int pendingStock=InventoryOps.Count(em,root,gold),pendingPopulation=Sim.Employed(em);
                Check(Cmd(CommandKind.RecruitSoldier,secondHomeId,amount:1,argument:1)==ResultCode.Success,"Pool recruitment succeeds through command");
                Entity pendingUnit=Entity.Null;using(var units=Sim.OrderedEntities<Soldier>(em))foreach(var unit in units)if(em.GetComponentData<Soldier>(unit).Garrison==0)pendingUnit=unit;
                Check(pendingUnit!=Entity.Null&&em.GetComponentData<Soldier>(pendingUnit).Slot==0&&em.GetComponentData<Soldier>(pendingUnit).PendingSince==state.Turn,"New recruit remains unassigned with original turn deadline");
                Check(MilitaryOps.GarrisonCount(em,secondHomeId)==0&&InventoryOps.Count(em,root,gold)==pendingStock-pendingQuote.Costs.Sum(c=>c.Amount)&&Sim.Employed(em)>pendingPopulation,"Pool recruitment pays exact resources and population without occupying a slot");
                Check(Cmd(CommandKind.AssignSoldier,Id(pendingUnit),secondHomeId,argument:2)==ResultCode.Success&&em.GetComponentData<Soldier>(pendingUnit).Slot==2&&MilitaryOps.AtSlot(em,secondHomeId,1)==Entity.Null,"Selected pending soldier occupies specifically chosen slot");
                Check(Cmd(CommandKind.DismissSoldier,Id(pendingUnit),argument:1)==ResultCode.Success,"Pending flow fixture releases its soldier");
                var pendingHome=em.GetComponentData<Building>(secondHome);pendingHome.SoldiersRecruited=0;em.SetComponentData(secondHome,pendingHome);
                Check(q.Code == ResultCode.Success && q.Quantity == 2 && q.RemainingLimit == em.GetComponentData<BuildingStats>(home).Garrison, "Quote exposes two-person cost and per-site default limit");
                int stock = InventoryOps.Count(em, root, gold); var random = em.GetComponentData<Session>(root).RandomState; int population = Sim.Employed(em);
                Check(Cmd(CommandKind.RecruitSoldier, homeId, amount: 2) == ResultCode.Success, "Two soldiers recruited immediately by command");
                Check(stock - InventoryOps.Count(em, root, gold) == q.Costs.Sum(c => c.Amount), "Fallback gold fee equals quote");
                Check(em.GetComponentData<Session>(root).RandomState == random, "Personal names do not consume simulation random state");
                Check(Sim.Employed(em) == population + 2 * Sim.Definition(em, root, soldierDefinition).Population, "Recruitment reserves military population separately");
                var a = MilitaryOps.AtSlot(em, homeId, 1); var b = MilitaryOps.AtSlot(em, homeId, 2); ulong aid = Id(a), bid = Id(b);
                Check(a != Entity.Null && b != Entity.Null && em.GetComponentData<Identity>(a).Name != Sim.Definition(em, root, soldierDefinition).Name, "Named persistent individuals occupy distinct real slots");
                Check(Cmd(CommandKind.RenameSoldier, aid, text: "守城先锋") == ResultCode.Success && em.GetComponentData<Identity>(a).Name.ToString() == "守城先锋", "Day rename retains stable identity");
                Check(Cmd(CommandKind.RenameSoldier, aid, text: "   ") == ResultCode.InvalidContent, "Empty soldier name rejected");
                Check(Cmd(CommandKind.AssignSoldier, aid, secondHomeId, argument: 1) == ResultCode.Success, "Free no-distance cross-site assignment");
                Check(Cmd(CommandKind.AssignSoldier, bid, secondHomeId, argument: 1) == ResultCode.NoCapacity, "Occupied destination cannot silently replace soldier");
                Check(Cmd(CommandKind.SwapSoldiers, aid, bid) == ResultCode.Success && em.GetComponentData<Soldier>(a).Slot == 2 && em.GetComponentData<Soldier>(a).Garrison == homeId, "Exchange transfers actual slot and home, not display order");
                int limit = MilitaryOps.RecruitQuote(em, root, homeId, soldierDefinition, 1).RemainingLimit;
                Check(limit == q.RemainingLimit - 2, "Moving troops does not refund recruitment quota");
                Check(Cmd(CommandKind.UnassignSoldier, aid) == ResultCode.Success && em.GetComponentData<Soldier>(a).Slot == 0, "Unassignment clears real slot");
                int pendingTurn = em.GetComponentData<Soldier>(a).PendingSince;
                state = em.GetComponentData<Session>(root); state.Turn++; em.SetComponentData(root, state);
                NightOps.Plan(em, root, false);
                Check(Cmd(CommandKind.UnassignSoldier, aid) == ResultCode.Success && em.GetComponentData<Soldier>(a).PendingSince == pendingTurn, "Repeated unassign does not renew pending age");
                Check(MilitaryOps.RecruitQuote(em, root, homeId, soldierDefinition, 1).RemainingLimit == q.RemainingLimit, "Next turn restores recruitment quota");
                Check(Cmd(CommandKind.FillGarrison, homeId) == ResultCode.Success && em.GetComponentData<Soldier>(a).Slot == 1 && em.GetComponentData<Soldier>(b).Garrison == secondHomeId, "Fill assigns pending to empty slots only");
                Check(Cmd(CommandKind.AssignSoldier, bid, homeId, argument: 2) == ResultCode.Success, "Second individual assigned to second slot");
                Check(Cmd(CommandKind.SwapSoldiers, aid, bid) == ResultCode.Success, "Reverse age versus slot order for shrink test");
                var stats = em.GetComponentData<BuildingStats>(home); int oldCapacity = stats.Garrison; stats.Garrison = 1; em.SetComponentData(home, stats); MilitaryOps.ReconcileGarrisons(em, root);
                Check(em.GetComponentData<Soldier>(a).Garrison == 0 && em.GetComponentData<Soldier>(b).Slot == 1, "Capacity shrink evicts highest slot even if it owns older ID");
                stats.Garrison = oldCapacity; em.SetComponentData(home, stats); MilitaryOps.ReconcileGarrisons(em, root);
                Check(em.GetComponentData<Soldier>(a).Garrison == 0, "Restored capacity never auto-fills pending soldiers");
                Check(Cmd(CommandKind.FillGarrison, homeId) == ResultCode.Success, "Explicit fill after capacity restoration");
                var saved = SnapshotCodec.Capture(em, root); var decoded = SnapshotCodec.Decode(em, root, saved);
                SnapshotCodec.Restore(em, root, decoded); a = Sim.Find(em, aid); b = Sim.Find(em, bid); home = Sim.Find(em, homeId); secondHome = Sim.Find(em, secondHomeId);
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "New soldier fields and recruitment ledger round-trip byte-exactly");
                var malformed = SnapshotCodec.Decode(em, root, saved); var records = malformed.Records.Where(r => (r.Mask & 4) != 0).ToArray(); records[0].Soldier.Slot = records[1].Soldier.Slot;
                Reject(() => SnapshotCodec.Restore(em, root, malformed), "Duplicate real slot rejected before world replacement");
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected soldier snapshot leaves live state untouched");
                malformed = SnapshotCodec.Decode(em, root, saved); malformed.Records.First(r => (r.Mask & 4) != 0).Soldier.Experience = -1;
                Reject(() => SnapshotCodec.Restore(em, root, malformed), "Negative soldier experience rejected");
                foreach (int fail in new[] { 0, 1, 2 })
                {
                    var before = SnapshotCodec.Capture(em, root);
                    Check(MilitaryOps.RecruitSoldiers(em, root, new Command { Target = secondHomeId, Definition = soldierDefinition, Amount = 2 }, index => { if (index == fail) throw new InvalidOperationException("injected"); }) == ResultCode.PreparationFailed, "Injected recruitment failure " + fail);
                    Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Whole quantity / money / IDs / quota rollback " + fail);
                }
                foreach (string failure in new[] { "record-created", "garrisons-prepared", "root-published", "before-retire" })
                {
                    bool threw = false; try { SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved), probe: at => { if (at == failure) throw new InvalidOperationException("probe"); }); } catch (InvalidOperationException) { threw = true; }
                    Check(threw && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Soldier restore transactional fault " + failure);
                }
                int free = Sim.Employed(em); stock = InventoryOps.Count(em, root, gold);
                Check(Cmd(CommandKind.DismissSoldier, aid) == ResultCode.ConfirmationRequired && em.Exists(a), "Dismiss requires explicit second confirmation");
                Check(Cmd(CommandKind.DismissSoldier, aid, argument: 1) == ResultCode.Success && !em.Exists(a), "Confirmed dismissal deletes only chosen individual");
                Check(Sim.Employed(em) == free - Sim.Definition(em, root, soldierDefinition).Population && InventoryOps.Count(em, root, gold) == stock, "Dismiss releases population with no refund");
                SnapshotCodec.Restore(em, root, decoded); a = Sim.Find(em, aid); b = Sim.Find(em, bid); home = Sim.Find(em, homeId); secondHome = Sim.Find(em, secondHomeId);
                var growth = Sim.Definition(em, root, soldierDefinition).SoldierGrowth; var soldier = em.GetComponentData<Soldier>(a); soldier.Experience = MilitaryOps.LevelThreshold(growth, 2); em.SetComponentData(a, soldier);
                Check(MilitaryOps.Level(growth, soldier.Experience) == 2 && MilitaryOps.Level(growth, soldier.Experience - 1) == 1, "Level curve exact threshold");
                Check(MilitaryOps.SoldierStats(em, root, a).Damage > MilitaryOps.SoldierStats(em, root, b).Damage, "Personal growth changes actual combat stats");
                var bell1 = Site(RuleKind.Bell); var bell2 = Site(RuleKind.Bell); var bell1Id = Id(bell1); var bell2Id = Id(bell2);
                NightPlanOps.Prepare(em, root); MilitaryOps.PrepareNight(em, root);
                state = em.GetComponentData<Session>(root); state.Phase = Phase.Night; state.NightKind = NightKind.Invasion; em.SetComponentData(root, state);
                void DeployAt(Entity unit, float3 point) { var actor = em.GetComponentData<Combatant>(unit); actor.Deployed = 1; em.SetComponentData(unit, actor); em.SetComponentData(unit, LocalTransform.FromPosition(point)); }
                NavigationOps.TryNearestOpen(em, root, Sim.Position(em, bell1), 12, out var nearBell); DeployAt(a, nearBell); DeployAt(b, nearBell + new float3(0, 0, 1));
                var bs = em.GetComponentData<BuildingStats>(bell1); bs.BellRadius = 100; em.SetComponentData(bell1, bs);
                bs = em.GetComponentData<BuildingStats>(bell2); bs.BellRadius = .001f; em.SetComponentData(bell2, bs);
                Check(Cmd(CommandKind.Bell, bell1Id) == ResultCode.Success && em.GetComponentData<UnitOrder>(a).Source == bell1Id, "Bell rallies by current position to reachable perimeter");
                Cmd(CommandKind.Bell, bell2Id); Check(em.GetComponentData<UnitOrder>(a).Source != bell1Id && em.GetComponentData<UnitOrder>(a).Kind != OrderKind.Rally, "New bell clears previous command even outside new range");
                Cmd(CommandKind.Bell, bell2Id); Check(em.GetComponentData<Session>(root).ActiveBell == 0, "Repeated bell click cancels active bell");
                var enemy = Sim.Spawn(em, root, Sim.FirstDefinition(em, root, ContentKind.Enemy), Sim.Position(em, a), false); MilitaryOps.ConfigureCombatant(em, root, enemy, 1, false, true, homeId, Sim.Position(em, a));
                var engaged = em.GetComponentData<Combatant>(a); engaged.Target = enemy; em.SetComponentData(a, engaged);
                Cmd(CommandKind.Bell, bell1Id); Check(em.GetComponentData<UnitOrder>(a).Kind != OrderKind.Rally, "Bell never pulls a soldier out of close engagement");
                Check(Cmd(CommandKind.AssignSoldier, aid, secondHomeId) == ResultCode.WrongPhase && Cmd(CommandKind.DismissSoldier, aid, argument: 1) == ResultCode.WrongPhase, "Day management commands cannot mutate night roster");
                Check(Cmd(CommandKind.RecallGarrison, homeId) == ResultCode.Success && em.GetComponentData<Soldier>(a).RecallState == 1 && em.GetComponentData<UnitOrder>(a).Kind == OrderKind.Recall, "Garrison recall directly orders its deployed soldiers");
                Check(em.GetComponentData<Combatant>(a).Target == Entity.Null, "Recall forcibly clears current engagement target");
                float health = em.GetComponentData<Health>(a).Current; CombatOps.ApplyDamage(em, root, new DamageRequest { Target = a, Amount = 1 });
                Check(em.GetComponentData<Health>(a).Current == health - 1, "Returning soldier remains damageable");
                Cmd(CommandKind.Bell, bell1Id); Check(em.GetComponentData<UnitOrder>(a).Kind == OrderKind.Recall, "Bell cannot override active recall");
                Cmd(CommandKind.RecallGarrison, homeId, argument: 1); Check(em.GetComponentData<Soldier>(a).RecallState == 0, "Recall cancellable before arrival");
                Cmd(CommandKind.RecallGarrison, homeId); var destination = em.GetComponentData<UnitOrder>(a).Destination; em.SetComponentData(a, LocalTransform.FromPosition(destination)); MilitaryOps.TickSoldierOrders(em, root);
                Check(em.GetComponentData<Soldier>(a).RecallState == 2 && em.GetComponentData<Combatant>(a).Deployed == 0 && em.GetComponentData<VisualState>(a).Visible == 0, "Arrival shelters and hides soldier without deleting it");
                Cmd(CommandKind.RecallGarrison, homeId, argument: 1); Check(em.GetComponentData<Soldier>(a).RecallState == 2, "Cancel cannot redeploy already returned soldiers");
                Check(MilitaryOps.SoldierBattleExperience(em, root, a) == 0, "No experience from deployment alone");
                var ca = em.GetComponentData<Combatant>(a); ca.Participated = 1; em.SetComponentData(a, ca); MilitaryOps.ReportSoldierExperience(em, root); MilitaryOps.ReportSoldierExperience(em, root);
                int experienceRows = 0; foreach (var entry in em.GetBuffer<BattleReportEntry>(root)) if (entry.Kind == EventKind.SoldierExperience && entry.Id == aid) experienceRows++;
                Check(experienceRows == 1, "Experience report preview is idempotent");
                int experience = em.GetComponentData<Soldier>(a).Experience; MilitaryOps.Dawn(em, root); MilitaryOps.Dawn(em, root);
                Check(em.GetComponentData<Soldier>(a).Experience == experience + growth.BattleExperience, "Dawn grants soldier experience exactly once");
                Check(em.GetComponentData<Health>(a).Current == em.GetComponentData<Health>(a).Maximum && em.GetComponentData<Soldier>(a).RecallState == 0, "Dawn fully heals survivors and clears recall state");
                state = em.GetComponentData<Session>(root); state.Turn++; state.NightKind = NightKind.Peaceful; em.SetComponentData(root, state);
                Check(MilitaryOps.SoldierBattleExperience(em, root, a) == 0, "Peaceful patrol never grants combat experience");
                em.DestroyEntity(enemy);
                // Queued soldiers stay home when recalled, and losing a home interrupts only those still returning.
                state.NightKind = NightKind.Invasion; em.SetComponentData(root, state); var sb = em.GetComponentData<Soldier>(b); sb.RecallState = 0; em.SetComponentData(b, sb);
                var cb = em.GetComponentData<Combatant>(b); cb.Deployed = 0; em.SetComponentData(b, cb); Cmd(CommandKind.RecallGarrison, homeId);
                Check(em.GetComponentData<Soldier>(b).RecallState == 2, "Recall cancels not-yet-deployed queue for this night");
                sb.RecallState = 0; em.SetComponentData(b, sb); DeployAt(b, nearBell); Cmd(CommandKind.RecallGarrison, homeId);
                BuildingOps.Ruin(em, root, home); MilitaryOps.TickSoldierOrders(em, root);
                Check(em.GetComponentData<Soldier>(b).RecallState == 0 && em.GetComponentData<Combatant>(b).Deployed == 1, "Home ruin aborts return and leaves deployed soldier fighting");
                BuildingOps.DawnBuildings(em, root); Check(em.GetComponentData<Soldier>(b).Garrison == 0 && em.GetComponentData<Soldier>(b).Slot == 0, "Ruin commit clears both home and real slot");
                // Restore a valid day for independent cost tests.
                SnapshotCodec.Restore(em, root, decoded); home = Sim.Find(em, homeId);
                MultiCost(em, root, homeId, soldierDefinition, gold, otherItem);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void MultiCost(EntityManager em, Entity root, ulong home, int soldier, int gold, int other)
        {
            var original = em.GetComponentData<ContentCatalog>(root).Value; using var builder = new BlobBuilder(Allocator.Temp); ref var blob = ref builder.ConstructRoot<ContentBlob>();
            var definitions = builder.Allocate(ref blob.Definitions, original.Value.Definitions.Length); for (int i = 0; i < definitions.Length; i++) definitions[i] = original.Value.Definitions[i];
            var rules = builder.Allocate(ref blob.Rules, original.Value.Rules.Length + 3); for (int i = 0; i < original.Value.Rules.Length; i++) rules[i] = original.Value.Rules[i];
            var d = definitions[soldier]; d.RuleStart = original.Value.Rules.Length; d.RuleCount = 3; definitions[soldier] = d;
            rules[d.RuleStart] = new Rule { Kind = RuleKind.RecruitCost, Target = gold, Amount = 10 }; rules[d.RuleStart + 1] = new Rule { Kind = RuleKind.RecruitCost, Target = other, Amount = 2 }; rules[d.RuleStart + 2] = new Rule { Kind = RuleKind.RecruitCost, Target = gold, Amount = 5 };
            using var modified = builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent); em.SetComponentData(root, new ContentCatalog { Value = modified });
            try
            {
                var s = em.GetComponentData<Session>(root); s.Phase = Phase.Day; em.SetComponentData(root, s);
                var q = MilitaryOps.RecruitQuote(em, root, home, soldier, 2);
                Check(q.Costs.Count == 2 && q.Costs.First(c => c.Item == gold).Amount == 30 && q.Costs.First(c => c.Item == other).Amount == 4, "Explicit multi-resource costs replace gold fallback and aggregate duplicate items");
                InventoryOps.Remove(em, root, other, InventoryOps.Count(em, root, other)); int stock = InventoryOps.Count(em, root, gold);
                Check(MilitaryOps.RecruitSoldiers(em, root, new Command { Target = home, Definition = soldier, Amount = 2 }) == ResultCode.InsufficientResources && InventoryOps.Count(em, root, gold) == stock, "Missing secondary resource cannot partially charge gold");
                InventoryOps.Add(em, root, other, 4); stock = InventoryOps.Count(em, root, gold); int otherStock = InventoryOps.Count(em, root, other);
                Check(MilitaryOps.RecruitSoldiers(em, root, new Command { Target = home, Definition = soldier, Amount = 2 }) == ResultCode.Success, "Multi-resource quantity recruitment executes successfully");
                Check(InventoryOps.Count(em, root, gold) == stock - 30 && InventoryOps.Count(em, root, other) == otherStock - 4, "Multi-resource recruitment charges exactly the aggregated quote");
            }
            finally { em.SetComponentData(root, new ContentCatalog { Value = original }); }
        }
    }
}
#endif
