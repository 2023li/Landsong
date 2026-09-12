using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class QuestSubmissionQuote
    {
        public ResultCode Code = ResultCode.InvalidTarget;
        public int Item = -1, Available, Remaining, Maximum, ProgressIndex = -1;
        public string Key, Stamp, Reason = "任务或要求已不存在";
    }
    public static class QuestOps
    {
        public static bool Requirement(RuleKind kind) => kind >= RuleKind.RequireBuilding && kind <= RuleKind.RequireTurn;
        public static FixedString64Bytes Key(Rule rule, int local) => rule.Key.IsEmpty ? new FixedString64Bytes("@" + local) : rule.Key; // Only hand-built test blobs use this fallback; authoring requires explicit keys.
        public static QuestTracking Tracking(EntityManager em, Entity root) => em.HasComponent<QuestTracking>(root) ? em.GetComponentData<QuestTracking>(root) : default;
        public static bool Trackable(EntityManager em, Entity e) => e != Entity.Null && em.Exists(e) && em.HasComponent<Quest>(e) && (em.GetComponentData<Quest>(e).Status == QuestStatus.Active || em.GetComponentData<Quest>(e).Status == QuestStatus.Completed);
        public static long RewardValue(EntityManager em, Entity root, int definition)
        {
            var d=Sim.Definition(em,root,definition);long value=0;
            for(var i=0;i<d.RuleCount;i++)
            {
                var rule=Sim.GetRule(em,root,d.RuleStart+i);if(rule.Kind!=RuleKind.RewardItem)continue;
                // Baked reward amounts already include the quest quantity multiplier.
                var amount=(long)math.max(0,rule.Amount)*math.max(0,Sim.Definition(em,root,rule.Target).Value);
                value=amount>long.MaxValue-value?long.MaxValue:value+amount;
            }
            return value;
        }
        public static int CompareValue(EntityManager em, Entity root, Entity a, Entity b)
        {
            var ia=em.GetComponentData<Identity>(a);var ib=em.GetComponentData<Identity>(b);
            var order=RewardValue(em,root,ib.Definition).CompareTo(RewardValue(em,root,ia.Definition));
            return order!=0?order:ia.Id.CompareTo(ib.Id);
        }
        public static bool HasQuestPredecessor(EntityManager em, Entity root, int definition, int predecessor = -1)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind == RuleKind.Prerequisite && Sim.Definition(em, root, r.Target).Kind == ContentKind.Quest &&
                    (predecessor < 0 || r.Target == predecessor)) return true;
            }
            return false;
        }
        static ulong BestAccepted(EntityManager em, Entity root, int predecessor=-1)
        {
            Entity best=Entity.Null;using var all=Sim.OrderedEntities<Quest>(em);
            foreach(var e in all)
            {
                if(!Trackable(em,e))continue;
                if(predecessor>=0)
                {
                    var d=Sim.Definition(em,root,em.GetComponentData<Identity>(e).Definition);var follows=false;
                    for(var i=0;i<d.RuleCount;i++){var r=Sim.GetRule(em,root,d.RuleStart+i);if(r.Kind==RuleKind.Prerequisite && r.Target==predecessor)follows=true;}
                    if(!follows)continue;
                }
                if(best==Entity.Null || CompareValue(em,root,e,best)<0)best=e;
            }
            return best==Entity.Null?0:em.GetComponentData<Identity>(best).Id;
        }
        public static void FollowClaim(EntityManager em, Entity root, int definition, ulong continuation = 0)
        {
            var next = Trackable(em, Sim.Find(em, continuation)) ? continuation : BestAccepted(em, root, definition);
            if (next == 0) next = BestAccepted(em, root);
            Sim.Set(em,root,new QuestTracking{Mode=0,Target=next});
        }
        public static void RefreshTracking(EntityManager em, Entity root)
        {
            var state=Tracking(em,root);var prior=state.Target;
            if(state.Mode==0 && !Trackable(em,Sim.Find(em,state.Target)))state.Target=BestAccepted(em,root);
            else if(state.Mode==2 || !Trackable(em,Sim.Find(em,state.Target)))state.Target=0;
            if(prior!=state.Target || !em.HasComponent<QuestTracking>(root))Sim.Set(em,root,state);
        }
        public static ResultCode Track(EntityManager em, Entity root, Command command)
        {
            if (command.Argument < 0 || command.Argument > 2) return ResultCode.InvalidTarget;
            if (command.Argument == 1 && !Trackable(em, Sim.Find(em, command.Target))) return ResultCode.InvalidTarget;
            if(command.Argument==2 && command.Target!=0 && Tracking(em,root).Target!=command.Target)return ResultCode.Success;
            Sim.Set(em, root, new QuestTracking { Mode = (byte)command.Argument, Target = command.Argument == 1 ? command.Target : 0 }); RefreshTracking(em, root); return ResultCode.Success;
        }
        public static string Fingerprint(EntityManager em, Entity root, Entity entity)
        {
            ulong hash = 14695981039346656037UL; void Add(ulong v) { unchecked { hash = (hash ^ v) * 1099511628211UL; } }
            var q = em.GetComponentData<Quest>(entity); Add(em.GetComponentData<Identity>(entity).Id); Add((ulong)q.Status); Add((ulong)q.StartTurn);
            foreach (var p in em.GetBuffer<QuestProgress>(entity)) { foreach (var c in p.Key.ToString()) Add(c); Add((ulong)p.Amount); }
            // A confirmation includes the displayed stock, not only the task's remaining demand.
            foreach (var s in em.GetBuffer<InventorySlot>(root)) { Add(s.Provider); Add((ulong)s.Index); Add((ulong)(s.Item + 1)); Add((ulong)s.Count); Add(s.Unavailable); }
            return hash.ToString("X16");
        }
        public static QuestSubmissionQuote Submission(EntityManager em, Entity root, Entity entity, string key)
        {
            var quote = new QuestSubmissionQuote { Key = key };
            if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<Quest>(entity)) return quote;
            var phase = em.GetComponentData<Session>(root); var quest = em.GetComponentData<Quest>(entity);
            if (phase.Phase != Phase.Day || phase.CheckpointPending != 0) { quote.Code = ResultCode.WrongPhase; quote.Reason = "仅白天可提交"; return quote; }
            if (quest.Status != QuestStatus.Active) { quote.Code = ResultCode.Unavailable; quote.Reason = "仅进行中的任务可提交"; return quote; }
            if (!ConditionOps.Prerequisites(em, root, em.GetComponentData<Identity>(entity).Definition))
            { quote.Code = ResultCode.Unavailable; quote.Reason = "等待前置条件，原承接槽位保留"; return quote; }
            var progress = em.GetBuffer<QuestProgress>(entity);
            for (var i = 0; i < progress.Length; i++)
            {
                var p = progress[i]; var rule = Sim.GetRule(em, root, p.RuleIndex); if (p.Key.ToString() != key || rule.Kind != RuleKind.SubmitItem) continue;
                quote.Item = rule.Target; quote.ProgressIndex = i; quote.Remaining = math.max(0, rule.Amount - p.Amount); quote.Available = InventoryOps.Count(em, root, rule.Target); quote.Maximum = math.min(quote.Remaining, quote.Available); quote.Stamp = Fingerprint(em, root, entity);
                quote.Code = quote.Maximum > 0 ? ResultCode.Success : ResultCode.InsufficientResources; quote.Reason = quote.Remaining == 0 ? "该项已经提交完毕" : quote.Maximum == 0 ? "正常库存不足；待存放不用于任务提交" : "只提交此项，确认后不可退还"; return quote;
            }
            return quote;
        }
        public static ResultCode Submit(EntityManager em, Entity root, Entity entity, Command command)
        {
            var parts = command.Text.ToString().Split('|'); if (parts.Length != 2) return ResultCode.InvalidTarget;
            var quote = Submission(em, root, entity, parts[0]); if (quote.Code != ResultCode.Success) return quote.Code;
            if (parts[1] != quote.Stamp || command.Definition != quote.Item) return ResultCode.Unavailable;
            if (command.Amount <= 0 || command.Amount > quote.Maximum) return ResultCode.InsufficientResources;
            if (!InventoryOps.Remove(em, root, quote.Item, command.Amount)) return ResultCode.InsufficientResources;
            var progress = em.GetBuffer<QuestProgress>(entity); var p = progress[quote.ProgressIndex]; p.Amount += command.Amount; progress[quote.ProgressIndex] = p;
            ProgressionOps.EvaluateQuests(em, root); return ResultCode.Success;
        }
        public static void ValidateProgress(EntityManager em, Entity root, int definition, Quest quest, QuestProgress[] progress, int turn)
        {
            if (!Sim.ValidDefinition(em, root, definition)) throw new InvalidDataException("Invalid quest definition");
            var d = Sim.Definition(em, root, definition);
            if (d.Kind != ContentKind.Quest || quest.Status > QuestStatus.Claimed || quest.StartTurn < 0 || quest.StartTurn > turn || quest.Deadline < 0 || quest.Mainline > 1) throw new InvalidDataException("Invalid quest state");
            var keys = new HashSet<string>(); var expected = 0;
            for (var n = 0; n < d.RuleCount; n++) if (Requirement(Sim.GetRule(em, root, d.RuleStart + n).Kind)) expected++;
            if (progress.Length != expected) throw new InvalidDataException("Quest requirement count mismatch");
            foreach (var p in progress)
            {
                if (p.RuleIndex < d.RuleStart || p.RuleIndex >= d.RuleStart + d.RuleCount || !keys.Add(p.Key.ToString())) throw new InvalidDataException("Invalid/duplicate quest requirement");
                var rule = Sim.GetRule(em, root, p.RuleIndex);
                if (!Requirement(rule.Kind) || p.Key != Key(rule, p.RuleIndex - d.RuleStart) || p.Amount < 0 || p.Amount > rule.Amount) throw new InvalidDataException("Invalid quest progress");
            }
        }
    }
}
