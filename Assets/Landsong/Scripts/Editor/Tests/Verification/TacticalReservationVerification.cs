#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    public static class TacticalReservationVerification
    {
        struct Reservation
        {
            public float3 Point;
            public float Radius;
        }

        [MenuItem("Landsong/ECS/Verification/Tactical reservations and timings")]
        public static string Run()
        {
            var report = new StringBuilder("Started: " + DateTimeOffset.Now.ToString("O") + "\n");
            report.AppendLine("Isolated Editor World, no jobs/rendering/saves. 5 warmups, 25 measured updates per size. Timings include query, stable sorting and native allocation; fixture/oracle excluded. No FPS or before/after claim.");
            int assertions = 0;
            void Check(bool value, string label)
            {
                assertions++;
                if (!value)
                    throw new InvalidOperationException(label);
            }

            try
            {
                foreach (var count in new[]
                {
                    20,
                    50,
                    100,
                    200
                }

                )
                {
                    using var builder = new BlobBuilder(Allocator.Temp);
                    ref var blob = ref builder.ConstructRoot<GridBlob>();
                    blob.Min = new int2(-40);
                    blob.Size = new int2(80);
                    var cells = builder.Allocate(ref blob.Cells, 6400);
                    for (int i = 0; i < cells.Length; i++)
                        cells[i] = new GridCell
                        {
                            Exists = 1,
                            Traversable = 1
                        };
                    using var gridBlob = builder.CreateBlobAssetReference<GridBlob>(Allocator.Persistent);
                    using var world = new World("Owned tactical reservation verification " + count);
                    var em = world.EntityManager;
                    var root = em.CreateEntity(typeof(Session), typeof(GridData));
                    var grid = new GridData
                    {
                        Value = gridBlob,
                        CellSize = 1
                    };
                    em.SetComponentData(root, grid);
                    {
                        em.SetComponentData(root, new Session { Phase = Phase.Night });
                        EntityState.Set(em, root, new GameClock() { Time = 1 });
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

                    em.AddBuffer<Occupancy>(root).ResizeUninitialized(cells.Length);
                    var occupied = em.GetBuffer<Occupancy>(root);
                    for (int i = 0; i < occupied.Length; i++)
                        occupied[i] = default;
                    var targets = new Entity[(count + 19) / 20];
                    for (int i = 0; i < targets.Length; i++)
                    {
                        var cell = new int2(-28 + i % 4 * 16, -20 + i / 4 * 20);
                        var target = em.CreateEntity(typeof(Identity), typeof(Health), typeof(LocalTransform));
                        targets[i] = target;
                        em.SetComponentData(target, new Identity { Id = (ulong)(10000 + i) });
                        em.SetComponentData(target, new Health { Current = 100 });
                        em.SetComponentData(target, LocalTransform.FromPosition(GridOps.Position(grid, cell, new int2(1))));
                        if (i % 2 == 0)
                        {
                            em.AddComponentData(target, new Building { });
                            em.AddComponentData(target, new BuildingPlacementState() { Cell = cell, Size = new int2(4) });
                            em.AddComponentData(target, new BuildingAppearanceState() { });
                            em.AddComponentData(target, new BuildingConstructionState() { });
                            em.AddComponentData(target, new BuildingWorkforceState() { });
                            em.AddComponentData(target, new BuildingHousingState() { });
                            em.AddComponentData(target, new BuildingProductionState() { });
                            em.AddComponentData(target, new BuildingFarmingState() { });
                            em.AddComponentData(target, new BuildingSanctumState() { });
                            em.AddComponentData(target, new BuildingGatheringState() { });
                            em.AddComponentData(target, new BuildingRecruitmentState() { });
                            em.AddComponentData(target, new BuildingMarketState() { });
                            em.AddComponentData(target, new BuildingExperienceState() { });
                            em.AddComponentData(target, new BuildingMaintenanceState() { });
                        }
                        else
                            em.AddComponentData(target, new Combatant { Profile = new CombatProfile { BodyRadius = .4f } });
                    }

                    for (int i = 0; i < count; i++)
                    {
                        var target = targets[i / 20];
                        var unit = em.CreateEntity(typeof(Identity), typeof(Health), typeof(LocalTransform), typeof(Combatant), typeof(TacticalState), typeof(Perception), typeof(UnitOrder), typeof(NavigationState), typeof(SimulationOwner));
                        var position = EntityState.Position(em, target) + new float3(i % 5 - 2, .5f, i % 7 - 3);
                        var profile = CombatProfile.Default;
                        profile.BodyRadius = i % 4 == 0 ? 3.8f : i % 4 == 1 ? 1.2f : .35f;
                        profile.ChaseRadius = 128;
                        profile.ChaseSeconds = 120;
                        em.SetComponentData(unit, new Identity { Id = (ulong)(count - i) });
                        em.SetComponentData(unit, new Health { Current = 10 });
                        em.SetComponentData(unit, LocalTransform.FromPosition(position));
                        em.SetComponentData(unit, new SimulationOwner { Root = root });
                        em.SetComponentData(unit, new Combatant { Faction = 1, Deployed = 1, Range = 5, Profile = profile });
                        em.SetComponentData(unit, new Perception { Enemy = target });
                    }

                    var otherRootUnit = em.CreateEntity(typeof(Identity), typeof(Health), typeof(Combatant), typeof(TacticalState), typeof(SimulationOwner));
                    em.SetComponentData(otherRootUnit, new Identity { Id = 50000 });
                    em.SetComponentData(otherRootUnit, new Health { Current = 10 });
                    em.SetComponentData(otherRootUnit, new SimulationOwner { Root = Entity.Null });
                    em.SetComponentData(otherRootUnit, new Combatant { Deployed = 1 });
                    TacticalOps.Update(em, root);
                    occupied = em.GetBuffer<Occupancy>(root);
                    Check(em.GetComponentData<TacticalState>(otherRootUnit).HasSlot == 0, "Other session is excluded");
                    var reserved = new List<Reservation>();
                    using (var ordered = WorldQueries.OrderedEntities<Combatant>(em))
                        foreach (var unit in ordered)
                        {
                            if (unit == otherRootUnit || !em.HasComponent<TacticalState>(unit))
                                continue;
                            var actor = em.GetComponentData<Combatant>(unit);
                            var target = em.GetComponentData<Perception>(unit).Enemy;
                            var position = EntityState.Position(em, unit);
                            float best = float.MaxValue;
                            float3 expected = position;
                            bool hasSlot = false;
                            void Consider(float3 point)
                            {
                                if (!GridOps.Traversable(grid, occupied, GridOps.Cell(grid, point)))
                                    return;
                                foreach (var prior in reserved)
                                    if (math.distance(point.xz, prior.Point.xz) < actor.Profile.BodyRadius + prior.Radius)
                                        return;
                                var score = math.distancesq(position, point);
                                if (score < best)
                                {
                                    best = score;
                                    expected = point;
                                    hasSlot = true;
                                }
                            }

                            if (em.HasComponent<Building>(target))
                            {
                                BuildingPlacementState buildingPlacement = em.GetComponentData<BuildingPlacementState>(target);
                                for (int y = -1; y <= buildingPlacement.Size.y; y++)
                                    for (int x = -1; x <= buildingPlacement.Size.x; x++)
                                        if (x < 0 || y < 0 || x >= buildingPlacement.Size.x || y >= buildingPlacement.Size.y)
                                            Consider(GridOps.Position(grid, buildingPlacement.Cell + new int2(x, y), new int2(1)) + new float3(0, .5f, 0));
                            }
                            else
                            {
                                var center = EntityState.Position(em, target);
                                var body = em.GetComponentData<Combatant>(target).Profile.BodyRadius;
                                for (int i = 0; i < 16; i++)
                                {
                                    float angle = i * math.PI / 8;
                                    Consider(center + new float3(math.cos(angle), 0, math.sin(angle)) * (body + math.max(actor.Profile.BodyRadius, actor.Range * .75f)));
                                }
                            }

                            var actual = em.GetComponentData<TacticalState>(unit);
                            Check((actual.HasSlot != 0) == hasSlot && math.all(actual.Engagement == expected), "Spatial reservations agree with brute-force scan at " + count + " units, ID " + em.GetComponentData<Identity>(unit).Id);
                            if (hasSlot)
                                reserved.Add(new Reservation { Point = expected, Radius = actor.Profile.BodyRadius });
                        }

                    for (int i = 0; i < 5; i++)
                        TacticalOps.Update(em, root);
                    var ticks = new long[25];
                    long allocation = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < ticks.Length; i++)
                    {
                        long start = Stopwatch.GetTimestamp();
                        TacticalOps.Update(em, root);
                        ticks[i] = Stopwatch.GetTimestamp() - start;
                    }

                    allocation = GC.GetAllocatedBytesForCurrentThread() - allocation;
                    Array.Sort(ticks);
                    double medianMs = ticks[ticks.Length / 2] * 1000d / Stopwatch.Frequency;
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "PASS units={0}; p50={1:F3} ms/update; managed={2:F0} bytes/update; reserved={3}", count, medianMs, allocation / 25d, reserved.Count));
                }

                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine("FAIL " + error);
                throw;
            }
            finally
            {
                report.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O"));
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/tactical-reservation-verification.txt", report.ToString());
            }
        }

        public static void Batch() => UnityEngine.Debug.Log(Run());
    }
}
#endif
