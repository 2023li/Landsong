using Landsong.ECS.Definitions;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    /// <summary>已归档的经济、王朝事件和夜战，按回合合并显示。</summary>
    public sealed class UI_GamePanel_History : UI_GamePanel_View
    {
        [LabelText("历史滚动视图"), Required] public ScrollRect HistoryScroll;
        [LabelText("回合历史模板"), Required] public UI_GamePanel_HistoryTurn TurnTemplate;
        [LabelText("无记录提示"), Required] public TMP_Text EmptyState;
        [LabelText("历史范围"), Required] public TMP_Text ScopeLabel;
        [LabelText("查看全城"), Required] public Button AllSources;

        readonly Dictionary<int, UI_GamePanel_HistoryTurn> turns = new Dictionary<int, UI_GamePanel_HistoryTurn>();
        ulong source;
        public IEnumerable<UI_GamePanel_HistoryTurn> TurnViews => turns.Values;

        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (HistoryScroll == null || HistoryScroll.content == null || TurnTemplate == null ||
                TurnTemplate.transform.parent != HistoryScroll.content || TurnTemplate.TurnLabel == null ||
                TurnTemplate.Body == null || EmptyState == null || ScopeLabel == null || AllSources == null)
                throw new InvalidOperationException("历史面板的滚动视图、回合模板或范围引用不完整。");
        }

        public override void Bind(GameUiSessionHandle session, GameUiCommandWriter commands, IGameUiNavigation navigation, GameUiRefreshScheduler refresh)
        {
            base.Bind(session, commands, navigation, refresh);
            AllSources.onClick.RemoveAllListeners();
            AllSources.onClick.AddListener(() => OpenHistory());
            TurnTemplate.gameObject.SetActive(false);
        }

        public void OpenHistory(ulong buildingSource = 0)
        {
            if (source != buildingSource)
                ClearAllRows();
            source = buildingSource;
            Navigation.OpenPanel(GamePanelId.History);
        }

        public override void Render()
        {
            Title.text = "历史";
            ScopeLabel.text = source == 0 ? "全城历史 · 经济为回合结算快照" : Session.EntityName(source) + " · 本建筑经济历史";
            AllSources.gameObject.SetActive(source != 0);
            var groups = ReadTurns(em, root, source);
            EmptyState.gameObject.SetActive(groups.Count == 0);
            EmptyState.text = "暂无历史记录。";
            int index = 1;
            foreach (var pair in groups)
            {
                if (!turns.TryGetValue(pair.Key, out var view))
                {
                    view = Instantiate(TurnTemplate, HistoryScroll.content);
                    view.gameObject.SetActive(true);
                    turns.Add(pair.Key, view);
                }

                view.TurnLabel.text = $"第{pair.Key}回合：";
                view.Body.text = FormatTurn(pair.Value, source != 0);
                view.transform.SetSiblingIndex(index++);
            }

            foreach (var turn in turns.Keys.Where(turn => !groups.ContainsKey(turn)).ToArray())
            {
                Destroy(turns[turn].gameObject);
                turns.Remove(turn);
            }
        }

        public sealed class TurnRecord
        {
            public readonly List<(string name, EconomyBillEntry bill)> Economy = new List<(string, EconomyBillEntry)>();
            public readonly List<HistoryEntry> Events = new List<HistoryEntry>();
            public readonly List<BattleReportEntry> Battle = new List<BattleReportEntry>();
        }

        public static SortedDictionary<int, TurnRecord> ReadTurns(EntityManager manager, Entity owner, ulong buildingSource = 0)
        {
            var result = new SortedDictionary<int, TurnRecord>();
            TurnRecord Turn(int turn)
            {
                if (!result.TryGetValue(turn, out var record))
                    result.Add(turn, record = new TurnRecord());
                return record;
            }

            if (manager.HasBuffer<EconomyBillEntry>(owner))
                foreach (var bill in manager.GetBuffer<EconomyBillEntry>(owner))
                {
                    if (bill.Source != buildingSource)
                        continue;
                    var record = Turn(bill.Turn);
                    if (bill.Item.IsValid)
                        record.Economy.Add((ItemDefinitions.Get(manager, owner, bill.Item).Metadata.Name.ToString(), bill));
                }

            if (buildingSource != 0)
                return result;
            if (manager.HasBuffer<HistoryEntry>(owner))
                foreach (var entry in manager.GetBuffer<HistoryEntry>(owner))
                    if (!entry.Item.IsValid && !entry.Text.IsEmpty)
                        Turn(entry.Turn).Events.Add(entry);
            if (manager.HasBuffer<BattleHistoryEntry>(owner))
                foreach (var entry in manager.GetBuffer<BattleHistoryEntry>(owner))
                    Turn(entry.Turn).Battle.Add(entry.Entry);
            return result;
        }

        public static string FormatTurn(TurnRecord record, bool economyOnly = false)
        {
            var text = new StringBuilder("经济：\n");
            foreach (var row in record.Economy.OrderBy(row => row.bill.Item))
                text.Append(row.name).Append("  本回合库存量 ").Append(row.bill.Stored)
                    .Append("  本回合变化量 ").Append(Signed(row.bill.Income - row.bill.Expense)).Append('\n');
            if (record.Economy.Count == 0)
                text.Append("本回合无已结算经济记录。\n");
            if (economyOnly)
                return text.ToString().TrimEnd();

            text.Append("事件：\n");
            var events = EventLines(record.Events);
            if (events.Count == 0)
                text.Append("无事件。\n");
            else
                foreach (var line in events)
                    text.Append(line).Append('\n');

            text.Append("战报：\n");
            foreach (var line in BattleLines(record.Battle))
                text.Append(line).Append('\n');
            return text.ToString().TrimEnd();
        }

        public static List<string> EventLines(IReadOnlyList<HistoryEntry> entries)
        {
            var result = new List<string>();
            var pairedSuccessors = new HashSet<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (pairedSuccessors.Contains(i))
                    continue;
                var entry = entries[i];
                var message = entry.Text.ToString();
                var name = entry.SourceName.ToString();
                if (message.StartsWith("君王") &&
                    (message.Contains("逝世") || message.Contains("遇难") || message.Contains("赐死") || message.Contains("弑杀")))
                {
                    bool paired = false;
                    for (int next = i + 1; next < entries.Count; next++)
                    {
                        var succession = entries[next];
                        var successionText = succession.Text.ToString();
                        if (!successionText.Contains("新君即位") && !successionText.Contains("登基"))
                            continue;
                        if (!string.IsNullOrEmpty(name) && !succession.SourceName.IsEmpty)
                        {
                            result.Add($"{name}国王驾崩，由{succession.SourceName}继位");
                            pairedSuccessors.Add(next);
                            paired = true;
                        }
                        break;
                    }
                    if (paired)
                        continue;
                }
                result.Add((string.IsNullOrEmpty(name) || message.Contains(name) ? "" : name + " · ") + message + (entry.Count > 1 ? " × " + entry.Count : ""));
            }

            return result;
        }

        public static List<string> BattleLines(IReadOnlyList<BattleReportEntry> entries)
        {
            if (entries.Count == 0)
                return new List<string> { "本回合尚无已归档战报。" };
            var soldiers = entries.Where(entry => entry.Kind == EventKind.SoldierDeath || entry.Kind == EventKind.HeroDeath)
                .Select(entry => entry.SourceName.ToString()).Where(name => !string.IsNullOrEmpty(name)).ToArray();
            var buildings = entries.Where(entry => entry.Kind == EventKind.Ruin)
                .Select(entry => entry.SourceName.ToString()).Where(name => !string.IsNullOrEmpty(name)).ToArray();
            var result = new List<string>();
            if (soldiers.Length > 0)
                result.Add("为抵御敌人入侵，我们失去了一些勇士：" + string.Join("、", soldiers));
            if (buildings.Length > 0)
                result.Add("入侵中被损毁的建筑：" + string.Join("、", buildings));
            if (result.Count == 0)
                result.Add(entries.Any(entry => entry.Kind == EventKind.EnemyDeath || entry.Kind == EventKind.BossKilled || entry.Kind == EventKind.BossRetreated)
                    ? "成功抵御敌人入侵，没有勇士或建筑损失。" : "是个平安夜");
            return result;
        }

        public static string Signed(long amount) => amount > 0 ? "+" + amount : amount.ToString();

        internal override void ClearAllRows()
        {
            foreach (var view in turns.Values)
                if (view != null)
                    Destroy(view.gameObject);
            turns.Clear();
        }

        internal void ResetSession()
        {
            ClearAllRows();
            source = 0;
        }
    }
}
