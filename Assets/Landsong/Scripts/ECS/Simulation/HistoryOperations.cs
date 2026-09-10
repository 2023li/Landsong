using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum HistoryCategory : byte { General, Economy, Military, Important }
    [InternalBufferCapacity(0)] public struct HistoryEntry : IBufferElementData
    {
        public int Turn, Item, Delta, Count; public ulong Source; public byte Pending, HasPosition, Transfer;
        public HistoryCategory Category; public float3 Position; public FixedString128Bytes SourceName, Text;
    }
    public struct ManualHistoryContext : IComponentData { public byte Active; public ulong Source; public FixedString128Bytes Name, Reason; }
    public static class HistoryOps
    {
        public const int Limit = 2048;
        public static HistoryCategory Category(EventKind kind,string message)
        {if(kind==EventKind.Ruin||message.Contains("核心")||message.Contains("绝嗣")||message.Contains("失败")||message.Contains("阵亡")||message.Contains("死亡"))return HistoryCategory.Important;if(message.Contains("资源")||message.Contains("收获")||message.Contains("生产")||message.Contains("供奉")||message.Contains("金币"))return HistoryCategory.Economy;return HistoryCategory.General;}
        public static void Ensure(EntityManager em,Entity root) {Sim.Buffer<HistoryEntry>(em,root);if(!em.HasComponent<ManualHistoryContext>(root))em.AddComponentData(root,new ManualHistoryContext());}
        public static void Message(EntityManager em,Entity root,EventKind kind,FixedString128Bytes message,ulong source)
        {
            if(message.IsEmpty||kind!=EventKind.Message&&kind!=EventKind.Ruin||!em.HasBuffer<HistoryEntry>(root)||EconomyJournalOps.Forecast(em,root))return;
            var entry=new HistoryEntry {Turn=em.GetComponentData<Session>(root).Turn,Item=-1,Count=1,Source=source,Text=message,Category=Category(kind,message.ToString())};
            var e=Sim.Find(em,source);if(e!=Entity.Null){entry.SourceName=em.GetComponentData<Identity>(e).Name;if(em.HasComponent<Unity.Transforms.LocalTransform>(e)&&(em.HasComponent<Building>(e)||em.HasComponent<Combatant>(e))){entry.Position=Sim.Position(em,e);entry.HasPosition=1;}}
            var rows=em.GetBuffer<HistoryEntry>(root);if(rows.Length>0){var last=rows[rows.Length-1];if(last.Turn==entry.Turn&&last.Source==source&&last.Text.Equals(message)&&last.Item==-1){last.Count=math.min(100000,last.Count+1);rows[rows.Length-1]=last;return;}}
            rows.Add(entry);
        }
        public static void Resource(EntityManager em,Entity root,int item,int delta,bool pending,FixedString128Bytes note)
        {
            if(!em.HasBuffer<HistoryEntry>(root)||delta==0&&note.IsEmpty)return;
            var journal=em.HasComponent<EconomyJournalState>(root)?em.GetComponentData<EconomyJournalState>(root):default;
            var manual=em.HasComponent<ManualHistoryContext>(root)?em.GetComponentData<ManualHistoryContext>(root):default;
            if(journal.Forecast!=0||journal.Recording==0&&manual.Active==0)return;
            var entry=new HistoryEntry {Turn=em.GetComponentData<Session>(root).Turn,Category=HistoryCategory.Economy,Item=item,Delta=delta,Pending=(byte)(pending?1:0),Count=1,Source=journal.Recording!=0?journal.Source:manual.Source,SourceName=journal.Recording!=0?journal.SourceName:manual.Name,Text=note.IsEmpty?(journal.Recording!=0?new FixedString128Bytes(Reason(journal.Reason)):manual.Reason):note};
            entry.Transfer=(byte)((journal.Recording!=0?journal.Reason==EconomyReason.CapacityTransfer:manual.Reason.ToString()=="待存放转库")?1:0);
            var e=Sim.Find(em,entry.Source);if(e!=Entity.Null&&em.HasComponent<Unity.Transforms.LocalTransform>(e)&&(em.HasComponent<Building>(e)||em.HasComponent<Combatant>(e))){entry.Position=Sim.Position(em,e);entry.HasPosition=1;}em.GetBuffer<HistoryEntry>(root).Add(entry);
        }
        public static string Reason(EconomyReason r)=>r switch {EconomyReason.Construction=>"施工",EconomyReason.Repair=>"修复",EconomyReason.Maintenance=>"维护",EconomyReason.Workforce=>"岗位",EconomyReason.Production=>"生产",EconomyReason.Crop=>"作物",EconomyReason.Food=>"食物",EconomyReason.Tax=>"税收",EconomyReason.Offering=>"供奉",EconomyReason.Market=>"交易",EconomyReason.NaturalLoss=>"自然损耗",EconomyReason.CapacityTransfer=>"转库（非收入）",EconomyReason.Research=>"科研",EconomyReason.TalentWage=>"人才工资",EconomyReason.TalentBenefit=>"人才收益",EconomyReason.Expedition=>"远征",EconomyReason.QuestPenalty=>"任务惩罚",EconomyReason.NightDiscard=>"入夜清空",_=>"资源变动"};
        public static bool Manual(CommandKind kind)=>kind!=CommandKind.Advance&&kind!=CommandKind.ForecastEconomy&&kind!=CommandKind.CameraMoved&&kind!=CommandKind.CameraZoomed&&kind!=CommandKind.Save&&kind!=CommandKind.Load&&kind!=CommandKind.Pause&&kind!=CommandKind.RetryDay&&kind!=CommandKind.RetryDusk&&kind!=CommandKind.ReadIntelligence&&kind!=CommandKind.IntelligenceMode;
        public static string ActionName(CommandKind kind)=>kind switch {CommandKind.Discard or CommandKind.DiscardSlot or CommandKind.DiscardPending=>"主动丢弃",CommandKind.Build or CommandKind.BuildRoad=>"建造支付",CommandKind.Demolish=>"拆除返还 / 转库",CommandKind.Repair=>"修复支付",CommandKind.Upgrade=>"升级支付",CommandKind.RecruitSoldier or CommandKind.RecruitHero or CommandKind.RecruitTalent=>"招募",CommandKind.WakeHero=>"英雄出场",CommandKind.Harvest=>"手动收获",CommandKind.GiftPerson=>"赠礼",CommandKind.StartExpedition=>"远征派遣",CommandKind.ClaimExpedition=>"远征结算",CommandKind.ClaimQuest=>"任务奖励",CommandKind.SubmitQuest=>"任务提交",CommandKind.StorePending or CommandKind.StorePendingSlot=>"待存放转库",_=>"玩家操作"};
        public static void Trim(EntityManager em,Entity root)
        {if(!em.HasBuffer<HistoryEntry>(root))return;var rows=em.GetBuffer<HistoryEntry>(root);if(rows.Length>Limit)rows.RemoveRange(0,rows.Length-Limit);}
    }
}
