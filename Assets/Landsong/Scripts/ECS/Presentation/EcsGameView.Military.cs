using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        int soldierFilter = -1, soldierSort, recruitQuantity = 1;
        ulong compareSoldier, renameSoldier;
        string soldierNameDraft;
        InputField militaryNameInput;
        string SoldierLabel(Entity unit)
        {
            var id = em.GetComponentData<Identity>(unit); var s = em.GetComponentData<Soldier>(unit); var d = Sim.Definition(em, root, id.Definition);
            return (em.HasComponent<SoldierPerson>(unit)?PortraitOps.Age(em,unit)+" 岁 · ":"")+$"{id.Name} #{id.Id} · {d.Name} · Lv.{MilitaryOps.Level(d.SoldierGrowth, s.Experience)}" + (!Sim.Alive(em, unit) ? " · 阵亡（黎明释放）" : s.RecallState == 2 ? " · 已归营" : s.RecallState == 1 ? " · 召回途中" : "");
        }
        void SelectSoldier(ulong id)
        { if (Sim.Find(em,id)!=Entity.Null && em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison==0 || selectedSoldier == 0 || selectedSoldier == id) { selectedSoldier = id; compareSoldier = 0; } else compareSoldier = id; nextRefresh = 0; }
        void MilitaryRows()
        {
            var phase = em.GetComponentData<Session>(root).Phase; bool day = phase == Phase.Day && em.GetComponentData<Session>(root).Paused == 0 && em.GetComponentData<Session>(root).CheckpointPending == 0;
            bool tactical = (phase == Phase.Night || phase == Phase.Retreat) && em.GetComponentData<Session>(root).Paused == 0;
            Row("已驻扎 · 按真实槽位显示（所有兵种通用）"); Row("未驻扎 · 下次入夜前未分配将解散", right: true);
            Row("兵种筛选：" + (soldierFilter < 0 ? "全部" : Name(soldierFilter)), () =>
            {
                var choices = new List<int> { -1 }; ForDefinitions(ContentKind.Soldier, (i, _) => choices.Add(i)); soldierFilter = choices[(choices.IndexOf(soldierFilter) + 1) % choices.Count]; nextRefresh = 0;
            });
            Row("未驻扎排序：" + (soldierSort == 0 ? "稳定编号" : soldierSort == 1 ? "经验从高到低" : "姓名"), () => { soldierSort = (soldierSort + 1) % 3; nextRefresh = 0; }, true);
            if (day)
            {
                Row("招募数量 " + recruitQuantity + " · 点击增加（最多 100）", () => { recruitQuantity = math.min(100, recruitQuantity + 1); nextRefresh = 0; });
                Row("招募数量减一", recruitQuantity > 1 ? () => { recruitQuantity--; nextRefresh = 0; } : null);
            }
            using var buildings = Sim.OrderedEntities<Building>(em);
            foreach (var e in buildings)
            {
                var stats = em.GetComponentData<BuildingStats>(e); if (stats.Garrison <= 0) continue;
                var id = em.GetComponentData<Identity>(e); bool normal = Sim.Operational(em, e);
                Row($"【{id.Name} #{id.Id}】{MilitaryOps.GarrisonCount(em, id.Id)}/{stats.Garrison}" + (normal ? " · 点击定位" : " · 不可接收驻军"), () => LocateGarrison(id.Id));
                for (int slot = 1; slot <= stats.Garrison; slot++)
                {
                    int at = slot; var unit = MilitaryOps.AtSlot(em, id.Id, slot);
                    if (unit == Entity.Null) Row($"槽 {slot} · 空位", day && normal && selectedSoldier != 0 ? () => Send(CommandKind.AssignSoldier, selectedSoldier, id.Id, argument: at) : null);
                    else
                    {
                        var soldierId = em.GetComponentData<Identity>(unit); bool matches = soldierFilter < 0 || soldierId.Definition == soldierFilter;
                        SoldierPortraitRow(unit,$"槽 {slot} · " + (matches ? SoldierLabel(unit) : "其他兵种（占用）"), matches ? () => { SelectSoldier(soldierId.Id); OpenSoldierDetails(soldierId.Id); } : null);
                    }
                }
                if (day && normal)
                {
                    Row("一键填充空槽（不替换现有驻军）", () => Send(CommandKind.FillGarrison, id.Id));
                    ForDefinitions(ContentKind.Soldier, (index, definition) =>
                    {
                        if (soldierFilter >= 0 && soldierFilter != index) return;
                        var quote = MilitaryOps.RecruitQuote(em, root, id.Id, index, recruitQuantity,true);
                        Row($"募兵 {definition.Name} ×{recruitQuantity} · {CostText(quote.Costs)}\n空槽 {quote.EmptySlots} · 本回合剩余额度 {quote.RemainingLimit} · 空闲人口 {quote.FreePopulation}" + (quote.Code == ResultCode.Success ? "" : "\n" + (quote.RemainingLimit < recruitQuantity ? "本回合招募额度不足" : ResultName(quote.Code))), quote.Code == ResultCode.Success ? () => ConfirmSoldierRecruit(id.Id, index) : null);
                    });
                }
                if (tactical && normal) { Row("召回所属士兵", () => Send(CommandKind.RecallGarrison, id.Id)); Row("取消途中召回（已归营不再出勤）", () => Send(CommandKind.RecallGarrison, id.Id, argument: 1)); }
            }
            var pool = new List<Entity>(); using var all = Sim.OrderedEntities<Soldier>(em);
            foreach (var e in all) if (em.GetComponentData<Soldier>(e).Garrison == 0 && (soldierFilter < 0 || em.GetComponentData<Identity>(e).Definition == soldierFilter)) pool.Add(e);
            pool.Sort((a, b) =>
            {
                int cmp = soldierSort == 1 ? em.GetComponentData<Soldier>(b).Experience.CompareTo(em.GetComponentData<Soldier>(a).Experience) : soldierSort == 2 ? string.CompareOrdinal(em.GetComponentData<Identity>(a).Name.ToString(), em.GetComponentData<Identity>(b).Name.ToString()) : 0;
                return cmp != 0 ? cmp : em.GetComponentData<Identity>(a).Id.CompareTo(em.GetComponentData<Identity>(b).Id);
            });
            foreach (var e in pool) { var id = em.GetComponentData<Identity>(e).Id; SoldierPortraitRow(e,(selectedSoldier==id?"【已选中，点击左侧空槽】 ":"")+SoldierLabel(e), () => SelectSoldier(id), true); }
            if (pool.Count == 0) Row("当前筛选下无待分配士兵", right: true);
            SoldierSelection(day);
            using var heroes = Sim.OrderedEntities<Hero>(em);
            foreach (var e in heroes) { var h = em.GetComponentData<Hero>(e); var id = em.GetComponentData<Identity>(e); Row($"英雄：{id.Name} · 经验 {h.Experience} · {(h.DeathPending != 0 ? "阵亡，冷却待黎明开始" : h.Recruited != 0 ? "已招募" : "重募回合 " + h.CooldownUntil)}", right: true); }
        }
        void LocateGarrison(ulong id)
        {
            var site = Sim.Find(em, id); if (site == Entity.Null) return;
            selected = id; var position = (Vector3)Sim.Position(em, site);
            if (Camera != null) LocateHistory(id,position);
            nextRefresh = 0;
        }
        void ConfirmSoldierRecruit(ulong home, int definition)
        {
            int quantity = recruitQuantity; var quote = MilitaryOps.RecruitQuote(em, root, home, definition, quantity,true);
            ShowBuildingConfirmation("招募 " + Name(definition) + " ×" + quantity, new List<string> { CostText(quote.Costs), "募兵后进入待分配池并占用人口。先选择右侧士兵，再点击左侧空槽分配；下次入夜前未分配将解散。" }, () => Send(CommandKind.RecruitSoldier, home, definition: definition, amount: quantity, argument: 1));
        }
        void SoldierSelection(bool day)
        {
            var unit = Sim.Find(em, selectedSoldier); if (unit == Entity.Null || !em.HasComponent<Soldier>(unit)) { selectedSoldier = 0; compareSoldier = 0; return; }
            void DetailUnit(Entity e, string heading)
            {
                var s = em.GetComponentData<Soldier>(e); var d = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition); var stats = MilitaryOps.SoldierStats(em, root, e); int level = MilitaryOps.Level(d.SoldierGrowth, s.Experience);
                SoldierPortraitRow(e,heading + SoldierLabel(e) + $"\n驻地 {EntityName(s.Garrison)} · 槽 {s.Slot} · 人口 {s.PopulationCost}\n生命上限 {stats.Health:0.#} · 攻击 {stats.Damage:0.#} · 速度 {stats.Speed:0.#}\n经验 {s.Experience}" + (level >= d.SoldierGrowth.MaxLevel ? "（满级）" : " / 下级累计 " + MilitaryOps.LevelThreshold(d.SoldierGrowth, level + 1)), right: true);
            }
            Row("查看士兵详情",()=>OpenSoldierDetails(selectedSoldier),true); DetailUnit(unit, "选中："); var compare = Sim.Find(em, compareSoldier);
            if (compare != Entity.Null && em.HasComponent<Soldier>(compare))
            {
                DetailUnit(compare, "对比：");
                Row("交换两名士兵的位置", day && Sim.Alive(em, unit) && Sim.Alive(em, compare) ? () => Send(CommandKind.SwapSoldiers, selectedSoldier, compareSoldier) : null, true);
                Row("改为选中对比士兵", () => { selectedSoldier = compareSoldier; compareSoldier = 0; nextRefresh = 0; }, true);
            }
            Row("清除士兵选择", () => { selectedSoldier = compareSoldier = 0; nextRefresh = 0; }, true);
            if (!day || !Sim.Alive(em, unit)) return;
            ulong target = selectedSoldier; var identity = em.GetComponentData<Identity>(unit);
            Row("移入待分配池", () => Send(CommandKind.UnassignSoldier, target), true);
            Row("解散士兵…", () => ShowBuildingConfirmation("解散：" + identity.Name, new List<string> { "立即释放其占用人口与驻军槽；不返还任何招募资源。此操作会移除该个体。" }, () => Send(CommandKind.DismissSoldier, target, argument: 1)), true);
            if (renameSoldier != target) { renameSoldier = target; soldierNameDraft = identity.Name.ToString(); }
            var row = Row("", right: true); row.GetComponent<LayoutElement>().preferredHeight = 54;
            var child = row.transform.Find("MilitaryName");
            if (child == null)
            {
                var go = new GameObject("MilitaryName", typeof(RectTransform), typeof(Image), typeof(InputField)); go.transform.SetParent(row.transform, false);
                var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(8, 5); rect.offsetMax = new Vector2(-8, -5);
                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(go.transform, false);
                var tr = (RectTransform)textGo.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(8, 2); tr.offsetMax = new Vector2(-8, -2);
                var text = textGo.GetComponent<Text>(); text.font = RowTemplate.GetComponentInChildren<Text>(true).font; text.fontSize = 18; text.color = Color.black; text.richText = false; text.alignment = TMPro.TextAlignmentOptions.Left;
                var input = go.GetComponent<InputField>(); input.textViewport = input.GetComponent<RectTransform>(); input.textComponent = text; input.targetGraphic = go.GetComponent<Image>(); input.characterLimit = 30; child = go.transform;
            }
            child.gameObject.SetActive(true); militaryNameInput = child.GetComponent<InputField>(); militaryNameInput.onValueChanged.RemoveAllListeners(); militaryNameInput.SetTextWithoutNotify(soldierNameDraft); militaryNameInput.onValueChanged.AddListener(value => soldierNameDraft = value);
            Row("保存士兵姓名", () => Send(CommandKind.RenameSoldier, target, text: soldierNameDraft), true);
        }
    }
}
