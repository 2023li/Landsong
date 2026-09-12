using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BattleReport : UI_GamePanel_List
    {
        protected override bool UsesConfiguredPresenter => false;

        public override void Render()
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase != Phase.Day && phase != Phase.Report)
            {
                Row("本夜战报尚未结算；被盗明细只在结算时显示。");
                return;
            }

            var rows = new System.Collections.Generic.List<BattleReportEntry>();
            foreach (var entry in em.GetBuffer<BattleReportEntry>(root))
                rows.Add(entry);
            foreach (var line in NightReportOps.Lines(em, root, rows))
                Row(line);
            Row(em.GetComponentData<Session>(root).Phase == Phase.Day ? "收益已结算，溢出物资进入待存放池。" : "特殊掉落已全部记录；确认战报后先提交战损，再发放收益，放不下的进入待存放池。");
            if (em.GetComponentData<Session>(root).Phase == Phase.Report)
                Row("确认战报 · 下一回合", () => Commands.TryQueue(CommandRequests.Advance()));
            if (em.GetComponentData<Session>(root).Phase == Phase.Day)
                ReportHistoryRows();
        }

        int historyTurn;
        void ReportHistoryRows()
        {
            if (!em.HasBuffer<BattleHistoryEntry>(root))
                return;
            var turns = new List<int>();
            foreach (var h in em.GetBuffer<BattleHistoryEntry>(root))
                if (!turns.Contains(h.Turn))
                    turns.Add(h.Turn);
            turns.Reverse();
            foreach (int turn in turns)
                Row("查看第 " + turn + " 夜已结算战报", () =>
                {
                    historyTurn = historyTurn == turn ? 0 : turn;
                    Session.NextRefresh = 0;
                });
            if (historyTurn == 0)
                return;
            var entries = new List<BattleReportEntry>();
            foreach (var h in em.GetBuffer<BattleHistoryEntry>(root))
                if (h.Turn == historyTurn)
                    entries.Add(h.Entry);
            Row("第 " + historyTurn + " 夜 · 已提交记录");
            foreach (var line in NightReportOps.Lines(em, root, entries))
                Row(line);
        }
    }
}
