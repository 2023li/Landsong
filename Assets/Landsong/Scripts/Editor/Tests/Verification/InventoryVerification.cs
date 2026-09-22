#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
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
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class InventoryVerification
    {
        static StringBuilder log;
        static int assertions;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            assertions++;
            log.AppendLine("PASS " + label);
        }

        static T[] Rows<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        [MenuItem("Landsong/ECS/Verification/Inventory")]
        public static string Run()
        {
            log = new StringBuilder();
            assertions = 0;
            try
            {
                Fixture();
                foreach (var path in Landsong.EditorTools.GameMapPaths.BakedScenes())
                    Map(path);
                log.AppendLine("Assertions: " + assertions);
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
                File.WriteAllText("Library/LandsongEcs/inventory-verification.txt", log.ToString());
            }
        }

        static void Fixture()
        {
            using var fixture = new InventoryTestFixture();
            try
            {
                using var world = new World("Inventory fixture");
                var em = world.EntityManager;
                var root = em.CreateEntity();
                {
                    em.AddComponentData(root, new Session { });
                    em.AddComponentData(root, new GameClock() { Turn = 1 });
                    em.AddComponentData(root, new SimulationControl() { });
                    em.AddComponentData(root, new PopulationState() { });
                    em.AddComponentData(root, new PublicOpinionState() { });
                    em.AddComponentData(root, new ResearchState() { });
                    em.AddComponentData(root, new ExpeditionPenaltyState() { });
                    em.AddComponentData(root, new NightRuntimeState() { });
                    em.AddComponentData(root, new DaySettlementState() { });
                    em.AddComponentData(root, new RetryState() { });
                    em.AddComponentData(root, new HeroSelection() { });
                    em.AddComponentData(root, new BellState() { });
                    em.AddComponentData(root, new IntelligenceModeState() { });
                    em.AddComponentData(root, new PersistenceGate() { });
                    em.AddComponentData(root, new SimulationRandomState() { });
                    em.AddComponentData(root, new IdentitySequence() { });
                    em.AddComponentData(root, new DynastyIdentity() { });
                }

                fixture.Install(em, root);
                em.AddBuffer<InventorySlot>(root);
                em.AddBuffer<PendingItem>(root);
                InvitationExpeditionVerification.FixturePermissions(em, root);
                InventorySlot Slot(int index, int item = -1, int count = 0, float debt = 0, int type = 4, byte locked = 0) => new InventorySlot
                {
                    Provider = 7,
                    Index = index,
                    SlotType = StorageSlotId.FromIndex(type - 4),
                    Item = item < 0 ? ItemId.None : ItemId.FromIndex(item),
                    Count = count,
                    LossRemainder = debt,
                    Unavailable = locked
                };
                void Reset(params InventorySlot[] values)
                {
                    em.GetBuffer<InventorySlot>(root).CopyFrom(values);
                    em.GetBuffer<PendingItem>(root).Clear();
                }

                ResultCode Move(int from, int to, int item, int amount, string stamp = null) => InventoryLayout.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.MoveInventory, SourceProvider = 7, DestinationProvider = 7, SourceSlot = from, DestinationSlot = to, Item = ItemId.FromIndex(item), Quantity = amount, ExpectedInventory = stamp ?? InventoryLayout.Fingerprint(em, root) });
                Check(InventoryStorage.Accepts(em, root, StorageSlotId.FromIndex(1), ItemId.FromIndex(0)) && !InventoryStorage.Accepts(em, root, StorageSlotId.FromIndex(1), ItemId.FromIndex(1)) && InventoryStorage.Accepts(em, root, StorageSlotId.FromIndex(0), ItemId.FromIndex(1)), "Optional slot acceptance matches parent group; default type remains unrestricted");
                Reset(Slot(0, 0, 1), Slot(1, type: 5));
                InventoryOps.Add(em, root, ItemId.FromIndex(0), 3);
                Check(InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 4 && em.GetBuffer<InventorySlot>(root)[1].Count == 3, "Better-loss empty slot precedes worse-loss existing stack");
                InventoryOps.Remove(em, root, ItemId.FromIndex(0), 1);
                Check(em.GetBuffer<InventorySlot>(root)[0].Count == 0, "Consumption drains high-loss slot first");
                Reset(Slot(9), Slot(3));
                InventoryOps.Add(em, root, ItemId.FromIndex(0), 2);
                Check(em.GetBuffer<InventorySlot>(root)[1].Count == 2, "Equal storage priority uses stable provider/index rather than buffer order");
                Reset(Slot(0, 0, 8, .8f), Slot(1));
                Check(Move(0, 1, 0, 3) == ResultCode.Success, "Partial move splits stack");
                Check(math.abs(em.GetBuffer<InventorySlot>(root)[0].LossRemainder - .5f) < .00001f && math.abs(em.GetBuffer<InventorySlot>(root)[1].LossRemainder - .3f) < .00001f, "Split apportions debt with quantities");
                for (var i = 0; i < 100; i++)
                {
                    Move(1, 0, 0, 1);
                    Move(0, 1, 0, 1);
                }

                Check(InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 8 && math.abs(Rows<InventorySlot>(em, root).Sum(s => s.LossRemainder) - .8f) < .0001f, "Repeated movement conserves count and accrued loss");
                Reset(Slot(0, 0, 8, .8f), Slot(1, 0, 8, .4f));
                var before = InventoryLayout.Fingerprint(em, root);
                Check(Move(0, 1, 0, 3) == ResultCode.NoCapacity && before == InventoryLayout.Fingerprint(em, root), "Full destination rejects whole requested transfer without partial mutation");
                Reset(Slot(0, 0, 8, .8f), Slot(1, 1, 2, .2f));
                Check(Move(0, 1, 0, 2) != ResultCode.Success, "Partial cross-item swap rejected");
                Check(Move(0, 1, 0, 8) == ResultCode.Success && em.GetBuffer<InventorySlot>(root)[0].Item == ItemId.FromIndex(1) && em.GetBuffer<InventorySlot>(root)[1].LossRemainder == .8f, "Full swap transfers both identities, quantities and debt");
                Reset(Slot(0, 0, 8, .8f, 5), Slot(1, 1, 2));
                before = InventoryLayout.Fingerprint(em, root);
                Check(Move(0, 1, 0, 8) != ResultCode.Success && before == InventoryLayout.Fingerprint(em, root), "Swap checks reverse acceptance before any write");
                Reset(Slot(0, 0, 8), Slot(1, locked: 1));
                Check(Move(0, 1, 0, 2) != ResultCode.Success, "Ruined slot refuses transfer");
                Reset(Slot(0, 0, 8), Slot(1));
                var stale = InventoryLayout.Fingerprint(em, root);
                InventoryOps.Remove(em, root, ItemId.FromIndex(0), 1);
                before = InventoryLayout.Fingerprint(em, root);
                Check(Move(0, 1, 0, 2, stale) != ResultCode.Success && before == InventoryLayout.Fingerprint(em, root), "Stale drag cannot move a changed inventory");
                var state = em.GetComponentData<Session>(root);
                state.Phase = Phase.Night;
                em.SetComponentData(root, state);
                Check(Move(0, 1, 0, 2) == ResultCode.WrongPhase, "Layout commands cannot mutate at night");
                state.Phase = Phase.Day;
                em.SetComponentData(root, state);
                Reset(Slot(0, 0, 8, .8f), Slot(1));
                HistoryOps.Ensure(em, root);
                ResultCode ToPending(int amount, string stamp = null) => InventoryLayout.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.MoveInventoryToPending, SourceProvider = 7, SourceSlot = 0, Item = ItemId.FromIndex(0), Quantity = amount, ExpectedInventory = stamp ?? InventoryLayout.Fingerprint(em, root) });
                Check(ToPending(3) == ResultCode.Success && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 5 && InventoryOps.PendingCount(em, root, ItemId.FromIndex(0)) == 3, "Building stack can move a selected amount into pending pool");
                Check(math.abs(em.GetBuffer<InventorySlot>(root)[0].LossRemainder - .5f) < .00001f && math.abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .3f) < .00001f, "Moving out preserves proportional accrued loss");
                var expired = InventoryLayout.Fingerprint(em, root);
                Check(ToPending(2) == ResultCode.Success && em.GetBuffer<PendingItem>(root).Length == 1, "Moving out merges same resource in pending");
                before = InventoryLayout.Fingerprint(em, root);
                Check(ToPending(1, expired) != ResultCode.Success && before == InventoryLayout.Fingerprint(em, root), "Stale outbound transfer is atomic");
                state.Phase = Phase.Night;
                em.SetComponentData(root, state);
                Check(ToPending(1) == ResultCode.WrongPhase, "Outbound transfer is read-only at night");
                state.Phase = Phase.Day;
                em.SetComponentData(root, state);
                var target = em.GetBuffer<InventorySlot>(root)[1];
                target.Provider = 9;
                var crossSlots = em.GetBuffer<InventorySlot>(root);
                crossSlots[1] = target;
                var returnCommand = new InventoryLayoutRequest
                {
                    Action = InventoryLayoutAction.StorePendingSlot,
                    DestinationProvider = 9,
                    DestinationSlot = 1,
                    Item = ItemId.FromIndex(0),
                    Quantity = 5,
                    ExpectedInventory = InventoryLayout.Fingerprint(em, root)
                };
                Check(InventoryLayout.Execute(em, root, returnCommand) == ResultCode.Success && InventoryOps.Count(em, root, ItemId.FromIndex(0), 9) == 5, "Pending enters a different building by stable key");
                Check(InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 8 && math.abs(Rows<InventorySlot>(em, root).Sum(x => x.LossRemainder) - .8f) < .00001f, "Cross-building round trip conserves stock and loss");
                Reset(Slot(0, 0, 8, .8f));
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = int.MaxValue });
                before = InventoryLayout.Fingerprint(em, root);
                Check(ToPending(1) == ResultCode.NoCapacity && before == InventoryLayout.Fingerprint(em, root), "Pending overflow rejects before any stock mutation");
                Reset(Slot(0, 0, 8, .8f, locked: 1));
                before = InventoryLayout.Fingerprint(em, root);
                Check(ToPending(1) == ResultCode.InvalidTarget && before == InventoryLayout.Fingerprint(em, root), "Lost building stock cannot be rescued through pending");
                Reset(Slot(0, 0, 8, .8f));
                var outbound = new InventoryLayoutRequest
                {
                    Action = InventoryLayoutAction.MoveInventoryToPending,
                    SourceProvider = 7,
                    SourceSlot = 0,
                    Item = ItemId.FromIndex(0),
                    Quantity = 8,
                    ExpectedInventory = InventoryLayout.Fingerprint(em, root)
                };
                using (HistoryOps.ForRequest(em, root, new QueuedGameplayRequest { Kind = outbound.Kind, Target = outbound.Target }))
                    Check(InventoryCommandHandler.Execute(em, root, outbound) == ResultCode.Success, "Outbound command routes through inventory domain");
                var history = Rows<HistoryEntry>(em, root);
                Check(history.Length >= 2 && history[history.Length - 1].Transfer == 1 && history[history.Length - 2].Transfer == 1 && history[history.Length - 1].Delta + history[history.Length - 2].Delta == 0, "Outbound history records a balanced transfer, not production or discard");
                Check(em.GetBuffer<InventorySlot>(root)[0].Item == ItemId.None && em.GetBuffer<InventorySlot>(root)[0].LossRemainder == 0, "Full outbound transfer clears the empty source");
                Reset(Slot(0, 0, 8, .8f), Slot(1, 0, 2, .2f), Slot(2, type: 5));
                Check(InventoryLayout.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.SortInventory, ExpectedInventory = InventoryLayout.Fingerprint(em, root) }) == ResultCode.Success, "One-click sort commits");
                Check(em.GetBuffer<InventorySlot>(root)[2].Count == 10 && math.abs(Rows<InventorySlot>(em, root).Sum(s => s.LossRemainder) - 1) < .00001f, "Sort consolidates into lowest-loss storage without erasing debt");
                var sorted = InventoryLayout.Fingerprint(em, root);
                InventoryLayout.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.SortInventory, ExpectedInventory = sorted });
                Check(sorted == InventoryLayout.Fingerprint(em, root), "Sort is idempotent");
                Reset(Slot(0, 0, 8), Slot(1, 1, 10));
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = 6, LossRemainder = .6f });
                var command = new InventoryLayoutRequest
                {
                    Action = InventoryLayoutAction.StorePendingSlot,
                    DestinationProvider = 7,
                    DestinationSlot = 0,
                    Item = ItemId.FromIndex(0),
                    Quantity = 3,
                    ExpectedInventory = InventoryLayout.Fingerprint(em, root)
                };
                before = InventoryLayout.Fingerprint(em, root);
                Check(InventoryLayout.Execute(em, root, command) == ResultCode.NoCapacity && before == InventoryLayout.Fingerprint(em, root), "Explicit pending transfer is atomic on insufficient room");
                command.Quantity = 2;
                Check(InventoryLayout.Execute(em, root, command) == ResultCode.Success && InventoryOps.PendingCount(em, root, ItemId.FromIndex(0)) == 4, "Selected pending quantity enters chosen slot");
                Check(math.abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .4f) < .00001f && math.abs(em.GetBuffer<InventorySlot>(root)[0].LossRemainder - .2f) < .00001f, "Pending partial transfer conserves both debt portions");
                Reset(Slot(0, 0, 8));
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = 6, LossRemainder = .6f });
                InventoryLayout.StoreAllPending(em, root);
                Check(InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 10 && InventoryOps.PendingCount(em, root, ItemId.FromIndex(0)) == 4, "Store-all fills only available space and keeps overflow");
                var discarded = new InventoryLayoutRequest
                {
                    Action = InventoryLayoutAction.DiscardPending,
                    Item = ItemId.FromIndex(0),
                    Quantity = 1,
                    ExpectedInventory = InventoryLayout.Fingerprint(em, root)
                };
                Check(InventoryLayout.Execute(em, root, discarded) == ResultCode.Success && InventoryLayout.Execute(em, root, discarded) != ResultCode.Success, "Discard confirmation cannot replay against modified pool");
                Reset(Slot(0, 1, 3, .3f));
                var warehouse = em.CreateEntity();
                em.AddComponentData(warehouse, new BuildingDefinitionRef { Definition = BuildingId.FromIndex(0) });
                em.AddComponentData(warehouse, new Identity { Id = 7 });
                {
                    em.AddComponentData(warehouse, new Building { Level = 1, Stage = LifeStage.Operational });
                    em.AddComponentData(warehouse, new BuildingPlacementState() { });
                    em.AddComponentData(warehouse, new BuildingAppearanceState() { });
                    em.AddComponentData(warehouse, new BuildingConstructionState() { });
                    em.AddComponentData(warehouse, new BuildingWorkforceState() { });
                    em.AddComponentData(warehouse, new BuildingHousingState() { });
                    em.AddComponentData(warehouse, new BuildingProductionState() { });
                    em.AddComponentData(warehouse, new BuildingFarmingState() { });
                    em.AddComponentData(warehouse, new BuildingSanctumState() { });
                    em.AddComponentData(warehouse, new BuildingGatheringState() { });
                    em.AddComponentData(warehouse, new BuildingRecruitmentState() { });
                    em.AddComponentData(warehouse, new BuildingMarketState() { });
                    em.AddComponentData(warehouse, new BuildingExperienceState() { });
                    em.AddComponentData(warehouse, new BuildingMaintenanceState() { });
                }

                InventoryProviders.Provision(em, root, warehouse);
                Check(em.GetBuffer<InventorySlot>(root)[0].SlotType == StorageSlotId.FromIndex(1) && em.GetBuffer<InventorySlot>(root)[0].Count == 0 && InventoryOps.PendingCount(em, root, ItemId.FromIndex(1)) == 3, "Changed slot type relocates newly incompatible contents to pending");
                Check(math.abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .3f) < .00001f, "Capacity/type reconciliation retains debt");
                var staffing = em.GetComponentData<Building>(warehouse);
                BuildingWorkforceState staffingWorkforce = em.GetComponentData<BuildingWorkforceState>(warehouse);
                BuildingMaintenanceState staffingMaintenance = em.GetComponentData<BuildingMaintenanceState>(warehouse);
                staffingWorkforce.Workers = 2;
                staffingMaintenance.Maintained = 1;
                {
                    em.SetComponentData(warehouse, staffing);
                    em.SetComponentData(warehouse, staffingWorkforce);
                    em.SetComponentData(warehouse, staffingMaintenance);
                }

                Reset(Slot(0, 0, 5, .2f, 5));
                em.GetBuffer<OwnedBuff>(root).Add(new OwnedBuff { Buff = BuffId.FromIndex(0), Level = 1 });
                Check(math.abs(InventoryStorage.LossRate(em, root, em.GetBuffer<InventorySlot>(root)[0], ItemId.FromIndex(0)) - .025f) < .00001f, "Displayed/automatic loss rate shares slot and buff modifiers");
                InventoryDecay.Settle(em, root);
                Check(math.abs(em.GetBuffer<InventorySlot>(root)[0].LossRemainder - .325f) < .00001f, "Actual loss uses shared quoted rate");
                staffingWorkforce.Workers = 1;
                {
                    em.SetComponentData(warehouse, staffing);
                    em.SetComponentData(warehouse, staffingWorkforce);
                    em.SetComponentData(warehouse, staffingMaintenance);
                }

                Check(math.abs(InventoryStorage.LossRate(em, root, em.GetBuffer<InventorySlot>(root)[0], ItemId.FromIndex(0)) - .05f) < .00001f, "Worker shortage immediately affects shared loss rate");
                staffingMaintenance.Maintained = 0;
                {
                    em.SetComponentData(warehouse, staffing);
                    em.SetComponentData(warehouse, staffingWorkforce);
                    em.SetComponentData(warehouse, staffingMaintenance);
                }

                Check(math.abs(InventoryStorage.LossRate(em, root, em.GetBuffer<InventorySlot>(root)[0], ItemId.FromIndex(0)) - .15f) < .00001f, "Maintenance failure combines with workforce and buff loss modifiers");
                staffingWorkforce.Workers = 2;
                staffingMaintenance.Maintained = 1;
                {
                    em.SetComponentData(warehouse, staffing);
                    em.SetComponentData(warehouse, staffingWorkforce);
                    em.SetComponentData(warehouse, staffingMaintenance);
                }

                {
                    em.AddComponentData(warehouse, new BuildingHousingStats { });
                    em.AddComponentData(warehouse, new BuildingWorkforceStats() { Capacity = 2 });
                    em.AddComponentData(warehouse, new BuildingStorageStats() { });
                    em.AddComponentData(warehouse, new BuildingGarrisonStats() { });
                    em.AddComponentData(warehouse, new BuildingQuestStats() { });
                    em.AddComponentData(warehouse, new BuildingIntelligenceStats() { });
                    em.AddComponentData(warehouse, new BuildingSanctumStats() { });
                    em.AddComponentData(warehouse, new BuildingRangeStats() { });
                    em.AddComponentData(warehouse, new BuildingNavigationStats() { });
                    em.AddComponentData(warehouse, new BuildingBellStats() { });
                }

                em.AddBuffer<NightWave>(root);
                {
                    em.AddComponentData(root, new NightSettings { });
                    em.AddComponentData(root, new CurrencySettings() { Gold = ItemId.FromIndex(0) });
                    em.AddComponentData(root, new IntelligenceSettings() { });
                }

                Reset(Slot(0, 0, 5, .2f, 5), Slot(1, 1, 6, .6f));
                Check(GameRequestExecution.Execute(em, root, new ChangeWorkersRequest { Building = 7, Delta = -1 }) == ResultCode.Success && InventoryLayout.SlotIndex(em, root, 7, 1) < 0, "Actual worker command immediately removes threshold-dependent slot");
                Check(em.GetBuffer<InventorySlot>(root)[0].Index == 0 && em.GetBuffer<InventorySlot>(root)[0].LossRemainder == .2f && InventoryOps.PendingCount(em, root, ItemId.FromIndex(1)) == 6 && math.abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .6f) < .00001f, "Capacity shrink preserves surviving key and pending quantity/debt");
                staffingWorkforce.Workers = 2;
                {
                    em.SetComponentData(warehouse, staffing);
                    em.SetComponentData(warehouse, staffingWorkforce);
                    em.SetComponentData(warehouse, staffingMaintenance);
                }

                InventoryProviders.Provision(em, root, warehouse);
                Check(InventoryLayout.SlotIndex(em, root, 7, 1) >= 0 && em.GetBuffer<InventorySlot>(root)[1].Count == 0 && InventoryOps.PendingCount(em, root, ItemId.FromIndex(1)) == 6, "Restored capacity reuses stable key without silently consuming pending pool");
                InventoryLayout.StoreAllPending(em, root);
                Check(InventoryOps.PendingCount(em, root, ItemId.FromIndex(1)) == 0 && math.abs(em.GetBuffer<InventorySlot>(root)[1].LossRemainder - .6f) < .00001f, "Regained capacity can explicitly store overflow with retained debt");
                var lockedSlot = em.GetBuffer<InventorySlot>(root)[0];
                lockedSlot.Unavailable = 1;
                var lockedBuffer = em.GetBuffer<InventorySlot>(root);
                lockedBuffer[0] = lockedSlot;
                staffing.RuinPending = 1;
                staffing.Stage = LifeStage.Ruined;
                {
                    em.SetComponentData(warehouse, staffing);
                    em.SetComponentData(warehouse, staffingWorkforce);
                    em.SetComponentData(warehouse, staffingMaintenance);
                }

                before = InventoryLayout.Fingerprint(em, root);
                InventoryProviders.Provision(em, root, warehouse);
                Check(before == InventoryLayout.Fingerprint(em, root), "Pending night ruin does not relocate already lost stock into safety");
                Reset(Slot(0));
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = 0 });
                InventoryLayout.StoreAllPending(em, root);
                Check(em.GetBuffer<PendingItem>(root).Length == 0, "Empty pending row is safely removed without nonfinite debt");
                Reset(Slot(0, 0, 1));
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = 0 });
                Check(InventoryOps.RemoveWithPending(em, root, ItemId.FromIndex(0), 1) && em.GetBuffer<PendingItem>(root).Length == 0 && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 0, "Repair payment tolerates empty pending row without division by zero");
                var groups = ScriptableObject.CreateInstance<ItemGroupCatalogAsset>();
                var group = ScriptableObject.CreateInstance<ItemGroupDefinitionAsset>();
                try
                {
                    group.Metadata.Id = "food";
                    group.ParentGroup = group;
                    groups.Definitions = new[]
                    {
                        group
                    };
                    var rejected = false;
                    try
                    {
                        using var invalid = ItemGroupCatalogCompiler.Build(groups);
                    }
                    catch (InvalidOperationException)
                    {
                        rejected = true;
                    }

                    Check(rejected, "Baking validation rejects cyclic item groups");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(group);
                    UnityEngine.Object.DestroyImmediate(groups);
                }
            }
            finally
            {
            }
        }

        static void Map(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Inventory baked map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                InvitationExpeditionVerification.FixturePermissions(em, root);
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                var slots = em.GetBuffer<InventorySlot>(root);
                Check(slots.Length >= 2, "Map supplies multiple real inventory slots");
                for (var i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    s.Item = ItemId.None;
                    s.Count = 0;
                    s.LossRemainder = 0;
                    slots[i] = s;
                }

                var first = slots[0];
                first.Item = gold;
                first.Count = 8;
                first.LossRemainder = .8f;
                slots[0] = first;
                var second = slots[1];
                var command = new InventoryLayoutRequest
                {
                    Action = InventoryLayoutAction.MoveInventory,
                    SourceProvider = first.Provider,
                    SourceSlot = first.Index,
                    DestinationProvider = second.Provider,
                    DestinationSlot = second.Index,
                    Item = gold,
                    Quantity = 3,
                    ExpectedInventory = InventoryLayout.Fingerprint(em, root)
                };
                Check(GameRequestExecution.Execute(em, root, command) == ResultCode.Success, "Real command processor dispatches stable-key transfer");
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 5, LossRemainder = .5f });
                var snapshot = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                var before = InventoryLayout.Fingerprint(em, root);
                SnapshotCodec.Restore(em, root, snapshot);
                Check(before == InventoryLayout.Fingerprint(em, root), "Version-five restore retains slot layout and pending debt");
                var invalid = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                invalid.Inventory[0].Count = int.MaxValue;
                var failed = false;
                try
                {
                    SnapshotCodec.Restore(em, root, invalid);
                }
                catch (InvalidDataException)
                {
                    failed = true;
                }

                Check(failed && before == InventoryLayout.Fingerprint(em, root), "Overfull snapshot is rejected without changing live inventory");
                invalid = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                invalid.Pending[0].LossRemainder = float.NaN;
                failed = false;
                try
                {
                    SnapshotCodec.Restore(em, root, invalid);
                }
                catch (InvalidDataException)
                {
                    failed = true;
                }

                Check(failed && before == InventoryLayout.Fingerprint(em, root), "Nonfinite pending loss is rejected atomically");
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && before == InventoryLayout.Fingerprint(em, root), "New inventory schema works inside forecast transaction");
                var historyStart = em.GetBuffer<HistoryEntry>(root).Length;
                var storeSelected = new InventoryLayoutRequest
                {
                    Action = InventoryLayoutAction.StorePendingSlot,
                    DestinationProvider = first.Provider,
                    DestinationSlot = first.Index,
                    Item = gold,
                    Quantity = 1,
                    ExpectedInventory = InventoryLayout.Fingerprint(em, root)
                };
                Check(GameRequestExecution.Execute(em, root, storeSelected) == ResultCode.Success, "Real command processor stores selected pending quantity");
                var transfers = Rows<HistoryEntry>(em, root).Skip(historyStart).Where(row => row.Item == gold && row.Delta != 0).ToArray();
                Check(transfers.Length == 2 && transfers.All(row => row.Transfer == 1) && transfers.Sum(row => row.Delta) == 0 && transfers.Any(row => row.Pending != 0) && transfers.Any(row => row.Pending == 0), "Selected pending storage records both sides as a transfer, never economic income");
                var stale = new InventoryLayoutRequest
                {
                    Action = InventoryLayoutAction.DiscardSlot,
                    SourceProvider = first.Provider,
                    SourceSlot = first.Index,
                    Item = gold,
                    Quantity = 1,
                    ExpectedInventory = InventoryLayout.Fingerprint(em, root)
                };
                historyStart = em.GetBuffer<HistoryEntry>(root).Length;
                Check(GameRequestExecution.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.StorePending, ExpectedInventory = InventoryLayout.Fingerprint(em, root) }) == ResultCode.Success, "Real command processor stores all pending quantity");
                transfers = Rows<HistoryEntry>(em, root).Skip(historyStart).Where(row => row.Item == gold && row.Delta != 0).ToArray();
                Check(transfers.Length >= 2 && transfers.All(row => row.Transfer == 1) && transfers.Sum(row => row.Delta) == 0 && transfers.Any(row => row.Pending != 0) && transfers.Any(row => row.Pending == 0), "Store-all pending records balanced transfers, never economic income");
                Check(em.GetComponentData<ManualHistoryContext>(root).Active == 0, "Real pending commands release their manual history context");
                before = InventoryLayout.Fingerprint(em, root);
                Check(GameRequestExecution.Execute(em, root, stale) != ResultCode.Success && before == InventoryLayout.Fingerprint(em, root), "Old discard authorization cannot apply after pool storage");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
