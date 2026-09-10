#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingFeatureVerification
    {
        static readonly StringBuilder report = new StringBuilder(); static int assertions;
        static void Check(bool value, string text) { if (!value) throw new InvalidOperationException("FAIL " + text); assertions++; report.AppendLine("PASS " + text); }
        static bool Has<T>(DynamicBuffer<T> buffer, Func<T, bool> predicate) where T : unmanaged, IBufferElementData { foreach (var value in buffer) if (predicate(value)) return true; return false; }
        static void PhaseDay(EntityManager em, Entity root) { var s = em.GetComponentData<Session>(root); s.Phase = Phase.Day; s.Paused = 0; em.SetComponentData(root, s); }
        static void Step(EntityManager em, Entity root) { var s = em.GetComponentData<Session>(root); s.LastSettledTurn = -1; em.SetComponentData(root, s); EconomyOps.Settle(em, root); }
        static void Funds(EntityManager em, Entity root)
        {
            var slots = em.GetBuffer<InventorySlot>(root); var provider = slots[0].Provider;
            var count = em.GetComponentData<ContentCatalog>(root).Value.Value.Definitions.Length;
            for (var i = 0; i < count; i++) if (Sim.Definition(em, root, i).Kind == ContentKind.Item)
            {
                var remaining = 10000; var batch = 0;
                while (remaining > 0) { var amount = math.min(remaining, Sim.Definition(em, root, i).Capacity); slots.Add(new InventorySlot { Provider = provider, Index = 10000 + i * 100 + batch++, SlotType = slots[0].SlotType, Item = i, Count = amount }); remaining -= amount; }
            }
        }
        static void GivePending(EntityManager em, Entity root, int item, int amount)
        { var pool = em.GetBuffer<PendingItem>(root); for (var i = 0; i < pool.Length; i++) if (pool[i].Item == item) { var p = pool[i]; p.Amount += amount; pool[i] = p; return; } pool.Add(new PendingItem { Item = item, Amount = amount }); }
        static int2 Free(EntityManager em, Entity root, int definition, ulong ignore = 0, int2? different = null)
        {
            var g = em.GetComponentData<GridData>(root); for (var i = 0; i < g.Value.Value.Cells.Length; i++)
            { var p = g.Value.Value.Min + new int2(i % g.Value.Value.Size.x, i / g.Value.Value.Size.x); if ((!different.HasValue || math.any(p != different.Value)) && GridOps.CanPlace(em, root, definition, p, 0, ignore)) return p; }
            throw new InvalidOperationException("No legal verification cell for " + Sim.Definition(em, root, definition).Id);
        }
        [MenuItem("Landsong/ECS/Buildings/Verify building workflow")]
        public static string Run()
        {
            assertions = 0; report.Clear();
            try
            {
                VerifyAssets();
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { EcsSceneFlow.MapSceneRoot.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) VerifyMap(path);
                report.AppendLine("Assertions: " + assertions); return report.ToString();
            }
            catch (Exception e) { report.AppendLine(e.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/building-verification.txt", report.ToString()); }
        }
        static void VerifyAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            foreach (var d in catalog.Definitions.Where(d => d.Data.Kind == ContentKind.Building))
            {
                var prefab = d.Data.Prefab; Check(prefab != null && prefab.GetComponent<BuildingVisualAuthoring>()?.Definition == d, d.Data.Id + " visual authoring bound");
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                { Check(renderer.GetComponent<MeshFilter>()?.sharedMesh != null && renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null), d.Data.Id + " mesh/material references " + renderer.name); Check(renderer.GetComponent<EntityVisualAuthoring>()?.Owner == prefab, d.Data.Id + " renderer owned by persistent root"); }
                for (var level = 1; level <= d.Data.Level; level++)
                {
                    var slot = BuildingVisualResolver.Select(prefab, LifeStage.Operational, level, 1, d.Data.Building.DefaultSkin); Check(slot != null, d.Data.Id + " level " + level + " has model or explicit fallback");
                    var renderers = slot.GetComponentsInChildren<MeshRenderer>(true); var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    report.AppendLine("VISUAL " + d.Data.Id + " LV" + level + " extent=" + bounds.size + " placeholder=" + slot.Placeholder);
                }
            }
            var warehouse = catalog.Definitions[catalog.Find("b仓库")].Data;
            var one = BuildingVisualResolver.Select(warehouse.Prefab, LifeStage.Operational, 1, 1, warehouse.Building.DefaultSkin);
            var two = BuildingVisualResolver.Select(warehouse.Prefab, LifeStage.Operational, 2, 1, warehouse.Building.DefaultSkin);
            var three = BuildingVisualResolver.Select(warehouse.Prefab, LifeStage.Operational, 3, 1, warehouse.Building.DefaultSkin);
            Check(one != two && two != three && one.Level == 1 && two.Level == 2 && three.Level == 3, "Warehouse has three real distinct level models");
            Check(BuildingVisualResolver.Score(BuildingVisualPurpose.Operational, 1, 0, "another", false, BuildingVisualPurpose.Operational, 3, 1, "") < 0, "Never substitutes a different skin silently");
            var scene = EditorSceneManager.OpenPreviewScene(EcsSceneFlow.Game);
            try { var view = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EcsGameView>(true)).Single(); Check(view.BuildingToolbar != null && view.BuildingDetailsRows != null && view.BuildingConfirmRows != null && view.BuildingCatalog == catalog && view.BuildingBar != null && view.BuildingBar.Cards != null && view.BuildingBar.Tabs != null, "Game scene serialized building UGUI wiring"); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void VerifyMap(string path)
        {
            report.AppendLine("MAP " + path); var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128); using var world = new World("Building feature verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root); InvitationExpeditionVerification.FixturePermissions(em, root); Funds(em, root);
                var warehouse = Sim.FindDefinition(em, root, "b仓库"); Sim.Grant(em, root, warehouse, 3);
                var e = BuildingOps.Create(em, root, warehouse, Free(em, root, warehouse), 0, 1, true); var id = em.GetComponentData<Identity>(e).Id;
                var b = em.GetComponentData<Building>(e); b.Experience = 100; em.SetComponentData(e, b);
                var position = Free(em, root, warehouse, id, b.Cell); var quote = BuildingOps.CheckMove(em, root, e, position, 0);
                Check(quote.Allowed && quote.ExperienceLoss == 30, "Move quotes original 30 percent experience loss; " + quote.Code + " " + quote.Reason + " loss=" + quote.ExperienceLoss);
                var stock = quote.Costs.ToDictionary(c => c.Item, c => InventoryOps.Count(em, root, c.Item));
                var initialSlots = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).Where(s => s.Provider == id).ToArray();
                Check(BuildingOps.Move(em, root, e, position, 0) == ResultCode.Success, "Legal move succeeds");
                b = em.GetComponentData<Building>(e); Check(em.GetComponentData<Identity>(e).Id == id && b.Experience == 70 && math.all(b.Cell == position), "Move retains ID and updates position/experience");
                foreach (var c in quote.Costs) Check(InventoryOps.Count(em, root, c.Item) == stock[c.Item] - c.Amount, "Move payment matches preview " + c.Item);
                Check(initialSlots.SequenceEqual(em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).Where(s => s.Provider == id)), "Move retains inventory slots and contents");
                var unchanged = SnapshotCodec.Capture(em, root);
                Check(BuildingOps.Move(em, root, e, position, 0) != ResultCode.Success && unchanged.SequenceEqual(SnapshotCodec.Capture(em, root)), "Same-cell rejected move has no side effects");
                Check(BuildingOps.Move(em, root, e, new int2(int.MinValue / 2), 0) == ResultCode.InvalidPlacement && unchanged.SequenceEqual(SnapshotCodec.Capture(em, root)), "Out-of-map move is atomic");
                Check(BuildingOps.Rename(em, e, "<b>国库</b>\n") == ResultCode.Success && em.GetComponentData<Identity>(e).Name == "国库", "Name filters markup and control characters");
                BuildingOps.Rename(em, e, ""); Check(em.GetComponentData<Identity>(e).Name == Sim.Definition(em, root, warehouse).Name, "Empty name restores default");
                Check(System.Text.Encoding.UTF8.GetByteCount(BuildingOps.SanitizeName(new string('仓', 200))) <= 120, "Name has bounded UTF8 size");
                for (var level = 2; level <= 3; level++) { b = em.GetComponentData<Building>(e); b.Experience = 100000; em.SetComponentData(e, b); Check(BuildingOps.Upgrade(em, root, e) == ResultCode.Success && em.GetComponentData<Building>(e).Level == level, "Warehouse actual upgrade to " + level); var selected = BuildingVisualResolver.Select(em, e); Check(selected != Entity.Null && Has(em.GetBuffer<BuildingVisualSlot>(e), v => v.Slot == selected && v.Level == level), "Baked ECS chooses upgraded visual " + level); }
                Check(!BuildingOps.CheckUpgrade(em, root, e).Allowed, "Maximum level blocks upgrade");
                var storedSlots = em.GetBuffer<InventorySlot>(root); var gold = em.GetComponentData<GameSettings>(root).Gold;
                var testIndex = -1; for (var i = 0; i < storedSlots.Length; i++) if (storedSlots[i].Provider == id) { testIndex = i; var slot = storedSlots[i]; slot.Item = gold; slot.Count = 7; storedSlots[i] = slot; break; }
                Check(testIndex >= 0, "Warehouse test owns inventory");
                var night = em.GetComponentData<Session>(root); night.Phase = Phase.Night; em.SetComponentData(root, night);
                var beforeRuin = InventoryOps.Count(em, root, gold); BuildingOps.Ruin(em, root, e);
                Check(em.GetComponentData<Building>(e).RuinPending == 1 && em.GetBuffer<InventorySlot>(root)[testIndex].Count == 7 && InventoryOps.Count(em, root, gold) == beforeRuin - 7, "Night ruin locks stored contents before dawn commit");
                var grid = em.GetComponentData<GridData>(root); Check(GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), position), "Ruin is high-cost traversable");
                Check(BuildingOps.CheckMove(em, root, e).Code == ResultCode.InvalidTarget, "Ruins cannot move");
                BuildingOps.DawnBuildings(em, root); PhaseDay(em, root);
                Check(!Has(em.GetBuffer<InventorySlot>(root), s => s.Provider == id) && em.GetComponentData<Building>(e).Workers == 0, "Dawn removes ruined slots and jobs");
                Check(Has(em.GetBuffer<BattleReportEntry>(root), r => r.Kind == EventKind.InventoryLost && r.Amount == 7 && r.SourceName == em.GetComponentData<Identity>(e).Name), "Inventory loss records building name and amount");
                var total = BuildingCostOps.RepairTotal(em, root, e, out var duration); var goldBefore = InventoryOps.Count(em, root, gold);
                Check(BuildingOps.Repair(em, root, e) == ResultCode.Success && em.GetComponentData<Building>(e).Stage == LifeStage.Repairing && InventoryOps.Count(em, root, gold) == goldBefore, "Repair starts without paying all installments upfront");
                var repairSnapshot = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                var investment = BuildingCostOps.Investment(em, e); SnapshotCodec.Restore(em, root, repairSnapshot); e = Sim.Find(em, id);
                Check(em.GetBuffer<RepairMaterial>(e).Length == total.Count && BuildingCostOps.Investment(em, e).SequenceEqual(investment), "Snapshot restores frozen repair bill and investment");
                // Isolated out-of-map site deliberately has no resource connection.
                GridOps.Occupy(em, root, e, true); b = em.GetComponentData<Building>(e); b.Cell = new int2(-10000); em.SetComponentData(e, b);
                Step(em, root); Check(em.GetComponentData<Building>(e).Progress == 0, "Disconnected repair pauses with resources in ordinary stock");
                foreach (var c in total) GivePending(em, root, c.Item, c.Amount);
                for (var step = 0; step < duration; step++) Step(em, root);
                b = em.GetComponentData<Building>(e); Check(b.Stage == LifeStage.Operational && b.Workers == 0 && b.Level == 3, "Pending pool funds installments; repair keeps level without auto hiring");
                Check(em.GetBuffer<RepairMaterial>(e).Length == 0 && BuildingCostOps.Investment(em, e).SequenceEqual(investment), "Repair spending does not inflate construction investment");
                VerifyPopulation(em, root); VerifyRoad(em, root); VerifyDemolition(em, root); VerifyConstruction(em, root); VerifyAllDefinitions(em, root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void VerifyPopulation(EntityManager em, Entity root)
        {
            var house = BuildingOps.Create(em, root, Sim.FindDefinition(em, root, "b居民房"), new int2(-12000), 0, 1, true);
            BuildingOps.Ruin(em, root, house); BuildingOps.Repair(em, root, house); var costs = BuildingCostOps.RepairTotal(em, root, house, out var turns);
            foreach (var c in costs) GivePending(em, root, c.Item, c.Amount);
            for (var i = 0; i < turns; i++) Step(em, root);
            Check(em.GetComponentData<Building>(house).Population == 0 && em.GetComponentData<Building>(house).DeferredResidents == 2, "Repair-completion residents do not join earlier settlement");
            BuildingOps.DawnBuildings(em, root); Check(em.GetComponentData<Building>(house).Population == 2, "Repaired house gets two residents at that dawn");
            var garrison = BuildingOps.Create(em, root, Sim.FindDefinition(em, root, "b驻军营地"), new int2(-13000), 0, 1, true);
            var id = em.GetComponentData<Identity>(garrison).Id; var s = em.GetComponentData<Session>(root); s.BasePopulation = 100; em.SetComponentData(root, s);
            Funds(em, root); MilitaryOps.Recruit(em, root, new Command { Target = id, Definition = Sim.FindDefinition(em, root, "militia") }, false); MilitaryOps.PrepareNight(em, root);
            Entity troop; using (var troops = Sim.Entities<Soldier>(em)) troop = troops[0];
            s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; em.SetComponentData(root, s); BuildingOps.Ruin(em, root, garrison);
            Check(em.GetComponentData<Combatant>(troop).Deployed == 0 && em.GetComponentData<Health>(troop).Current == em.GetComponentData<Health>(troop).Maximum * .5f, "Queued sortie from ruined garrison has 50 percent health");
            BuildingOps.DawnBuildings(em, root); Check(em.GetComponentData<Soldier>(troop).Garrison == 0, "Ruined garrison unassigned only after dawn");
            s = em.GetComponentData<Session>(root); s.BasePopulation = -1000; em.SetComponentData(root, s); EconomyOps.ReconcilePopulation(em, root);
            Check(em.Exists(troop), "Population deficit never silently deletes persistent soldiers"); s.BasePopulation = 100; em.SetComponentData(root, s); PhaseDay(em, root);
        }
        static void VerifyRoad(EntityManager em, Entity root)
        {
            var definition = -1; var count = em.GetComponentData<ContentCatalog>(root).Value.Value.Definitions.Length;
            for (var i = 0; i < count; i++) if (Sim.Definition(em, root, i).Kind == ContentKind.Building && BuildingRoadOps.IsRoad(em, root, i)) { definition = i; break; }
            Check(definition >= 0, "Road category migrated"); Sim.Grant(em, root, definition); var start = Free(em, root, definition);
            var plan = BuildingRoadOps.Plan(em, root, definition, start, start); Check(plan.Quote.Allowed && plan.NewCells.Count == 1, "Road single-segment quote");
            var grid = em.GetComponentData<GridData>(root); var point = GridOps.Position(grid, start, new int2(1));
            Check(BuildingRoadOps.Build(em, root, new Command { Definition = definition, Position = point, EndPosition = point }) == ResultCode.Success, "Road command creates quoted path");
            var snapshot = SnapshotCodec.Capture(em, root); Check(!BuildingRoadOps.Plan(em, root, definition, start, start).Quote.Allowed && snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Existing road path skips cost and duplicate construction");
            Check(!BuildingRoadOps.Plan(em, root, definition, start, new int2(-99999)).Quote.Allowed, "Invalid road is rejected as a whole");
        }
        static void VerifyDemolition(EntityManager em, Entity root)
        {
            var e = BuildingOps.Create(em, root, Sim.FindDefinition(em, root, "b仓库"), new int2(-14000), 0, 1, true); var item = em.GetComponentData<GameSettings>(root).Gold;
            em.GetBuffer<BuildingInvestment>(e).Clear(); em.GetBuffer<BuildingInvestment>(e).Add(new BuildingInvestment { Item = item, Amount = 11 });
            Check(BuildingCostOps.DemolitionRefund(em, root, e).Single().Amount == 6, "Normal demolition refund rounds each resource upward to 50 percent");
            BuildingOps.Ruin(em, root, e); Check(BuildingCostOps.DemolitionRefund(em, root, e).Single().Amount == 3, "Ruined demolition refund rounds upward to 20 percent");
            BuildingOps.Repair(em, root, e); Check(BuildingCostOps.DemolitionRefund(em, root, e).Single().Amount == 3, "Repairing demolition uses ruined refund");
            var pending = em.GetBuffer<PendingItem>(root).ToNativeArray(Allocator.Temp).ToArray(); BuildingOps.Demolish(em, root, e);
            Check(!em.Exists(e) && pending.SequenceEqual(em.GetBuffer<PendingItem>(root).ToNativeArray(Allocator.Temp)), "Demolition never diverts overflowing refunds into pending pool");
        }
        static void VerifyAllDefinitions(EntityManager em, Entity root)
        {
            var count = em.GetComponentData<ContentCatalog>(root).Value.Value.Definitions.Length;
            for (var d = 0; d < count; d++)
            {
                var definition = Sim.Definition(em, root, d); if (definition.Kind != ContentKind.Building) continue;
                for (var level = 1; level <= definition.Level; level++)
                {
                    var e = BuildingOps.Create(em, root, d, new int2(-15000 - d * 20, -15000 - level * 20), 0, level, true);
                    var allOwned = true; foreach (var linked in em.GetBuffer<LinkedEntityGroup>(e)) if (em.HasComponent<Unity.Rendering.MaterialMeshInfo>(linked.Value)) allOwned &= em.HasComponent<VisualOwner>(linked.Value) && em.GetComponentData<VisualOwner>(linked.Value).Owner == e;
                    Check(allOwned, definition.Id + " every rendered submesh has remapped building owner");
                    Check(BuildingVisualResolver.Select(em, e) != Entity.Null, definition.Id + " baked visual resolves at level " + level);
                    var b = em.GetComponentData<Building>(e); b.Stage = LifeStage.Construction; em.SetComponentData(e, b); Check(BuildingVisualResolver.Select(em, e) != Entity.Null, definition.Id + " construction visual/fallback");
                    b.Stage = LifeStage.Ruined; em.SetComponentData(e, b); Check(BuildingVisualResolver.Select(em, e) != Entity.Null, definition.Id + " ruined visual/fallback");
                    em.DestroyEntity(e);
                }
            }
        }
        static void VerifyConstruction(EntityManager em, Entity root)
        {
            Funds(em, root); var definition = Sim.FindDefinition(em, root, "b仓库"); var grid = em.GetComponentData<GridData>(root);
            Entity core; using (var buildings = Sim.Entities<Building>(em)) core = buildings.First(e => em.GetComponentData<BuildingStats>(e).IsCore != 0);
            var anchor = em.GetComponentData<Building>(core).Cell; int2? cell = null;
            for (var radius = 2; radius < 30 && !cell.HasValue; radius++) for (var y = -radius; y <= radius && !cell.HasValue; y++) for (var x = -radius; x <= radius; x++) if (GridOps.CanPlace(em, root, definition, anchor + new int2(x, y), 0)) { cell = anchor + new int2(x, y); break; }
            Check(cell.HasValue, "Construction fixture has legal nearby cell");
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Build, Definition = definition, Position = GridOps.Position(grid, cell.Value, new int2(1)) }) == ResultCode.Success, "Real build command pays and creates foundation");
            var e = Sim.Find(em, em.GetBuffer<Occupancy>(root)[GridOps.Index(grid, cell.Value)].Owner); Check(em.GetComponentData<Building>(e).Stage == LifeStage.Construction, "Foundation starts in construction stage");
            Check(ResourceNetworkOps.Provider(em, root, e) != Entity.Null, "Construction is connected to a real provider");
            var construct = typeof(EconomyOps).GetMethod("Construct", BindingFlags.Static | BindingFlags.NonPublic); var bill = BuildingCostOps.Rules(em, root, definition, RuleKind.ConstructionCost, 1);
            var missing = bill[0].Item; InventoryOps.Remove(em, root, missing, InventoryOps.Count(em, root, missing)); var snapshot = SnapshotCodec.Capture(em, root);
            construct.Invoke(null, new object[] { em, root, e }); Check(snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Missing construction material causes no payment or progress");
            Funds(em, root); for (var i = 0; i < Sim.Definition(em, root, definition).Duration; i++) construct.Invoke(null, new object[] { em, root, e });
            Check(em.GetComponentData<Building>(e).Stage == LifeStage.Operational, "Paid construction periods complete real building");
            Check(BuildingCostOps.Investment(em, e).SequenceEqual(BuildingCostOps.DefinitionInvestment(em, root, definition, 1, int.MaxValue)), "Completed construction investment equals paid placement and all stages");
        }
    }
}
#endif
