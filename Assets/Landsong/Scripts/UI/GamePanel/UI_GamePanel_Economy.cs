using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Economy remains the stable navigation/serialized feature identity; this view presents historical bills.
    public sealed class UI_GamePanel_Economy : UI_GamePanel_View
    {
        [Sirenix.OdinInspector.LabelText("账单滚动视图")] public ScrollRect BillScroll;
        [Sirenix.OdinInspector.LabelText("回合账单模板")] public UI_GamePanel_BillTurn TurnTemplate;
        [Sirenix.OdinInspector.LabelText("无记录提示")] public TMP_Text EmptyState;
        [Sirenix.OdinInspector.LabelText("账单范围")] public TMP_Text ScopeLabel;
        [Sirenix.OdinInspector.LabelText("查看全城")] public Button AllSources;
        internal IGameBuildingUi Buildings;
        ulong economySource;
        readonly Dictionary<int, UI_GamePanel_BillTurn> turns = new Dictionary<int, UI_GamePanel_BillTurn>();
        readonly Dictionary<(int turn, int item), UI_GamePanel_BillRow> rows = new Dictionary<(int, int), UI_GamePanel_BillRow>();
        public IEnumerable<UI_GamePanel_BillTurn> TurnViews => turns.Values;
        public IEnumerable<UI_GamePanel_BillRow> ResourceRows => rows.Values;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (BillScroll == null || BillScroll.content == null || TurnTemplate == null || TurnTemplate.TurnLabel == null
                || TurnTemplate.Rows == null || TurnTemplate.RowTemplate == null || EmptyState == null || ScopeLabel == null || AllSources == null)
                throw new InvalidOperationException("账单面板引用不完整。");
            var row = TurnTemplate.RowTemplate;
            if (row.Resource == null || row.Income == null || row.Expense == null || row.Net == null || row.Stored == null)
                throw new InvalidOperationException("账单表格必须配置资源名、产出、消耗、净量、库存五列。");
        }
        public override void Bind(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation)
        {
            base.Bind(session, commands, navigation);
            AllSources.onClick.RemoveAllListeners(); AllSources.onClick.AddListener(() => navigation.OpenEconomy());
        }
        public void OpenEconomy(ulong source = 0)
        {
            if (economySource != source) ClearAllRows();
            economySource = source;
            Navigation.OpenPanel(GamePanelId.Economy);
        }
        public override void Render()
        {
            Title.text = "账单";
            ScopeLabel.text = economySource == 0 ? "全城 · 已结算回合；库存为当回合结算后已入库数量。" : Session.EntityName(economySource) + " · 本建筑收支与结算后库存";
            AllSources.gameObject.SetActive(economySource != 0);
            var groups = new SortedDictionary<int, List<EconomyBillEntry>>();
            if (em.HasBuffer<EconomyBillEntry>(root)) foreach (var bill in em.GetBuffer<EconomyBillEntry>(root))
            {
                if (bill.Source != economySource) continue;
                if (!groups.TryGetValue(bill.Turn, out var list)) groups.Add(bill.Turn, list = new List<EconomyBillEntry>());
                if (bill.Item >= 0) list.Add(bill);
            }
            EmptyState.gameObject.SetActive(groups.Count == 0);
            EmptyState.text = "暂无账单记录。完成回合结算后显示；旧存档未保存的历史库存不补算。";
            var keys = new HashSet<(int, int)>(); int index = 1;
            foreach (var group in groups)
            {
                if (!turns.TryGetValue(group.Key, out var turn))
                {
                    turn = Instantiate(TurnTemplate, BillScroll.content); turn.RowTemplate.gameObject.SetActive(false);
                    turn.gameObject.SetActive(true); turns.Add(group.Key, turn);
                }
                turn.TurnLabel.text = group.Key + "回合"; turn.transform.SetSiblingIndex(index++);
                foreach (var entry in group.Value.OrderBy(r => r.Item))
                {
                    var key = (group.Key, entry.Item); keys.Add(key);
                    if (!rows.TryGetValue(key, out var row)) { row = Instantiate(turn.RowTemplate, turn.Rows); row.gameObject.SetActive(true); rows.Add(key, row); }
                    row.Show(Session.Name(entry.Item), entry);
                }
            }
            foreach (var key in rows.Keys.Where(k => !keys.Contains(k)).ToArray()) { Destroy(rows[key].gameObject); rows.Remove(key); }
            foreach (var key in turns.Keys.Where(k => !groups.ContainsKey(k)).ToArray()) { Destroy(turns[key].gameObject); turns.Remove(key); }
        }
        internal static string Signed(long amount) => amount > 0 ? "+" + amount : amount.ToString();
        internal static string EconomyReasonName(EconomyReason reason) => reason switch
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
        internal void ResetSession() { ClearAllRows(); economySource = 0; }
        internal override void ClearAllRows()
        {
            foreach (var turn in turns.Values) if (turn != null) { turn.gameObject.SetActive(false); Destroy(turn.gameObject); }
            rows.Clear(); turns.Clear();
        }
    }
}
