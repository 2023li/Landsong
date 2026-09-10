using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static partial class ProgressionOps
    {
        public static bool Prerequisites(EntityManager em, Entity root, int definition)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind == RuleKind.Prerequisite && !Sim.HasGrant(em, root, r.Target, math.max(1, r.Amount))) return false;
            }
            return true;
        }

        public static bool Reward(EntityManager em, Entity root, int definition, float multiplier = 1, bool pending = false)
        {
            var d = Sim.Definition(em, root, definition);
            using var reward = new InventoryTransaction(em, root);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind != RuleKind.RewardItem) continue;
                var amount = (int)math.floor(r.Amount * multiplier);
                if (InventoryOps.Add(em, root, r.Target, amount, pending) != amount && !pending)
                { reward.Reject("奖励放不下，整份物品奖励未入库"); return false; }
            }
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind == RuleKind.RewardBlueprint || r.Kind == RuleKind.RewardBuff || r.Kind == RuleKind.RewardFeature) Sim.Grant(em, root, r.Target, math.max(1, r.Amount));
            }
            reward.Commit(); return true;
        }

        public static ResultCode Research(EntityManager em, Entity root, int definition, bool cancel)
        {
            return ResearchOps.Command(em, root, definition, cancel);
        }

        static void ResearchTurn(EntityManager em, Entity root)
        {
            ResearchOps.Settle(em, root);
        }

        public static ResultCode Policy(EntityManager em, Entity root, int definition)
        {
            if (!Sim.ValidDefinition(em, root, definition)) return ResultCode.InvalidContent;
            var d = Sim.Definition(em, root, definition);
            if (d.Kind != ContentKind.Policy || !Prerequisites(em, root, definition)) return ResultCode.Unavailable;
            if (em.GetComponentData<Session>(root).PublicOpinion < d.Cost) return ResultCode.Unavailable;
            var policies = em.GetBuffer<PolicyChoice>(root);
            for (var i = 0; i < policies.Length; i++)
            {
                var old = Sim.Definition(em, root, policies[i].Definition);
                if (old.Group == d.Group && old.Level == d.Level) { policies[i] = new PolicyChoice { Definition = definition }; return ResultCode.Success; }
            }
            policies.Add(new PolicyChoice { Definition = definition }); return ResultCode.Success;
        }

        public static int QuestCapacity(EntityManager em)
        {
            var capacity = 0; using var all = Sim.OrderedEntities<Building>(em);
            foreach (var e in all) capacity += QuestContainerCapacity(em, e);
            return capacity;
        }
        public static int QuestContainerCapacity(EntityManager em, Entity e)
        {
            if (!Sim.Operational(em,e) || !em.HasComponent<BuildingStats>(e)) return 0;
            var b=em.GetComponentData<Building>(e); var stats=em.GetComponentData<BuildingStats>(e);
            return b.Maintained!=0 && b.Workers>=stats.JobCapacity ? math.max(0,stats.QuestCapacity) : 0;
        }
        public static bool QuestSlotFree(EntityManager em, ulong container, int slot)
        {
            using var all=Sim.OrderedEntities<Quest>(em);
            foreach(var e in all) {var q=em.GetComponentData<Quest>(e);if((q.Status==QuestStatus.Active || q.Status==QuestStatus.Completed) && q.Container==container && q.ContainerSlot==slot)return false;}
            return true;
        }
        public static bool BindQuestContainer(EntityManager em, ref Quest quest)
        {
            using var buildings=Sim.OrderedEntities<Building>(em);
            // Prefer the invitation's provider, then the first available stable building/slot ID.
            for(var pass=0;pass<2;pass++) foreach(var e in buildings)
            {
                var id=em.GetComponentData<Identity>(e).Id;if((id==quest.Source)!=(pass==0))continue;
                for(var i=0;i<QuestContainerCapacity(em,e);i++) if(QuestSlotFree(em,id,i)){quest.Container=id;quest.ContainerSlot=i;return true;}
            }
            return false;
        }
        public static void ReconcileQuestContainers(EntityManager em, Entity root)
        {
            using var quests=Sim.OrderedEntities<Quest>(em);
            foreach(var e in quests)
            {
                var q=em.GetComponentData<Quest>(e);
                if(q.Status!=QuestStatus.Active && q.Status!=QuestStatus.Completed)continue;
                if(q.Container==0 || q.ContainerSlot<0 || q.ContainerSlot>=QuestContainerCapacity(em,Sim.Find(em,q.Container)))
                    FailQuest(em,root,e,"承接槽位失效",true);
            }
        }
        public static int QuestCount(EntityManager em)
        {
            var count = 0; using var all = Sim.OrderedEntities<Quest>(em);
            foreach (var e in all) { var q = em.GetComponentData<Quest>(e); if (q.Status == QuestStatus.Active || q.Status == QuestStatus.Completed) count++; }
            return count;
        }
        public static Entity CreateQuest(EntityManager em, Entity root, int definition, ulong source = 0, int slot = 0, ulong container = 0, int containerSlot = 0)
        {
            var d = Sim.Definition(em, root, definition); var mainline = (d.Flags & 1) != 0;
            var turn = em.GetComponentData<Session>(root).Turn;
            var accepted = mainline || container != 0;
            var quest = new Quest { Mainline = (byte)(mainline ? 1 : 0), Status = accepted ? QuestStatus.Active : QuestStatus.Offered,
                Source = source, Slot = slot, Container = container, ContainerSlot = containerSlot,
                StartTurn = accepted ? turn : 0, Deadline = accepted && d.Duration > 0 ? turn + d.Duration : 0 };
            if (container != 0)
            {
                if (containerSlot < 0 || containerSlot >= QuestContainerCapacity(em, Sim.Find(em, container)) || !QuestSlotFree(em, container, containerSlot)) return Entity.Null;
            }
            else if(mainline)
            {
                if(quest.Source==0){using var buildings=Sim.OrderedEntities<Building>(em);foreach(var building in buildings)if(em.GetComponentData<BuildingStats>(building).IsCore!=0){quest.Source=em.GetComponentData<Identity>(building).Id;break;}}
                // No entity, timer or ID is allocated until a real shared slot is available.
                if(!BindQuestContainer(em,ref quest))return Entity.Null;
            }
            var e=Sim.Spawn(em,root,definition,default,true);Sim.Set(em,e,quest);
            Sim.Buffer<QuestProgress>(em, e);
            for (var i = 0; i < d.RuleCount; i++) { var rule = Sim.GetRule(em, root, d.RuleStart + i); if (QuestOps.Requirement(rule.Kind)) em.GetBuffer<QuestProgress>(e).Add(new QuestProgress { RuleIndex = d.RuleStart + i, Key = QuestOps.Key(rule, i) }); }
            return e;
        }
        static ulong ContinueQuest(EntityManager em, Entity root, int predecessor, Quest previous)
        {
            var existing = new System.Collections.Generic.HashSet<int>();
            using (var quests = Sim.OrderedEntities<Quest>(em))
                foreach (var e in quests) existing.Add(em.GetComponentData<Identity>(e).Definition);
            var catalog = em.GetComponentData<ContentCatalog>(root).Value;
            var best = -1; long bestValue = -1; var bestReady = false;
            for (var i = 0; i < catalog.Value.Definitions.Length; i++)
            {
                var d = catalog.Value.Definitions[i];
                if (d.Kind != ContentKind.Quest || (d.Flags & 2) != 0 ||
                    (d.Flags & 1) != 0 && existing.Contains(i) ||
                    !QuestOps.HasQuestPredecessor(em, root, i, predecessor)) continue;
                var ready = Prerequisites(em, root, i);
                var value = QuestOps.RewardValue(em, root, i);
                if (best < 0 || ready && !bestReady || ready == bestReady && value > bestValue) { best = i; bestValue = value; bestReady = ready; }
            }
            if (best < 0) return 0;
            // Both ordinary and mainline steps retain their invitation provenance and exact accepted slot.
            var next = CreateQuest(em, root, best, previous.Source, previous.Slot, previous.Container, previous.ContainerSlot);
            if (next == Entity.Null) return 0;
            if (!bestReady)
            {
                // A step with extra prerequisites keeps the slot but starts its timer only when ready.
                var waiting = em.GetComponentData<Quest>(next); waiting.StartTurn = 0; waiting.Deadline = 0;
                em.SetComponentData(next, waiting);
            }
            return em.GetComponentData<Identity>(next).Id;
        }
        public static System.Collections.Generic.List<int> WaitingMainlines(EntityManager em, Entity root)
        {
            var existing=new System.Collections.Generic.HashSet<int>();using(var quests=Sim.OrderedEntities<Quest>(em))foreach(var e in quests)existing.Add(em.GetComponentData<Identity>(e).Definition);
            var waiting=new System.Collections.Generic.List<int>();var catalog=em.GetComponentData<ContentCatalog>(root).Value;
            for(var i=0;i<catalog.Value.Definitions.Length;i++)
            {
                var d=catalog.Value.Definitions[i];if(d.Kind==ContentKind.Quest && (d.Flags&1)!=0 && (d.Flags&2)==0 && !existing.Contains(i) && Prerequisites(em,root,i))waiting.Add(i);
            }
            return waiting;
        }
        public static void DiscoverQuests(EntityManager em, Entity root)
        {
            var phase=em.GetComponentData<Session>(root).Phase;if(phase!=Phase.Day && phase!=Phase.Settlement)return;
            foreach(var definition in WaitingMainlines(em,root))if(CreateQuest(em,root,definition)==Entity.Null)break;
        }
        public static ResultCode QuestCommand(EntityManager em, Entity root, Command command)
        {
            var e = Sim.Find(em, command.Target);
            if (e == Entity.Null || !em.HasComponent<Quest>(e)) return ResultCode.InvalidTarget;
            var q = em.GetComponentData<Quest>(e); var definition = em.GetComponentData<Identity>(e).Definition;
            if (command.Kind == CommandKind.AcceptQuest)
            {
                if (q.Status != QuestStatus.Offered) return ResultCode.Unavailable;
                if (Sim.Find(em, q.Source) == Entity.Null) return ResultCode.InvalidTarget;
                if (QuestCount(em) >= QuestCapacity(em) || !BindQuestContainer(em, ref q)) return ResultCode.QuestOverflow;
                q.Status = QuestStatus.Active; q.StartTurn = em.GetComponentData<Session>(root).Turn;
                var duration = Sim.Definition(em, root, definition).Duration; q.Deadline = duration > 0 ? q.StartTurn + duration : 0;
                em.SetComponentData(e, q); RestartOfferCooldown(em, root, q); EvaluateQuests(em, root); return ResultCode.Success;
            }
            if (command.Kind == CommandKind.RejectQuest || command.Kind == CommandKind.AbandonQuest)
            {
                if (q.Mainline != 0 || (command.Kind == CommandKind.RejectQuest ? q.Status != QuestStatus.Offered : q.Status != QuestStatus.Active)) return ResultCode.Unavailable;
                if (command.Kind == CommandKind.AbandonQuest) return FailQuest(em, root, e, "主动放弃");
                RestartOfferCooldown(em, root, q); em.DestroyEntity(e); return ResultCode.Success;
            }
            if (command.Kind == CommandKind.SubmitQuest)
            {
                return QuestOps.Submit(em, root, e, command);
            }
            if (command.Kind == CommandKind.ClaimQuest)
            {
                if (q.Status != QuestStatus.Active && q.Status != QuestStatus.Completed) return ResultCode.Unavailable;
                EvaluateQuests(em, root); if (!em.Exists(e)) return ResultCode.Unavailable; q = em.GetComponentData<Quest>(e);
                if (q.Status != QuestStatus.Completed) return ResultCode.Unavailable;
                var follow=QuestOps.Tracking(em,root).Target==command.Target;
                if (!Reward(em, root, definition)) return ResultCode.NoCapacity;
                Sim.Grant(em, root, definition);
                var previous = q;
                if (q.Mainline != 0) { q.Status = QuestStatus.Claimed; q.Container=0; q.ContainerSlot=0; em.SetComponentData(e, q); }
                else em.DestroyEntity(e);
                // Hand off before discovery can allocate the vacated slot to another quest.
                var continuation = ContinueQuest(em, root, definition, previous);
                DiscoverQuests(em, root); EvaluateQuests(em, root);
                if(follow)QuestOps.FollowClaim(em,root,definition,continuation);
                return ResultCode.Success;
            }
            return ResultCode.Unavailable;
        }

        public static void TrackInput(EntityManager em, Entity root, RuleKind kind)
        {
            using var all = Sim.OrderedEntities<Quest>(em);
            foreach (var e in all)
            {
                if (em.GetComponentData<Quest>(e).Status != QuestStatus.Active ||
                    !Prerequisites(em, root, em.GetComponentData<Identity>(e).Definition)) continue;
                var progress = em.GetBuffer<QuestProgress>(e);
                for (var i = 0; i < progress.Length; i++) { var p = progress[i]; var rule = Sim.GetRule(em, root, p.RuleIndex); if (rule.Kind == kind && p.Amount < rule.Amount) { p.Amount++; progress[i] = p; } }
            }
        }
        public static void EvaluateQuests(EntityManager em, Entity root)
        {
            ReconcileQuestContainers(em, root);
            DiscoverQuests(em, root);
            using var all = Sim.OrderedEntities<Quest>(em);
            foreach (var e in all)
            {
                var q = em.GetComponentData<Quest>(e);
                if (q.Status == QuestStatus.Offered)
                {
                    if (Sim.Find(em, q.Source) == Entity.Null) em.DestroyEntity(e);
                    continue;
                }
                if (q.Status == QuestStatus.Claimed || q.Status == QuestStatus.Completed) continue;
                var definition = em.GetComponentData<Identity>(e).Definition;
                if (!Prerequisites(em, root, definition)) continue;
                if (q.StartTurn == 0 && QuestOps.HasQuestPredecessor(em, root, definition))
                {
                    q.StartTurn = em.GetComponentData<Session>(root).Turn;
                    var duration = Sim.Definition(em, root, definition).Duration;
                    q.Deadline = duration > 0 ? q.StartTurn + duration : 0;
                }
                var complete = true; var progress = em.GetBuffer<QuestProgress>(e);
                for (var i = 0; i < progress.Length; i++)
                {
                    var p = progress[i]; var r = Sim.GetRule(em, root, p.RuleIndex); var count = int.MaxValue;
                    switch (r.Kind)
                    {
                        case RuleKind.RequireItem: count = InventoryOps.Count(em, root, r.Target); break;
                        case RuleKind.SubmitItem: case RuleKind.RequireCameraMove: case RuleKind.RequireCameraZoom: count = p.Amount; break;
                        case RuleKind.RequireTurn: count = em.GetComponentData<Session>(root).Turn - (r.B != 0 ? q.StartTurn : 0); break;
                        case RuleKind.RequireTechnology:
                            count = 0; var queue = ResearchOps.Queue(em, root); if (queue.Count > 0 && (r.Target < 0 || queue[0].Definition == r.Target) && Prerequisites(em, root, queue[0].Definition)) count = 1; break;
                        case RuleKind.RequireBuilding: case RuleKind.RequireCrop:
                            count = 0; using (var buildings = Sim.OrderedEntities<Building>(em)) foreach (var b in buildings)
                            {
                                var state = em.GetComponentData<Building>(b);
                                if (state.Stage != LifeStage.Operational && !(r.Kind == RuleKind.RequireBuilding && r.C == 0 && state.Stage == LifeStage.Construction)) continue;
                                if (r.Kind == RuleKind.RequireBuilding && em.GetComponentData<Identity>(b).Definition == r.Target && em.GetComponentData<Building>(b).Level >= math.max(1, r.B)) count++;
                                if (r.Kind == RuleKind.RequireCrop && state.Crop >= 0 && (r.Target < 0 || em.GetComponentData<Identity>(b).Definition == r.Target)) count++;
                            }
                            break;
                    }
                    if (count != int.MaxValue) { if (count < r.Amount) complete = false; if (r.Kind != RuleKind.SubmitItem) { p.Amount = math.clamp(count, 0, r.Amount); progress[i] = p; } }
                }
                q.Status = complete ? QuestStatus.Completed : QuestStatus.Active; em.SetComponentData(e, q);
                if (complete) Sim.Emit(em, root, EventKind.Message, "任务已完成", em.GetComponentData<Identity>(e).Id, em.GetComponentData<Identity>(e).Definition);
            }
            QuestOps.RefreshTracking(em, root);
        }

        public static ResultCode Offer(EntityManager em, Entity root, Entity source, int slotIndex, bool paid)
            => QuestOfferOps.Generate(em, root, source, slotIndex, paid);
        public static ResultCode ExpeditionCommand(EntityManager em, Entity root, Command command)
            => ExpeditionOps.Command(em, root, command);

        public static void Settle(EntityManager em, Entity root)
        {
            ResearchTurn(em, root); DynastyOps.Settle(em, root); var turn = em.GetComponentData<Session>(root).Turn;
            ExpeditionOps.Settle(em, root);
            EvaluateQuests(em, root); // Production/research in this settlement can complete a task before expiry.
            using (var quests = Sim.OrderedEntities<Quest>(em)) foreach (var e in quests)
            {
                var q = em.GetComponentData<Quest>(e);
                if (q.Mainline == 0 && (q.Status == QuestStatus.Active || q.Status == QuestStatus.Completed) && q.Deadline > 0 && turn + 1 >= q.Deadline)
                { FailQuest(em, root, e, "期限已到"); }
            }
            QuestOfferOps.Settle(em, root);
            DiscoverQuests(em, root); EvaluateQuests(em, root);
        }

        public static void RemovePopulation(EntityManager em, Entity root, int amount)
        {
            using var all = Sim.OrderedEntities<Building>(em);
            foreach (var e in all) { if (amount <= 0) break; var b = em.GetComponentData<Building>(e); var remove = math.min(amount, b.Population); b.Population -= remove; amount -= remove; em.SetComponentData(e, b); }
            if (amount > 0) { var s = em.GetComponentData<Session>(root); s.BasePopulation = math.max(0, s.BasePopulation - amount); em.SetComponentData(root, s); }
        }
    }
}
