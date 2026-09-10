#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class QuestVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); checks++; log.AppendLine("PASS " + label); }
        [MenuItem("Landsong/ECS/Verification/Quest")]
        public static string Run()
        {
            checks = 0; log = new StringBuilder();
            try
            {
                Configuration();
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes/EntityMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) Map(path);
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/quests-verification.txt", log.ToString()); }
        }
        static void Configuration()
        {
            var c = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset"); QuestContentValidation.Validate(c);
            var quests = c.Content.Where(x => x.Kind == ContentKind.Quest).ToArray();
            Check(quests.Length == 11 && quests.Count(x => x.Flags == 1) == 6 && quests.Count(x => x.Flags == 0) == 4 && quests.Single(x => x.Id == "QM007").Flags == 3, "11 definitions: six mainline, four random, one disabled original placeholder");
            foreach (var d in quests.Where(x => (x.Flags & 2) == 0)) Check(d.Rules.Where(r => QuestOps.Requirement(r.Kind)).All(r => r.Key.Length >= 32), "Authored stable requirement IDs: " + d.Id);
            // Compared against the actual archived eleven assets, not the stale mainline prose.
            void Expected(string id, int duration, int type, string rules) { var d = quests.Single(x => x.Id == id); Check(d.Duration == duration && d.Value == type && string.Join(";", d.Rules.Select(r => (int)r.Kind + ":" + r.Target + ":" + r.Amount + ":" + r.B + ":" + r.C)) == rules, "Legacy requirements, base rewards, penalties, parents and timing: " + id); }
            Expected("main_build_farms_3", 0, 0, "28:main_collect_building_materials:1:0:0;29:小麦:20:0:0;29:卷心菜:10:0:0;33:b农田:3:0:0;40::1:1:0");
            Expected("main_build_residential_houses_3", 0, 0, "28:main_plant_farms_3:1:0:0;32:feature.Technology:1:0:0;29:金币:100:0:0;33:b居民房:3:0:1");
            Expected("main_camera_survey", 0, 0, "29:金币:500:0:0;32:feature.Inventory:1:0:0;37::1:0:0;38::1:0:0");
            Expected("main_collect_building_materials", 0, 0, "28:main_camera_survey:1:0:0;29:泥土:100:0:0;29:原木:100:0:0;29:石头:100:0:0;30:b农田:1:0:0;32:feature.Building:1:0:0;35:泥土:10:0:0;35:原木:10:0:0;35:石头:10:0:0");
            Expected("main_plant_farms_3", 0, 0, "28:main_build_farms_3:1:0:0;29:小麦:200:0:0;29:卷心菜:100:0:0;30:b居民房:1:0:0;34:b农田:3:0:0");
            Expected("main_select_technology", 0, 0, "28:main_build_residential_houses_3:1:0:0;39::1:0:0");
            Expected("QM007", 0, 0, "");
            Expected("random_exploration_supplies", 6, 3, "29:金币:18:0:0;36:原木:5:0:0;36:石头:3:0:0;41:金币:5:0:0");
            Expected("random_supply_soil", 5, 0, "29:金币:15:0:0;36:泥土:10:0:0;41:金币:5:0:0"); // v8 authoring restores base amount; baking applies x3 to all item rules.
            Expected("random_supply_stone", 5, 0, "29:金币:8:0:0;36:石头:5:0:0;41:金币:4:0:0"); // v8 authoring x2, runtime requirement remains 10.
            Expected("random_supply_wood", 5, 0, "29:金币:15:0:0;36:原木:10:0:0;41:金币:3:0:0");
            var clone = ScriptableObject.CreateInstance<GameCatalogAsset>(); clone.Definitions = c.Definitions.Select(UnityEngine.Object.Instantiate).ToArray();
            try
            {
                var d = clone.Definitions[clone.Find("main_camera_survey")].Data; var requirements = d.Rules.Where(r => QuestOps.Requirement(r.Kind)).ToArray(); var key = requirements[1].Key;
                void Invalid(Action action, string label) { action(); var failed = false; try { QuestContentValidation.Validate(clone); } catch (InvalidOperationException) { failed = true; } Check(failed, label); }
                Invalid(() => requirements[1].Key = requirements[0].Key, "Duplicate requirement ID rejected"); requirements[1].Key = key;
                Invalid(() => requirements[1].Key = "", "Missing requirement ID rejected"); requirements[1].Key = key;
                Invalid(() => requirements[1].Amount = 0, "Zero requirement rejected"); requirements[1].Amount = 1;
                var old = d.Rules; d.Rules = old.Concat(new[] { new RuleSource { Kind = RuleKind.Prerequisite, Target = "main_collect_building_materials", Amount = 1 } }).ToArray();
                Invalid(() => { }, "Cyclic mainline dependencies rejected"); d.Rules = old;
                QuestContentValidation.Validate(clone); Check(true, "Valid configuration still accepted after rejected edits");
            }
            finally { foreach (var asset in clone.Definitions) UnityEngine.Object.DestroyImmediate(asset); UnityEngine.Object.DestroyImmediate(clone); }
        }
        static void Map(string path)
        {
            log.AppendLine("MAP " + path); var scene = EditorSceneManager.OpenPreviewScene(path); using var store = new BlobAssetStore(128); using var world = new World("Quest baked map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
                Entity Quest(string name) { using var all = Sim.OrderedEntities<Quest>(em); foreach (var e in all) if (em.GetComponentData<Identity>(e).Definition == catalog.Find(name)) return e; return Entity.Null; }
                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                ResultCode Command(CommandKind kind, Entity e, int arg = 0) => GameLoopSystem.Execute(em, root, new Command { Kind = kind, Target = e == Entity.Null ? 0 : Id(e), Argument = arg });
                void Stock(int item, int count) { InventoryOps.Remove(em, root, item, InventoryOps.Count(em, root, item)); Check(InventoryOps.Add(em, root, item, count) == count, "Fixture normal stock " + item); }
                var camera = Quest("main_camera_survey"); Check(camera != Entity.Null && Quest("QM007") == Entity.Null, "Only actual first mainline discovered");
                Check(QuestOps.Tracking(em, root).Target == Id(camera), "Automatic mainline tracking initialized");
                Check(Quest("main_collect_building_materials") == Entity.Null, "Second mainline requires reward claim");
                Command(CommandKind.CameraMoved, Entity.Null); Check(em.GetComponentData<Quest>(camera).Status == QuestStatus.Active, "Camera move alone does not finish two requirements");
                Command(CommandKind.CameraZoomed, Entity.Null); Check(em.GetComponentData<Quest>(camera).Status == QuestStatus.Completed, "Actual camera commands complete immediately");
                Check(Quest("main_collect_building_materials") == Entity.Null, "Completion without claim keeps next task locked");
                var chainSlot = em.GetComponentData<Quest>(camera);
                chainSlot.ContainerSlot = ProgressionOps.QuestContainerCapacity(em, Sim.Find(em, chainSlot.Container)) - 1;
                em.SetComponentData(camera, chainSlot); // Leave earlier slots free to detect accidental reallocation.
                Check(Command(CommandKind.ClaimQuest, camera) == ResultCode.Success, "First mainline reward claim succeeds");
                var materials = Quest("main_collect_building_materials"); Check(materials != Entity.Null && QuestOps.Tracking(em, root).Target == Id(materials), "Claim discovers and auto-tracks next mainline");
                var nextSlot = em.GetComponentData<Quest>(materials);
                Check(nextSlot.Container == chainSlot.Container && nextSlot.ContainerSlot == chainSlot.ContainerSlot, "Successor inherits exact slot even when earlier slots are free");
                Check(Command(CommandKind.ClaimQuest, camera) == ResultCode.Unavailable, "Already claimed mainline cannot reaward");
                var beforeHold = SnapshotCodec.Capture(em, root); var rules = em.GetBuffer<QuestProgress>(materials).ToNativeArray(Allocator.Temp).ToArray();
                foreach (var p in rules) { var r = Sim.GetRule(em, root, p.RuleIndex); Stock(r.Target, r.Amount); }
                ProgressionOps.EvaluateQuests(em, root); Check(em.GetComponentData<Quest>(materials).Status == QuestStatus.Completed, "Hold requirements complete without submission");
                foreach (var p in rules) { var r = Sim.GetRule(em, root, p.RuleIndex); Check(InventoryOps.Count(em, root, r.Target) >= r.Amount, "Hold requirement does not consume item"); InventoryOps.Remove(em, root, r.Target, r.Amount); }
                ProgressionOps.EvaluateQuests(em, root); Check(em.GetComponentData<Quest>(materials).Status == QuestStatus.Completed, "Completed status is sticky after resource spending");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, beforeHold));
                var core = Entity.Null; using (var buildings = Sim.OrderedEntities<Building>(em)) foreach (var b in buildings) if (em.GetComponentData<BuildingStats>(b).IsCore != 0) core = b;
                var offer = ProgressionOps.CreateQuest(em, root, catalog.Find("random_exploration_supplies"), Id(core)); var offerId = Id(offer);
                Check(em.GetComponentData<Quest>(offer).StartTurn == 0 && em.GetComponentData<Quest>(offer).Deadline == 0 && ProgressionOps.QuestCount(em) == 1, "Offered task has no timer or capacity consumption");
                Check(Command(CommandKind.TrackQuest, offer, 1) == ResultCode.InvalidTarget, "Offered task cannot be pinned");
                Check(Command(CommandKind.AcceptQuest, offer) == ResultCode.Success && ProgressionOps.QuestCount(em) == 2, "Accept starts active task and consumes one capacity");
                Check(Command(CommandKind.TrackQuest, offer, 1) == ResultCode.Success && QuestOps.Tracking(em, root).Target == offerId, "Manual tracking overrides automatic mainline");
                using var copy = em.GetBuffer<QuestProgress>(offer).ToNativeArray(Allocator.Temp); var requirements = copy.ToArray();
                Check(requirements.Length == 2 && requirements[0].Key != requirements[1].Key, "Grouped legacy submit produces two stable individual requirements");
                foreach (var p in requirements) { var r = Sim.GetRule(em, root, p.RuleIndex); Stock(r.Target, r.Amount); }
                var first = requirements[0]; var rule = Sim.GetRule(em, root, first.RuleIndex); var quote = QuestOps.Submission(em, root, offer, first.Key.ToString());
                var submit = new Command { Kind = CommandKind.SubmitQuest, Target = offerId, Definition = quote.Item, Amount = 1, Text = first.Key + "|" + quote.Stamp };
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.SubmitQuest, Target = offerId }) == ResultCode.InvalidTarget, "Legacy batch submit is refused");
                Check(GameLoopSystem.Execute(em, root, submit) == ResultCode.Success && em.GetBuffer<QuestProgress>(offer)[0].Amount == 1 && em.GetBuffer<QuestProgress>(offer)[1].Amount == 0, "Selected quantity only increments selected requirement");
                Check(InventoryOps.Count(em, root, rule.Target) == rule.Amount - 1, "Submission removes precisely one normal item");
                Check(GameLoopSystem.Execute(em, root, submit) == ResultCode.Unavailable, "Repeated stale confirmation cannot charge twice");
                var original = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); offer = Sim.Find(em, offerId);
                Check(original.SequenceEqual(SnapshotCodec.Capture(em, root)) && QuestOps.Tracking(em, root).Target == offerId && em.GetBuffer<QuestProgress>(offer)[0].Amount == 1, "v7 roundtrip preserves partial submit and tracking");
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Forecast preserves quests, tracking, inventory and RNG");
                var rollback = SnapshotCodec.Decode(em, root, original); rollback.Tracking = new QuestTracking { Mode = 2 };
                var rollbackFailed = false; try { SnapshotCodec.Restore(em, root, rollback, probe: stage => { if (stage == "root-published") throw new InvalidOperationException("Injected quest tracking publication fault"); }); } catch (InvalidOperationException) { rollbackFailed = true; }
                Check(rollbackFailed && original.SequenceEqual(SnapshotCodec.Capture(em, root)) && em.Exists(offer), "Published root failure restores original tracking, partial progress and Entity handles");
                void Reject(Action<SnapshotCodec.Snapshot> mutate, string label) { var invalid = SnapshotCodec.Decode(em, root, original); mutate(invalid); var failed = false; try { SnapshotCodec.Restore(em, root, invalid); } catch (InvalidDataException) { failed = true; } Check(failed && original.SequenceEqual(SnapshotCodec.Capture(em, root)), label); }
                Reject(s => s.Tracking.Target = ulong.MaxValue, "Invalid tracking target rejected atomically");
                Reject(s => s.Tracking.Mode = 9, "Invalid tracking mode rejected atomically");
                Reject(s => s.Records.Single(x => x.Identity.Id == offerId).Progress[1].Key = first.Key, "Duplicate saved requirement rejected atomically");
                Reject(s => s.Records.Single(x => x.Identity.Id == offerId).Progress[0].Amount = -1, "Negative saved progress rejected atomically");
                Reject(s => s.Records.Single(x => x.Identity.Id == offerId).Progress = Array.Empty<QuestProgress>(), "Missing saved requirements rejected atomically");
                Reorder(em, root, original, offerId, catalog);
                offer = Sim.Find(em, offerId); var session = em.GetComponentData<Session>(root); session.Phase = Phase.Night; em.SetComponentData(root, session);
                Check(Command(CommandKind.SubmitQuest, offer) == ResultCode.WrongPhase && Command(CommandKind.ClaimQuest, offer) == ResultCode.WrongPhase && Command(CommandKind.AbandonQuest, offer) == ResultCode.WrongPhase, "Night rejects submit, claim and abandon");
                session.Phase = Phase.Day; em.SetComponentData(root, session);
                foreach (var p in requirements)
                {
                    var q = QuestOps.Submission(em, root, offer, p.Key.ToString()); if (q.Maximum == 0) continue;
                    Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.SubmitQuest, Target = offerId, Definition = q.Item, Amount = q.Maximum, Text = p.Key + "|" + q.Stamp }) == ResultCode.Success, "Submit remaining requirement " + p.Key);
                }
                Check(em.GetComponentData<Quest>(offer).Status == QuestStatus.Completed && ProgressionOps.QuestCount(em) == 2, "Completed random task retains capacity until reward");
                Check(Command(CommandKind.AbandonQuest, offer) == ResultCode.Unavailable, "Completed task cannot be abandoned");
                // Force zero capacity without changing provider layout to verify the reward transaction only.
                var slots = em.GetBuffer<InventorySlot>(root); var saved = slots.ToNativeArray(Allocator.Temp).ToArray(); slots.Clear();
                Check(Command(CommandKind.ClaimQuest, offer) == ResultCode.NoCapacity && em.Exists(offer) && em.GetBuffer<InventorySlot>(root).Length == 0, "Full inventory leaves all rewards and completed quest unclaimed");
                Check(QuestOps.Tracking(em,root).Target==offerId,"Failed reward claim retains current tracking");
                slots = em.GetBuffer<InventorySlot>(root); foreach (var s in saved) slots.Add(s);
                Check(Command(CommandKind.ClaimQuest, offer) == ResultCode.Success && !em.Exists(offer), "Random reward claim consumes task once");
                Check(ProgressionOps.QuestCount(em)==1,"Claim releases ordinary slot while mainline retains its shared slot");
                Check(QuestOps.Tracking(em, root).Mode == 0 && QuestOps.Tracking(em, root).Target == Id(Quest("main_collect_building_materials")), "Claimed manual task falls back to highest-value accepted task");
                Command(CommandKind.TrackQuest, Entity.Null, 2); var unpinned = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, unpinned));
                ProgressionOps.EvaluateQuests(em, root); Check(QuestOps.Tracking(em, root).Mode == 2 && QuestOps.Tracking(em, root).Target == 0, "Explicit unpin survives load and evaluation");
                Command(CommandKind.TrackQuest, Entity.Null); Check(QuestOps.Tracking(em, root).Target != 0, "Automatic mainline tracking can be restored");
                // Continue the formal chain with authored buildings and actual requirement evaluation.
                materials = Quest("main_collect_building_materials");
                foreach (var p in em.GetBuffer<QuestProgress>(materials).ToNativeArray(Allocator.Temp).ToArray()) { var r = Sim.GetRule(em, root, p.RuleIndex); Stock(r.Target, r.Amount); }
                Check(Command(CommandKind.ClaimQuest, materials) == ResultCode.Success, "Material mainline reward unlocks farm task");
                Entity MakeBuilding(string name, bool operational)
                {
                    var def = catalog.Find(name); var grid = em.GetComponentData<GridData>(root); var cell = default(Unity.Mathematics.int2); var found = false;
                    for (var y = 0; y < grid.Value.Value.Size.y && !found; y++) for (var x = 0; x < grid.Value.Value.Size.x && !found; x++) { cell = grid.Value.Value.Min + new Unity.Mathematics.int2(x, y); found = GridOps.CanPlace(em, root, def, cell, 0); }
                    Check(found, "Legal chain fixture footprint " + name); return BuildingOps.Create(em, root, def, cell, 0, 1, operational);
                }
                var farms = new[] { MakeBuilding("b农田", false), MakeBuilding("b农田", false), MakeBuilding("b农田", false) }; var farmQuest = Quest("main_build_farms_3"); ProgressionOps.EvaluateQuests(em, root);
                Check(em.GetComponentData<Quest>(farmQuest).Status == QuestStatus.Active && em.GetBuffer<QuestProgress>(farmQuest)[0].Amount == 3, "Construction counts for farm objective but relative turn still required");
                session = em.GetComponentData<Session>(root); session.Turn++; em.SetComponentData(root, session); ProgressionOps.EvaluateQuests(em, root);
                Check(Command(CommandKind.ClaimQuest, farmQuest) == ResultCode.Success, "Relative turn releases next mainline");
                foreach (var b in farms) { var state = em.GetComponentData<Building>(b); state.Stage = LifeStage.Operational; state.Crop = catalog.Find("小麦"); em.SetComponentData(b, state); }
                ProgressionOps.EvaluateQuests(em, root); var cropQuest = Quest("main_plant_farms_3"); Check(em.GetComponentData<Quest>(cropQuest).Status == QuestStatus.Completed, "Three planted farms meet crop objective");
                Check(Command(CommandKind.ClaimQuest, cropQuest) == ResultCode.Success, "Planting reward unlocks residential blueprint and task");
                var houses = new[] { MakeBuilding("b居民房", false), MakeBuilding("b居民房", false), MakeBuilding("b居民房", false) }; var houseQuest = Quest("main_build_residential_houses_3"); ProgressionOps.EvaluateQuests(em, root);
                Check(em.GetComponentData<Quest>(houseQuest).Status == QuestStatus.Active, "Unfinished houses do not satisfy operational-only objective");
                foreach (var b in houses) { var state = em.GetComponentData<Building>(b); state.Stage = LifeStage.Operational; em.SetComponentData(b, state); }
                ProgressionOps.EvaluateQuests(em, root); Check(Command(CommandKind.ClaimQuest, houseQuest) == ResultCode.Success && ResearchOps.Unlocked(em, root), "Completed housing reward grants real technology permission");
                var techQuest = Quest("main_select_technology"); Check(em.GetComponentData<Quest>(techQuest).Status == QuestStatus.Active, "No current research does not satisfy selection objective");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Research, Definition = catalog.Find("TN_3_1_启蒙") }) == ResultCode.Success && em.GetComponentData<Quest>(techQuest).Status == QuestStatus.Completed, "Actual current research command completes final tutorial");
                Check(Command(CommandKind.ClaimQuest, techQuest) == ResultCode.Success && QuestOps.Tracking(em, root).Target == 0, "Formal six-task chain ends with no placeholder or repeated reward");
                Check(QuestOps.RewardValue(em,root,catalog.Find("random_supply_wood"))==15 && QuestOps.RewardValue(em,root,catalog.Find("random_supply_stone"))==16 && QuestOps.RewardValue(em,root,catalog.Find("random_supply_soil"))==45,"Reward value uses priced item amounts after baking multiplier exactly once");
                Check(QuestOps.RewardValue(em,root,catalog.Find("main_select_technology"))==0,"Non-item unlocks do not invent monetary value");
                Entity palace=Entity.Null;using(var buildings=Sim.OrderedEntities<Building>(em))foreach(var e in buildings)if(em.GetComponentData<BuildingStats>(e).IsCore!=0)palace=e;
                var extra=new Entity[3];var names=new[]{"random_supply_wood","random_supply_stone","random_supply_soil"};
                for(var i=0;i<3;i++){extra[i]=ProgressionOps.CreateQuest(em,root,catalog.Find(names[i]),Id(palace),i);Check(Command(CommandKind.AcceptQuest,extra[i])==ResultCode.Success,"Accepted value-ranked fixture "+i);var q=em.GetComponentData<Quest>(extra[i]);q.Status=QuestStatus.Completed;em.SetComponentData(extra[i],q);}
                var valueSorted=extra.OrderBy(e=>e,System.Collections.Generic.Comparer<Entity>.Create((a,b)=>QuestOps.CompareValue(em,root,a,b))).ToArray();
                Check(valueSorted[0]==extra[2] && valueSorted[1]==extra[1] && valueSorted[2]==extra[0],"Shared invitation comparator sorts reward value descending");
                var hs=em.GetComponentData<Quest>(houseQuest);hs.Status=QuestStatus.Completed;
                Check(ProgressionOps.BindQuestContainer(em, ref hs), "Reactivated mainline fixture binds the fourth shared slot");
                em.SetComponentData(houseQuest,hs); em.DestroyEntity(techQuest);
                Command(CommandKind.TrackQuest,houseQuest,1);
                Check(Command(CommandKind.ClaimQuest,houseQuest)==ResultCode.Success,"Claim hands off even when all shared slots are occupied");
                techQuest=Quest("main_select_technology");var ts=em.GetComponentData<Quest>(techQuest);
                Check(ts.Container==hs.Container && ts.ContainerSlot==hs.ContainerSlot && QuestOps.Tracking(em,root).Target==Id(techQuest),"Same-slot successor takes priority over more valuable unrelated accepted tasks");
                Check(Command(CommandKind.ClaimQuest,techQuest)==ResultCode.Success && QuestOps.Tracking(em,root).Target==Id(extra[2]),"No successor follows highest-value accepted task including ready rewards");
                var trackingSave=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,trackingSave));
                Check(trackingSave.SequenceEqual(SnapshotCodec.Capture(em,root)),"Automatic selected target survives snapshot restore");
                using(var tasks=Sim.OrderedEntities<Quest>(em))for(var i=0;i<3;i++)foreach(var e in tasks)if(em.GetComponentData<Identity>(e).Definition==catalog.Find(names[i]))extra[i]=e;
                Command(CommandKind.TrackQuest,extra[0],1);Command(CommandKind.TrackQuest,extra[2],1);Command(CommandKind.TrackQuest,extra[0],2);
                Check(QuestOps.Tracking(em,root).Target==Id(extra[2]),"Late uncheck of another card does not clear new tracked target");
                Command(CommandKind.TrackQuest,extra[2],2);Check(QuestOps.Tracking(em,root).Mode==2 && QuestOps.Tracking(em,root).Target==0,"Targeted uncheck clears matching tracking");
                Command(CommandKind.TrackQuest,extra[0],1);Check(Command(CommandKind.ClaimQuest,extra[1])==ResultCode.Success && QuestOps.Tracking(em,root).Target==Id(extra[0]),"Claiming another card preserves explicit current tracking");
                Command(CommandKind.TrackQuest,Entity.Null,2);Check(Command(CommandKind.ClaimQuest,extra[0])==ResultCode.Success && QuestOps.Tracking(em,root).Mode==2 && QuestOps.Tracking(em,root).Target==0,"Claiming an untracked task respects explicit uncheck");
                Command(CommandKind.TrackQuest,extra[2],1);Check(Command(CommandKind.ClaimQuest,extra[2])==ResultCode.Success && QuestOps.Tracking(em,root).Target==0,"Claiming last accepted task clears HUD without tracking invitations");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void Reorder(EntityManager em, Entity root, byte[] original, ulong questId, GameCatalogAsset source)
        {
            var clone = UnityEngine.Object.Instantiate(source); clone.Definitions = source.Definitions.Select(UnityEngine.Object.Instantiate).ToArray(); var old = em.GetComponentData<ContentCatalog>(root);
            try
            {
                var rules = clone.Definitions[clone.Find("random_exploration_supplies")].Data.Rules; var indices = Enumerable.Range(0, rules.Length).Where(i => QuestOps.Requirement(rules[i].Kind)).ToArray();
                (rules[indices[0]], rules[indices[1]]) = (rules[indices[1]], rules[indices[0]]);
                using var blob = GameWorldAuthoring.BuildCatalog(clone); em.SetComponentData(root, new ContentCatalog { Value = blob });
                var decoded = SnapshotCodec.Decode(em, root, original); var p = decoded.Records.Single(x => x.Identity.Id == questId).Progress;
                Check(p[0].Amount == 1 && p[0].RuleIndex > p[1].RuleIndex, "Stable IDs remap saved progress when authored requirements reorder");
                SnapshotCodec.Restore(em, root, decoded); Check(em.GetBuffer<QuestProgress>(Sim.Find(em, questId))[0].Amount == 1, "Reordered content restores successfully");
                em.SetComponentData(root, old); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            }
            finally { em.SetComponentData(root, old); foreach (var asset in clone.Definitions) UnityEngine.Object.DestroyImmediate(asset); UnityEngine.Object.DestroyImmediate(clone); }
        }
    }
}
#endif
