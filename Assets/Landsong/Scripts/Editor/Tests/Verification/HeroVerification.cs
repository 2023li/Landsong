#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class HeroVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); checks++; log.AppendLine("PASS " + label); }
        [MenuItem("Landsong/ECS/Verification/Hero")]
        public static string Run()
        {
            log = new StringBuilder(); checks = 0;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity"); using var store = new BlobAssetStore(128); using var world = new World("Hero verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var s = em.GetComponentData<Session>(root); s.BasePopulation += 100; s.CheckpointPending = 0; em.SetComponentData(root, s);
                int definition = Sim.FindDefinition(em, root, "b泰坦神殿"); var grid = em.GetComponentData<GridData>(root); Entity temple = Entity.Null;
                for (int y = 15; y < grid.Value.Value.Size.y - 15 && temple == Entity.Null; y++) for (int x = 15; x < grid.Value.Value.Size.x - 15; x++) if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0)) { temple = BuildingOps.Create(em, root, definition, new int2(x, y), 0, 1, true); break; }
                Check(temple != Entity.Null, "Legal temple fixture"); ulong siteId = em.GetComponentData<Identity>(temple).Id;
                int gold = em.GetComponentData<GameSettings>(root).Gold; Entity core = Entity.Null; using (var buildings = Sim.Entities<Building>(em)) foreach (var e in buildings) if (em.GetComponentData<BuildingStats>(e).IsCore != 0) core = e;
                for (int i = 0; i < 30; i++) em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = em.GetComponentData<Identity>(core).Id, Index = 5000 + i, SlotType = -1, Item = gold, Count = Sim.Definition(em, root, gold).Capacity });
                Check(MilitaryOps.HeroAvailability(em, root, temple, false).Contains("工人"), "Recruit quote explains worker shortage");
                var b = em.GetComponentData<Building>(temple); b.Workers = 30; em.SetComponentData(temple, b);
                var beforeRecruit = SnapshotCodec.Capture(em, root);
                Check(MilitaryOps.Recruit(em, root, new Command { Target = siteId }, true, at => { throw new InvalidOperationException("probe"); }) == ResultCode.PreparationFailed, "Recruitment after-charge failure is handled");
                Check(beforeRecruit.SequenceEqual(SnapshotCodec.Capture(em, root)), "Recruitment failure restores population identity and inventory");
                Check(MilitaryOps.Recruit(em, root, new Command { Target = siteId }, true) == ResultCode.Success, "Recruit legal persistent hero");
                Entity hero; using (var heroes = Sim.Entities<Hero>(em)) hero = heroes[0]; ulong id = em.GetComponentData<Identity>(hero).Id;
                Check(!em.HasComponent<Soldier>(hero), "Hero stays outside soldier pool");
                Check(MilitaryOps.HeroAvailability(em, root, temple, false) == "已招募", "Quote explains duplicate recruit");
                var saved = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved)); hero = Sim.Find(em, id); temple = Sim.Find(em, siteId);
                Check(em.HasComponent<HeroCombat>(hero) && em.GetComponentData<HeroCombat>(hero).EffectiveSeconds == 0, "Restore rebuilds empty transient combat intervals");
                var malformed = SnapshotCodec.Decode(em, root, saved); malformed.Records.First(r => (r.Mask & 8) != 0).Hero.Experience = -1;
                bool rejected = false; try { SnapshotCodec.Restore(em, root, malformed); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Invalid hero experience rejected without mutation");
                foreach (var fail in new[] { "root-published", "before-retire" })
                { bool threw = false; try { SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved), probe: at => { if (at == fail) throw new InvalidOperationException("probe"); }); } catch (InvalidOperationException) { threw = true; } Check(threw && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Hero restore rollback " + fail); }
                s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; s.NightKind = NightKind.Peaceful; em.SetComponentData(root, s);
                int funds = InventoryOps.Count(em, root, gold);
                Check(MilitaryOps.Wake(em, root, temple) == ResultCode.Unavailable && funds == InventoryOps.Count(em, root, gold), "Unpaid offering cannot wake or charge");
                b = em.GetComponentData<Building>(temple); b.PaidOfferingTurn = s.Turn; em.SetComponentData(temple, b);
                foreach (var fail in new[] { "paid", "deployed" })
                {
                    int before = InventoryOps.Count(em, root, gold), reportCount = em.GetBuffer<BattleReportEntry>(root).Length;
                    Check(MilitaryOps.Wake(em, root, temple, at => { if (at == fail) throw new InvalidOperationException("probe"); }) == ResultCode.PreparationFailed, "Wake fault handled " + fail);
                    Check(before == InventoryOps.Count(em, root, gold) && reportCount == em.GetBuffer<BattleReportEntry>(root).Length && em.GetComponentData<Combatant>(hero).Deployed == 0 && em.GetComponentData<Building>(temple).WokenTurn != s.Turn, "Wake fault restores costs and deployment " + fail);
                }
                Check(MilitaryOps.Wake(em, root, temple) == ResultCode.Success, "Paid hero wakes during peaceful night");
                Check(GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, Sim.Position(em, hero))), "Awakening uses legal perimeter cell");
                funds = InventoryOps.Count(em, root, gold); Check(MilitaryOps.Wake(em, root, temple) == ResultCode.Unavailable && funds == InventoryOps.Count(em, root, gold), "Duplicate awakening cannot charge twice");
                MilitaryOps.RecordHeroContribution(em, root, hero, 10); Check(MilitaryOps.HeroBattleExperience(em, root, hero) == 0, "Peaceful awakening earns no combat experience");
                s.NightKind = NightKind.Invasion; s.Time = 10; em.SetComponentData(root, s);
                Check(MilitaryOps.HeroBattleExperience(em, root, hero) == 0, "No combat effect means zero experience");
                MilitaryOps.RecordHeroContribution(em, root, hero, 0); Check(em.GetComponentData<HeroCombat>(hero).ContactUntil == 0, "Zero-effect support cannot start contact");
                MilitaryOps.RecordHeroContribution(em, root, hero, 1); MilitaryOps.RecordHeroContribution(em, root, hero, 1);
                s.Time = 12; em.SetComponentData(root, s); MilitaryOps.TickHeroExperience(em, root);
                Check(math.abs(em.GetComponentData<HeroCombat>(hero).EffectiveSeconds - 2) < .001f, "Concurrent effects count union time not effect count");
                s.Paused = 1; s.Time = 13; em.SetComponentData(root, s); MilitaryOps.TickHeroExperience(em, root);
                Check(em.GetComponentData<HeroCombat>(hero).EffectiveSeconds == 2, "Pause does not accrue experience");
                s.Paused = 0; s.Time = 30; em.SetComponentData(root, s); MilitaryOps.TickHeroExperience(em, root);
                Check(em.GetComponentData<HeroCombat>(hero).EffectiveSeconds == 3, "Idle time stops at contact grace window");
                int xp = MilitaryOps.HeroBattleExperience(em, root, hero); Check(xp > 0, "Effective contact grants configurable experience");
                var plan = NightPlanOps.State(em, root); int originalThreat = plan.BaseThreat; plan.BaseThreat = 100000; em.SetComponentData(root, plan);
                Check(MilitaryOps.HeroBattleExperience(em, root, hero) >= xp, "Higher event threat increases experience with configured cap"); plan.BaseThreat = originalThreat; em.SetComponentData(root, plan);
                var growth = Sim.Definition(em, root, em.GetComponentData<Identity>(hero).Definition).HeroGrowth;
                Check(MilitaryOps.AddHeroExperience(growth, 0, int.MaxValue) == MilitaryOps.LevelThreshold(growth.Progression, growth.Progression.MaxLevel), "Hero XP saturates safely at maximum level");
                MilitaryOps.ReportHeroExperience(em, root); MilitaryOps.ReportHeroExperience(em, root);
                int reports = 0; foreach (var entry in em.GetBuffer<BattleReportEntry>(root)) if (entry.Kind == EventKind.HeroExperience) reports++;
                Check(reports == 1, "Report preview deduplicates experience");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.SelectHero, Target = id }) == ResultCode.Success, "Select deployed hero");
                var anchor = Sim.Position(em, hero);
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.MoveHero, Position = new float3(float.NaN) }) == ResultCode.InvalidTarget, "Reject nonfinite movement");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.MoveHero, Position = anchor }) == ResultCode.Success, "Hold at legal ground position");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.FocusHero, Target = id }) == ResultCode.Unavailable, "Forced focus fire stays disabled");
                GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.SelectHero });
                Check(math.all(em.GetComponentData<Combatant>(hero).Home == anchor) && em.GetComponentData<UnitOrder>(hero).Kind == OrderKind.Move, "Deselection keeps last positional anchor");
                MilitaryOps.Dawn(em, root); Check(em.GetComponentData<Hero>(hero).Experience == xp, "Dawn commits preview once"); MilitaryOps.Dawn(em, root); Check(em.GetComponentData<Hero>(hero).Experience == xp, "Repeated dawn cannot duplicate XP");
                b = em.GetComponentData<Building>(temple); b.Workers = 0; em.SetComponentData(temple, b); Check(Sim.Alive(em, hero), "Worker shortage does not kill hero");
                GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.SelectHero, Target = id }); MilitaryOps.KillHero(em, root, hero);
                Check(em.GetComponentData<Session>(root).SelectedHero == Entity.Null && em.GetComponentData<Hero>(hero).Experience == 0, "Death clears selection and experience");
                Check(em.GetComponentData<Hero>(hero).DeathPending == 1, "Night death defers cooldown until dawn");
                MilitaryOps.Dawn(em, root); Check(em.GetComponentData<Hero>(hero).CooldownUntil == s.Turn + 1 + Sim.Definition(em, root, em.GetComponentData<Identity>(hero).Definition).Duration, "Full cooldown starts at following dawn");
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { EditorSceneManager.ClosePreviewScene(scene); Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/heroes-verification.txt", log.ToString()); }
        }
    }
}
#endif
