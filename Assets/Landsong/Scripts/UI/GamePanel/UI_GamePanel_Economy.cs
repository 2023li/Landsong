using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Economy : UI_GamePanel_List
    {
        protected override bool UsesConfiguredPresenter => false;

        internal IGameBuildingUi Buildings;
        ulong economySource;
        bool economyForecast, forecastCurrent;
        float forecastCheckAt;
        string checkedFingerprint;
        Entity checkedRoot;
        public void OpenEconomy(ulong source = 0)
        {
            economySource = source;
            forecastCheckAt = 0;
            Navigation.OpenPanel(GamePanelId.Economy);
        }

        public override void Render()
        {
            Row(economySource == 0 ? "经济总览 · 全城" : "建筑经济 · " + Session.EntityName(economySource));
            if (economySource != 0)
                Row("查看全城", () => Navigation.OpenEconomy());
            Row(economyForecast ? "切换：最近实际账本" : "切换：白天参考预测", () =>
            {
                economyForecast = !economyForecast;
                Session.NextRefresh = 0;
            });
            var rows = new List<EconomyEntry>();
            if (economyForecast)
            {
                var session = em.GetComponentData<Session>(root);
                Row("刷新预测（不扣资源，不推进回合）", session.Phase == Phase.Day ? () =>
                {
                    Commands.Send(CommandKind.ForecastEconomy);
                    forecastCheckAt = 0;
                } : null);
                Row("按当前工人和确定性规则计算。随机产物、随机收获及费用、远征结果不计入净额；王室事件不预告。下游资源可能因此与实际不同。未含入夜待存放清空。");
                if (!em.HasComponent<EconomyForecastState>(root) || em.GetComponentData<EconomyForecastState>(root).Fingerprint.IsEmpty)
                {
                    Row("尚未生成预测，请在白天刷新。");
                    return;
                }

                var model = em.GetComponentData<EconomyForecastState>(root);
                var key = model.Fingerprint.ToString();
                if (Time.unscaledTime >= forecastCheckAt || checkedRoot != root || checkedFingerprint != key)
                {
                    forecastCheckAt = Time.unscaledTime + 1;
                    checkedRoot = root;
                    checkedFingerprint = key;
                    forecastCurrent = session.Phase == Phase.Day && key == EconomyForecastOps.Fingerprint(em, root);
                }

                Row($"白天 {model.Turn} 的参考值 · " + (forecastCurrent ? "条件核对通过（每秒检查）" : "条件已变化，请刷新"));
                if (!forecastCurrent)
                    return;
                foreach (var row in em.GetBuffer<EconomyForecastEntry>(root))
                    if (economySource == 0 || row.Value.Source == economySource)
                        rows.Add(row.Value);
            }
            else
            {
                if (!em.HasComponent<EconomyJournalState>(root) || em.GetComponentData<EconomyJournalState>(root).Turn == 0)
                {
                    Row("尚无已提交的白天结算账本。");
                    return;
                }

                Row($"最近一次白天结算：回合 {em.GetComponentData<EconomyJournalState>(root).Turn}。不含白天手动建造、招募及夜战收支；已确认的入夜清空单列。");
                foreach (var row in em.GetBuffer<EconomyEntry>(root))
                    if (economySource == 0 || row.Source == economySource)
                        rows.Add(row);
            }

            var totals = new SortedDictionary<int, (long income, long expense, long normal, long pending)>();
            foreach (var row in rows)
            {
                if (row.Delta == 0)
                    continue;
                totals.TryGetValue(row.Item, out var total);
                if (row.Reason != EconomyReason.CapacityTransfer)
                {
                    if (row.Delta > 0)
                        total.income += row.Delta;
                    else
                        total.expense -= row.Delta;
                }

                if (row.Pending == 0)
                    total.normal += row.Delta;
                else
                    total.pending += row.Delta;
                totals[row.Item] = total;
            }

            Row("资源汇总（转库不计作收入/支出；库存与待存放净变动分别显示）");
            foreach (var item in totals)
                Row($"{Session.Name(item.Key)}：收入 {item.Value.income} / 支出 {item.Value.expense} / 净额 {Signed(item.Value.income - item.Value.expense)}\n库存 {Signed(item.Value.normal)} · 待存放 {Signed(item.Value.pending)}");
            Row("逐笔原因与停滞提示");
            foreach (var row in rows)
            {
                var source = row.Source == 0 ? row.SourceName.ToString() : Sim.Find(em, row.Source) != Entity.Null ? Session.EntityName(row.Source) : row.SourceName.ToString();
                var amount = row.Delta == 0 ? row.Item >= 0 ? " · " + Session.Name(row.Item) : "" : $" · {Session.Name(row.Item)} {Signed(row.Delta)} · {(row.Pending == 0 ? "库存" : "待存放")}";
                var sourceEntity = Sim.Find(em, row.Source);
                var building = sourceEntity != Entity.Null && em.HasComponent<Building>(sourceEntity);
                Row($"{source} / {EconomyReasonName(row.Reason)}{amount}" + (row.Note.IsEmpty ? "" : "\n" + row.Note), building ? () => Buildings.FocusBuilding(row.Source) : null);
            }

            if (rows.Count == 0)
                Row("本次没有该来源的记录。");
        }

        internal static string Signed(long amount) => amount > 0 ? "+" + amount : amount.ToString();
        static string EconomyReasonName(EconomyReason reason) => reason switch
        {
            EconomyReason.Construction => "施工",
            EconomyReason.Repair => "修复",
            EconomyReason.Maintenance => "维护",
            EconomyReason.Workforce => "岗位补贴",
            EconomyReason.Production => "生产",
            EconomyReason.Crop => "收获",
            EconomyReason.Food => "食谱",
            EconomyReason.Tax => "税收",
            EconomyReason.Offering => "供奉",
            EconomyReason.Market => "市场收益",
            EconomyReason.NaturalLoss => "自然损耗",
            EconomyReason.CapacityTransfer => "转库",
            EconomyReason.Research => "研究奖励",
            EconomyReason.TalentWage => "人才工资",
            EconomyReason.TalentBenefit => "人才收益",
            EconomyReason.Expedition => "远征",
            EconomyReason.QuestPenalty => "任务惩罚",
            EconomyReason.NightDiscard => "入夜清空",
            _ => reason.ToString()};
        internal void ResetSession()
        {
            economySource = 0;
            economyForecast = forecastCurrent = false;
            forecastCheckAt = 0;
            checkedFingerprint = null;
            checkedRoot = Entity.Null;
        }
    }
}
