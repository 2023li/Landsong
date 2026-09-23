using Landsong.Content;
#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
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
        static readonly StringBuilder report = new StringBuilder();
        static int assertions;
        static void Check(bool value, string text)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + text);
            assertions++;
            report.AppendLine("PASS " + text);
        }

        static bool Has<T>(DynamicBuffer<T> buffer, Func<T, bool> predicate)
            where T : unmanaged, IBufferElementData
        {
            foreach (var value in buffer)
                if (predicate(value))
                    return true;
            return false;
        }

        static void PhaseDay(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            s.Phase = Phase.Day;
            sControl.Paused = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sControl);
            }
        }

        static void Step(EntityManager em, Entity root)
        {
            DaySettlementState sSettlement = em.GetComponentData<DaySettlementState>(root);
            sSettlement.LastSettledTurn = -1;
            {
                em.SetComponentData(root, sSettlement);
            }

            DailyEconomySettlement.Settle(em, root);
        }

        static void Funds(EntityManager em, Entity root)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            var provider = slots[0].Provider;
            var count = ItemDefinitions.Count(em, root);
            for (var i = 0; i < count; i++)
            {
                var remaining = 10000;
                var batch = 0;
                while (remaining > 0)
                {
                    var amount = math.min(remaining, ItemDefinitions.Get(em, root, ItemId.FromIndex(i)).MaximumStack);
                    slots.Add(new InventorySlot { Provider = provider, Index = 10000 + i * 100 + batch++, SlotType = slots[0].SlotType, Item = ItemId.FromIndex(i), Count = amount });
                    remaining -= amount;
                }
            }
        }

        static void GivePending(EntityManager em, Entity root, ItemId item, int amount)
        {
            var pool = em.GetBuffer<PendingItem>(root);
            for (var i = 0; i < pool.Length; i++)
                if (pool[i].Item == item)
                {
                    var p = pool[i];
                    p.Amount += amount;
                    pool[i] = p;
                    return;
                }

            pool.Add(new PendingItem { Item = item, Amount = amount });
        }

        static int2 Free(EntityManager em, Entity root, BuildingId definition, ulong ignore = 0, int2? different = null)
        {
            var g = em.GetComponentData<GridData>(root);
            for (var i = 0; i < g.Value.Value.Cells.Length; i++)
            {
                var p = g.Value.Value.Min + new int2(i % g.Value.Value.Size.x, i / g.Value.Value.Size.x);
                if ((!different.HasValue || math.any(p != different.Value)) && GridOps.CanPlace(em, root, definition, p, 0, ignore))
                    return p;
            }

            throw new InvalidOperationException("No legal verification cell for " + BuildingDefinitions.Get(em, root, definition).Metadata.Id);
        }

        [MenuItem("Landsong/ECS/Buildings/Verify building workflow")]
        public static string Run()
        {
            assertions = 0;
            report.Clear();
            try
            {
                VerifyAssets();
                foreach (var path in Landsong.EditorTools.GameMapPaths.BakedScenes())
                    VerifyMap(path);
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception e)
            {
                report.AppendLine(e.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/building-verification.txt", report.ToString());
            }
        }

        static void VerifyAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
            foreach (var d in catalog.Definitions)
            {
                var prefab = d.Prefab;
                Check(prefab != null && prefab.GetComponent<BuildingVisualAuthoring>()?.Definition == d, d.Metadata.Id + " visual authoring bound");
                Check(prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "SelectionAnchor") == 1, d.Metadata.Id + " has exactly one SelectionAnchor");
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Check(renderer.GetComponent<MeshFilter>()?.sharedMesh != null && renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null), d.Metadata.Id + " mesh/material references " + renderer.name);
                    Check(renderer.GetComponent<EntityVisualAuthoring>()?.Owner == prefab, d.Metadata.Id + " renderer owned by persistent root");
                }

                for (var level = 1; level <= d.MaximumLevel; level++)
                {
                    var slot = BuildingVisualResolver.Select(prefab, LifeStage.Operational, level, 1, d.PlacementAndVisuals.DefaultSkin);
                    Check(slot != null, d.Metadata.Id + " level " + level + " has model or explicit fallback");
                    var renderers = slot.GetComponentsInChildren<MeshRenderer>(true);
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                        bounds.Encapsulate(renderer.bounds);
                    report.AppendLine("VISUAL " + d.Metadata.Id + " LV" + level + " extent=" + bounds.size + " placeholder=" + slot.Placeholder);
                }
            }

            var warehouse = catalog.Definitions.Single(asset => asset.Metadata.Id == "b仓库");
            var one = BuildingVisualResolver.Select(warehouse.Prefab, LifeStage.Operational, 1, 1, warehouse.PlacementAndVisuals.DefaultSkin);
            var two = BuildingVisualResolver.Select(warehouse.Prefab, LifeStage.Operational, 2, 1, warehouse.PlacementAndVisuals.DefaultSkin);
            var three = BuildingVisualResolver.Select(warehouse.Prefab, LifeStage.Operational, 3, 1, warehouse.PlacementAndVisuals.DefaultSkin);
            Check(one != two && two != three && one.Level == 1 && two.Level == 2 && three.Level == 3, "Warehouse has three real distinct level models");
            Check(BuildingVisualResolver.Score(BuildingVisualPurpose.Operational, 1, 0, "another", false, BuildingVisualPurpose.Operational, 3, 1, "") < 0, "Never substitutes a different skin silently");
            var gamePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationUiAuthoring.GamePath);
            Check(gamePrefab != null, "Game panel prefab exists");
            var view = gamePrefab.GetComponent<UI_GamePanel>();
            Check(view != null && view.Buildings != null, "Game panel has configured building controller");
            var buildings = view.Buildings;
            var details = view.BuildingDetails;
            var productionBlock = details.Block<UI_GamePanel_BuildingDetails_Block_基础产出>();
            var workforceBlock = details.Block<UI_GamePanel_BuildingDetails_Block_岗位>();
            var plantingBlock = details.Block<UI_GamePanel_BuildingDetails_Block_种植>();
            var garrisonBlock = details.Block<UI_GamePanel_BuildingDetails_Block_驻军>();
            var otherBlock = details.Block<UI_GamePanel_BuildingDetails_Block_其他>();
            Check(buildings.BuildingActionBar != null && buildings.BuildingDetailsButton != null && details != null && buildings.DetailsPanel == details && details.DetailsRows == otherBlock.Rows && productionBlock.View == details && workforceBlock.View == details && plantingBlock.View == details && garrisonBlock.View == details && otherBlock.View == details && otherBlock.RowTemplate != null && details.ExperienceHover != null && details.ExperienceHover.View == details && buildings.BuildingConfirmRows != null && buildings.BuildingCatalog == AssetDatabase.LoadAssetAtPath<Landsong.Content.BuildingDisplayCatalog>(BuildingDisplayCatalogCompiler.Path) && buildings.BuildingBar != null && buildings.BuildingBar.Cards != null && buildings.BuildingBar.Tabs != null, "Game panel serialized building UGUI wiring");
            Check(buildings.CropSelectionPanel != null && buildings.CropSelectionPanel.transform.IsChildOf(view.ModalRoot), "Planting has an independent crop selection modal");
            buildings.CropSelectionPanel.ValidateConfiguration();
            Check(new SerializedObject(buildings).FindProperty("openWithOwner").boolValue, "Building action bar controller opens with GamePanel owner");
        }

        static void VerifyMap(string path)
        {
            report.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Building feature verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                InvitationExpeditionVerification.FixturePermissions(em, root);
                Funds(em, root);
                var warehouse = BuildingDefinitions.Find(em, root, "b仓库");
                BuildingBlueprints.Grant(em, root, warehouse, 3);
                var e = BuildingCreation.Create(em, root, warehouse, Free(em, root, warehouse), 0, 1, true);
                var id = em.GetComponentData<Identity>(e).Id;
                var b = em.GetComponentData<Building>(e);
                BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                BuildingExperienceState bExperience = em.GetComponentData<BuildingExperienceState>(e);
                bExperience.Experience = 100;
                {
                    em.SetComponentData(e, b);
                    em.SetComponentData(e, bPlacement);
                    em.SetComponentData(e, bWorkforce);
                    em.SetComponentData(e, bExperience);
                }

                var position = Free(em, root, warehouse, id, bPlacement.Cell);
                var quote = BuildingPlacementCommands.CheckMove(em, root, e, position, 0);
                Check(quote.Allowed && quote.ExperienceLoss == 30, "Move quotes original 30 percent experience loss; " + quote.Code + " " + quote.Reason + " loss=" + quote.ExperienceLoss);
                var stock = quote.Costs.ToDictionary(c => c.Item, c => InventoryOps.Count(em, root, c.Item));
                var initialSlots = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).Where(s => s.Provider == id).ToArray();
                Check(BuildingPlacementCommands.Move(em, root, e, position, 0) == ResultCode.Success, "Legal move succeeds");
                {
                    b = em.GetComponentData<Building>(e);
                    bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                    bExperience = em.GetComponentData<BuildingExperienceState>(e);
                }

                Check(em.GetComponentData<Identity>(e).Id == id && bExperience.Experience == 70 && math.all(bPlacement.Cell == position), "Move retains ID and updates position/experience");
                foreach (var c in quote.Costs)
                    Check(InventoryOps.Count(em, root, c.Item) == stock[c.Item] - c.Amount, "Move payment matches preview " + c.Item);
                Check(initialSlots.SequenceEqual(em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).Where(s => s.Provider == id)), "Move retains inventory slots and contents");
                var unchanged = SnapshotCodec.Capture(em, root);
                Check(BuildingPlacementCommands.Move(em, root, e, position, 0) != ResultCode.Success && unchanged.SequenceEqual(SnapshotCodec.Capture(em, root)), "Same-cell rejected move has no side effects");
                Check(BuildingPlacementCommands.Move(em, root, e, new int2(int.MinValue / 2), 0) == ResultCode.InvalidPlacement && unchanged.SequenceEqual(SnapshotCodec.Capture(em, root)), "Out-of-map move is atomic");
                Check(BuildingNaming.Rename(em, e, "<b>国库</b>\n") == ResultCode.Success && em.GetComponentData<Identity>(e).Name == "国库", "Name filters markup and control characters");
                BuildingNaming.Rename(em, e, "");
                Check(em.GetComponentData<Identity>(e).Name == BuildingDefinitions.Get(em, root, warehouse).Metadata.Name, "Empty name restores default");
                Check(System.Text.Encoding.UTF8.GetByteCount(BuildingNaming.SanitizeName(new string ('仓', 200))) <= 120, "Name has bounded UTF8 size");
                for (var level = 2; level <= 3; level++)
                {
                    {
                        b = em.GetComponentData<Building>(e);
                        bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                        bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                        bExperience = em.GetComponentData<BuildingExperienceState>(e);
                    }

                    bExperience.Experience = 100000;
                    {
                        em.SetComponentData(e, b);
                        em.SetComponentData(e, bPlacement);
                        em.SetComponentData(e, bWorkforce);
                        em.SetComponentData(e, bExperience);
                    }

                    Check(BuildingUpgradeCommands.Apply(em, root, e) == ResultCode.Success && em.GetComponentData<Building>(e).Level == level, "Warehouse actual upgrade to " + level);
                    var selected = BuildingVisualResolver.Select(em, e);
                    Check(selected != Entity.Null && Has(em.GetBuffer<BuildingVisualSlot>(e), v => v.Slot == selected && v.Level == level), "Baked ECS chooses upgraded visual " + level);
                }

                Check(!BuildingUpgradeCommands.Check(em, root, e).Allowed, "Maximum level blocks upgrade");
                var storedSlots = em.GetBuffer<InventorySlot>(root);
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                var testIndex = -1;
                for (var i = 0; i < storedSlots.Length; i++)
                    if (storedSlots[i].Provider == id)
                    {
                        testIndex = i;
                        var slot = storedSlots[i];
                        slot.Item = gold;
                        slot.Count = 7;
                        storedSlots[i] = slot;
                        break;
                    }

                Check(testIndex >= 0, "Warehouse test owns inventory");
                var night = em.GetComponentData<Session>(root);
                night.Phase = Phase.Night;
                em.SetComponentData(root, night);
                var beforeRuin = InventoryOps.Count(em, root, gold);
                BuildingLifecycle.Ruin(em, root, e);
                Check(em.GetComponentData<Building>(e).RuinPending == 1 && em.GetBuffer<InventorySlot>(root)[testIndex].Count == 7 && InventoryOps.Count(em, root, gold) == beforeRuin - 7, "Night ruin locks stored contents before dawn commit");
                var grid = em.GetComponentData<GridData>(root);
                Check(GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), position), "Ruin is high-cost traversable");
                Check(BuildingPlacementCommands.CheckMove(em, root, e).Code == ResultCode.InvalidTarget, "Ruins cannot move");
                BuildingLifecycle.DawnBuildings(em, root);
                PhaseDay(em, root);
                Check(!Has(em.GetBuffer<InventorySlot>(root), s => s.Provider == id) && em.GetComponentData<BuildingWorkforceState>(e).Workers == 0, "Dawn removes ruined slots and jobs");
                Check(Has(em.GetBuffer<BattleReportEntry>(root), r => r.Kind == EventKind.InventoryLost && r.Amount == 7 && r.SourceName == em.GetComponentData<Identity>(e).Name), "Inventory loss records building name and amount");
                var total = BuildingCostOps.RepairTotal(em, root, e, out var duration);
                var goldBefore = InventoryOps.Count(em, root, gold);
                Check(BuildingLifecycle.Repair(em, root, e) == ResultCode.Success && em.GetComponentData<Building>(e).Stage == LifeStage.Repairing && InventoryOps.Count(em, root, gold) == goldBefore, "Repair starts without paying all installments upfront");
                var repairSnapshot = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                var investment = BuildingCostOps.Investment(em, e);
                SnapshotCodec.Restore(em, root, repairSnapshot);
                e = WorldQueries.Find(em, id);
                Check(em.GetBuffer<RepairMaterial>(e).Length == total.Count && BuildingCostOps.Investment(em, e).SequenceEqual(investment), "Snapshot restores frozen repair bill and investment");
                // Isolated out-of-map site deliberately has no resource connection.
                GridOps.Occupy(em, root, e, true);
                {
                    b = em.GetComponentData<Building>(e);
                    bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                    bExperience = em.GetComponentData<BuildingExperienceState>(e);
                }

                bPlacement.Cell = new int2(-10000);
                {
                    em.SetComponentData(e, b);
                    em.SetComponentData(e, bPlacement);
                    em.SetComponentData(e, bWorkforce);
                    em.SetComponentData(e, bExperience);
                }

                Step(em, root);
                Check(em.GetComponentData<BuildingConstructionState>(e).Progress == 0, "Disconnected repair pauses with resources in ordinary stock");
                foreach (var c in total)
                    GivePending(em, root, c.Item, c.Amount);
                for (var step = 0; step < duration; step++)
                    Step(em, root);
                {
                    b = em.GetComponentData<Building>(e);
                    bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                    bExperience = em.GetComponentData<BuildingExperienceState>(e);
                }

                Check(b.Stage == LifeStage.Operational && bWorkforce.Workers == 0 && b.Level == 3, "Pending pool funds installments; repair keeps level without auto hiring");
                Check(em.GetBuffer<RepairMaterial>(e).Length == 0 && BuildingCostOps.Investment(em, e).SequenceEqual(investment), "Repair spending does not inflate construction investment");
                VerifyPopulation(em, root);
                VerifyRoad(em, root);
                VerifyDemolition(em, root);
                VerifyConstruction(em, root);
                VerifyAllDefinitions(em, root);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void VerifyPopulation(EntityManager em, Entity root)
        {
            var house = BuildingCreation.Create(em, root, BuildingDefinitions.Find(em, root, "b居民房"), new int2(-12000), 0, 1, true);
            BuildingLifecycle.Ruin(em, root, house);
            BuildingLifecycle.Repair(em, root, house);
            var costs = BuildingCostOps.RepairTotal(em, root, house, out var turns);
            foreach (var c in costs)
                GivePending(em, root, c.Item, c.Amount);
            for (var i = 0; i < turns; i++)
                Step(em, root);
            Check(em.GetComponentData<BuildingHousingState>(house).Population == 0 && em.GetComponentData<BuildingHousingState>(house).DeferredResidents == 2, "Repair-completion residents do not join earlier settlement");
            BuildingLifecycle.DawnBuildings(em, root);
            Check(em.GetComponentData<BuildingHousingState>(house).Population == 2, "Repaired house gets two residents at that dawn");
            var garrison = BuildingCreation.Create(em, root, BuildingDefinitions.Find(em, root, "b驻军营地"), new int2(-13000), 0, 1, true);
            var id = em.GetComponentData<Identity>(garrison).Id;
            var s = em.GetComponentData<Session>(root);
            PopulationState sPopulation = em.GetComponentData<PopulationState>(root);
            sPopulation.BasePopulation = 100;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sPopulation);
            }

            Funds(em, root);
            SoldierOps.RecruitSoldiers(em, root, new RecruitSoldiersRequest { Garrison = id, Soldier = SoldierDefinitions.Find(em, root, "militia"), Quantity = 1 });
            BattleLifecycle.PrepareNight(em, root);
            var troop = GarrisonOps.AtSlot(em, id, 1);
            {
                s = em.GetComponentData<Session>(root);
                sPopulation = em.GetComponentData<PopulationState>(root);
            }

            s.Phase = Phase.Night;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sPopulation);
            }

            BuildingLifecycle.Ruin(em, root, garrison);
            Check(em.GetComponentData<Combatant>(troop).Deployed == 0 && em.GetComponentData<Health>(troop).Current == em.GetComponentData<Health>(troop).Maximum * .5f, "Queued sortie from ruined garrison has 50 percent health");
            BuildingLifecycle.DawnBuildings(em, root);
            Check(em.GetComponentData<Soldier>(troop).Garrison == 0, "Ruined garrison unassigned only after dawn");
            {
                s = em.GetComponentData<Session>(root);
                sPopulation = em.GetComponentData<PopulationState>(root);
            }

            sPopulation.BasePopulation = -1000;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sPopulation);
            }

            WorkforceSettlement.ReconcilePopulation(em, root);
            Check(em.Exists(troop), "Population deficit never silently deletes persistent soldiers");
            sPopulation.BasePopulation = 100;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sPopulation);
            }

            PhaseDay(em, root);
        }

        static void VerifyRoad(EntityManager em, Entity root)
        {
            var definition = BuildingId.None;
            var count = BuildingDefinitions.Count(em, root);
            for (var i = 0; i < count; i++)
                if (BuildingRoadOps.IsRoad(em, root, BuildingId.FromIndex(i)))
                {
                    definition = BuildingId.FromIndex(i);
                    break;
                }

            Check(definition.IsValid, "Road category migrated");
            BuildingBlueprints.Grant(em, root, definition, 1);
            var start = Free(em, root, definition);
            var plan = BuildingRoadOps.Plan(em, root, definition, start, start);
            Check(plan.Quote.Allowed && plan.NewCells.Count == 1, "Road single-segment quote");
            var grid = em.GetComponentData<GridData>(root);
            var point = GridOps.Position(grid, start, new int2(1));
            Check(BuildingRoadOps.Build(em, root, new BuildRoadRequest { Definition = definition, Start = point, End = point }) == ResultCode.Success, "Road command creates quoted path");
            var snapshot = SnapshotCodec.Capture(em, root);
            Check(!BuildingRoadOps.Plan(em, root, definition, start, start).Quote.Allowed && snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Existing road path skips cost and duplicate construction");
            Check(!BuildingRoadOps.Plan(em, root, definition, start, new int2(-99999)).Quote.Allowed, "Invalid road is rejected as a whole");
        }

        static void VerifyDemolition(EntityManager em, Entity root)
        {
            var e = BuildingCreation.Create(em, root, BuildingDefinitions.Find(em, root, "b仓库"), new int2(-14000), 0, 1, true);
            var item = em.GetComponentData<CurrencySettings>(root).Gold;
            em.GetBuffer<BuildingInvestment>(e).Clear();
            em.GetBuffer<BuildingInvestment>(e).Add(new BuildingInvestment { Item = item, Amount = 11 });
            Check(BuildingCostOps.DemolitionRefund(em, root, e).Single().Amount == 6, "Normal demolition refund rounds each resource upward to 50 percent");
            BuildingLifecycle.Ruin(em, root, e);
            Check(BuildingCostOps.DemolitionRefund(em, root, e).Single().Amount == 3, "Ruined demolition refund rounds upward to 20 percent");
            BuildingLifecycle.Repair(em, root, e);
            Check(BuildingCostOps.DemolitionRefund(em, root, e).Single().Amount == 3, "Repairing demolition uses ruined refund");
            var pending = em.GetBuffer<PendingItem>(root).ToNativeArray(Allocator.Temp).ToArray();
            BuildingLifecycle.Demolish(em, root, e);
            Check(!em.Exists(e) && pending.SequenceEqual(em.GetBuffer<PendingItem>(root).ToNativeArray(Allocator.Temp)), "Demolition never diverts overflowing refunds into pending pool");
        }

        static void VerifyAllDefinitions(EntityManager em, Entity root)
        {
            var count = BuildingDefinitions.Count(em, root);
            for (var d = 0; d < count; d++)
            {
                ref var definition = ref BuildingDefinitions.Get(em, root, BuildingId.FromIndex(d));
                for (var level = 1; level <= definition.MaximumLevel; level++)
                {
                    var e = BuildingCreation.Create(em, root, BuildingId.FromIndex(d), new int2(-15000 - d * 20, -15000 - level * 20), 0, level, true);
                    var allOwned = true;
                    foreach (var linked in em.GetBuffer<LinkedEntityGroup>(e))
                        if (em.HasComponent<Unity.Rendering.MaterialMeshInfo>(linked.Value))
                            allOwned &= em.HasComponent<VisualOwner>(linked.Value) && em.GetComponentData<VisualOwner>(linked.Value).Owner == e;
                    Check(allOwned, definition.Metadata.Id + " every rendered submesh has remapped building owner");
                    Check(BuildingVisualResolver.Select(em, e) != Entity.Null, definition.Metadata.Id + " baked visual resolves at level " + level);
                    var b = em.GetComponentData<Building>(e);
                    b.Stage = LifeStage.Construction;
                    em.SetComponentData(e, b);
                    Check(BuildingVisualResolver.Select(em, e) != Entity.Null, definition.Metadata.Id + " construction visual/fallback");
                    b.Stage = LifeStage.Ruined;
                    em.SetComponentData(e, b);
                    Check(BuildingVisualResolver.Select(em, e) != Entity.Null, definition.Metadata.Id + " ruined visual/fallback");
                    em.DestroyEntity(e);
                }
            }
        }

        static void VerifyConstruction(EntityManager em, Entity root)
        {
            Funds(em, root);
            var definition = BuildingDefinitions.Find(em, root, "b仓库");
            var grid = em.GetComponentData<GridData>(root);
            Entity core;
            using (var buildings = WorldQueries.Entities<Building>(em))
                core = buildings.First(e => em.GetComponentData<BuildingHousingStats>(e).IsCore != 0);
            var anchor = em.GetComponentData<BuildingPlacementState>(core).Cell;
            int2? cell = null;
            for (var radius = 2; radius < 30 && !cell.HasValue; radius++)
                for (var y = -radius; y <= radius && !cell.HasValue; y++)
                    for (var x = -radius; x <= radius; x++)
                        if (GridOps.CanPlace(em, root, definition, anchor + new int2(x, y), 0))
                        {
                            cell = anchor + new int2(x, y);
                            break;
                        }

            Check(cell.HasValue, "Construction fixture has legal nearby cell");
            Check(GameRequestExecution.Execute(em, root, new BuildRequest { Definition = definition, Position = GridOps.Position(grid, cell.Value, new int2(1)) }) == ResultCode.Success, "Real build command pays and creates foundation");
            var e = WorldQueries.Find(em, em.GetBuffer<Occupancy>(root)[GridOps.Index(grid, cell.Value)].Owner);
            Check(em.GetComponentData<Building>(e).Stage == LifeStage.Construction, "Foundation starts in construction stage");
            Check(ResourceNetworkOps.Provider(em, root, e) != Entity.Null, "Construction is connected to a real provider");
            var construct = typeof(ConstructionSettlement).GetMethod("Settle", BindingFlags.Static | BindingFlags.NonPublic);
            var bill = BuildingCostOps.ConstructionStage(em, root, definition, 1);
            var missing = bill[0].Item;
            InventoryOps.Remove(em, root, missing, InventoryOps.Count(em, root, missing));
            var snapshot = SnapshotCodec.Capture(em, root);
            construct.Invoke(null, new object[] { em, root, e });
            Check(snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Missing construction material causes no payment or progress");
            Funds(em, root);
            for (var i = 0; i < BuildingDefinitions.Get(em, root, definition).ConstructionTurns; i++)
                construct.Invoke(null, new object[] { em, root, e });
            Check(em.GetComponentData<Building>(e).Stage == LifeStage.Operational, "Paid construction periods complete real building");
            Check(BuildingCostOps.Investment(em, e).SequenceEqual(BuildingCostOps.DefinitionInvestment(em, root, definition, 1, int.MaxValue)), "Completed construction investment equals paid placement and all stages");
        }
    }
}
#endif
