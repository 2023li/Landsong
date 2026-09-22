using Landsong.ECS.Definitions;
using System;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Garrison : UI_GamePanel_List
    {
        internal WorldSelectionState WorldSelection;
        protected override bool UsesConfiguredPresenter => false;

        [Sirenix.OdinInspector.LabelText("组模板")]
        public UI_GamePanel_GarrisonRow GroupTemplate;
        [Sirenix.OdinInspector.LabelText("士兵模板")]
        public UI_GamePanel_SoldierRow SoldierTemplate;
        internal IGameBuildingUi Buildings;
        internal IGameSoldierUi Soldiers;
        internal IGameWorldUi World;
        [Sirenix.OdinInspector.LabelText("肖像缓存"), Sirenix.OdinInspector.Required]
        public PortraitCache Portraits;
        [Sirenix.OdinInspector.LabelText("已选士兵")]
        public ulong SelectedSoldier;
        SoldierId soldierFilter;
        int soldierSort, recruitQuantity = 1;
        void SelectSoldier(ulong id)
        {
            var unit = WorldQueries.Find(em, id);
            if (unit != Entity.Null && em.HasComponent<Soldier>(unit) && EntityState.Alive(em, unit) && em.GetComponentData<Soldier>(unit).Garrison == 0)
                SelectedSoldier = id;
            Refresh.NextPanel = 0;
        }

        public static float SoldierAttributeTotal(EntityManager manager, Entity owner, Entity unit)
        {
            var stats = SoldierOps.SoldierStats(manager, owner, unit);
            return (manager.HasComponent<Health>(unit) ? manager.GetComponentData<Health>(unit).Maximum : stats.Health) + stats.Speed;
        }

        public override void Render()
        {
            if (Portraits == null || SoldierTemplate == null || SoldierTemplate.Soldier == null || SoldierTemplate.Soldier.PortraitBinding == null)
                throw new InvalidOperationException("驻军面板未配置士兵肖像依赖。");
            var state = em.GetComponentData<Session>(root);
            SimulationControl stateControl = em.GetComponentData<SimulationControl>(root);
            PersistenceGate statePersistence = em.GetComponentData<PersistenceGate>(root);
            bool unlocked = stateControl.Paused == 0 && statePersistence.CheckpointPending == 0;
            bool day = state.Phase == Phase.Day && unlocked;
            bool tactical = (state.Phase == Phase.Night || state.Phase == Phase.Retreat) && unlocked;
            var chosen = WorldQueries.Find(em, SelectedSoldier);
            if (chosen == Entity.Null || !em.HasComponent<Soldier>(chosen) || !EntityState.Alive(em, chosen) || em.GetComponentData<Soldier>(chosen).Garrison != 0)
                SelectedSoldier = 0;
            Row("按建筑管理驻军 · 先选右侧士兵，再点左侧空槽");
            Row("兵种筛选：" + (!soldierFilter.IsValid ? "全部" : SoldierDefinitions.Get(em, root, soldierFilter).Metadata.Name.ToString()), () =>
            {
                var choices = new List<SoldierId>
                {
                    SoldierId.None
                };
                for (int i = 0; i < SoldierDefinitions.Count(em, root); i++)
                    choices.Add(SoldierId.FromIndex(i));
                soldierFilter = choices[(choices.IndexOf(soldierFilter) + 1) % choices.Count];
                Refresh.NextPanel = 0;
            });
            if (day)
            {
                Row("招募数量 " + recruitQuantity + " · 点击增加（最多 100）", () =>
                {
                    recruitQuantity = math.min(100, recruitQuantity + 1);
                    Refresh.NextPanel = 0;
                });
                Row("招募数量减一", recruitQuantity > 1 ? () =>
                {
                    recruitQuantity--;
                    Refresh.NextPanel = 0;
                } : null);
            }

            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var site in buildings)
            {
                BuildingGarrisonStats statsGarrison = em.GetComponentData<BuildingGarrisonStats>(site);
                if (statsGarrison.Capacity <= 0)
                    continue;
                var identity = em.GetComponentData<Identity>(site);
                ulong home = identity.Id;
                bool normal = BuildingStatus.Operational(em, site);
                var host = Rows.Item(GroupTemplate, "", parent: PrimaryRows, key: "garrison-building:" + home);
                if (host == null || !host.CanRebind)
                    continue;
                var group = host.Group;
                if (group == null)
                    throw new InvalidOperationException("通用行模板缺少 GarrisonGroup 引用。");
                group.gameObject.SetActive(true);
                group.BuildingId = home;
                Rows.Own(group.Rows, host.Interaction);
                Clear(group.Rows);
                Row($"【{identity.Name} #{home}】{GarrisonOps.GarrisonCount(em, home)}/{statsGarrison.Capacity}" + (normal ? " · 点击定位" : " · 不可接收驻军"), () => LocateGarrison(home), parent: group.Rows, key: "building-title:" + home);
                for (int slot = 1; slot <= statsGarrison.Capacity; slot++)
                {
                    int at = slot;
                    var unit = GarrisonOps.AtSlot(em, home, slot);
                    if (unit == Entity.Null)
                        Row($"槽 {slot} · 空位", day && normal && SelectedSoldier != 0 ? () => Commands.TryQueue(new AssignSoldierRequest { Soldier = SelectedSoldier, Garrison = home, Slot = at }) : null, parent: group.Rows, key: "empty-slot:" + home + ":" + at);
                    else
                        SoldierCard(unit, group.Rows, false, day, unlocked, $"槽 {slot}");
                }

                if (day && normal)
                {
                    Row("一键填充空槽（不替换现有驻军）", () => Commands.TryQueue(new FillGarrisonRequest { Garrison = home }), parent: group.Rows);
                    for (int i = 0; i < SoldierDefinitions.Count(em, root); i++)
                    {
                        var index = SoldierId.FromIndex(i);
                        if (soldierFilter.IsValid && soldierFilter != index)
                            continue;
                        ref var definition = ref SoldierDefinitions.Get(em, root, index);
                        var quote = SoldierOps.RecruitQuote(em, root, home, index, recruitQuantity, true);
                        Row($"募兵 {definition.Metadata.Name} ×{recruitQuantity} · {Buildings.CostText(quote.Costs)}\n本回合剩余额度 {quote.RemainingLimit} · 空闲人口 {quote.FreePopulation}" + (quote.Code == ResultCode.Success ? "" : " · " + GameUiText.ResultName(quote.Code)), quote.Code == ResultCode.Success ? () => ConfirmSoldierRecruit(home, index) : null, parent: group.Rows, key: "recruit:" + home + ":" + index.Index);
                    }
                }

                if (tactical && normal)
                {
                    Row("召回所属士兵", () => Commands.TryQueue(new RecallGarrisonRequest { Garrison = home, Cancel = false }), parent: group.Rows);
                    Row("取消途中召回（已归营不再出勤）", () => Commands.TryQueue(new RecallGarrisonRequest { Garrison = home, Cancel = true }), parent: group.Rows);
                }

                float height = 16 + Rows.Height(group.Rows);
                host.Layout.minHeight = host.Layout.preferredHeight = height;
            }

            Row("下次入夜前未分配将解散 · 点击卡片选择", right: true);
            Row("排序：" + (soldierSort == 0 ? "按等级（高→低）" : "按总属性值（高→低）"), () =>
            {
                soldierSort = 1 - soldierSort;
                Refresh.NextPanel = 0;
            }, true);
            if (soldierSort == 1)
                Row("总属性：生命上限＋敏捷（力量、知识待定义）", right: true);
            var pool = new List<Entity>();
            using var all = WorldQueries.OrderedEntities<Soldier>(em);
            foreach (var unit in all)
                if (em.GetComponentData<Soldier>(unit).Garrison == 0 && (!soldierFilter.IsValid || em.GetComponentData<SoldierDefinitionRef>(unit).Definition == soldierFilter))
                    pool.Add(unit);
            int Level(Entity unit)
            {
                var identity = em.GetComponentData<Identity>(unit);
                return UnitProgression.Level(SoldierDefinitions.Get(em, root, em.GetComponentData<SoldierDefinitionRef>(unit).Definition).Growth, em.GetComponentData<Soldier>(unit).Experience);
            }

            pool.Sort((a, b) =>
            {
                int result = soldierSort == 0 ? Level(b).CompareTo(Level(a)) : SoldierAttributeTotal(em, root, b).CompareTo(SoldierAttributeTotal(em, root, a));
                return result != 0 ? result : em.GetComponentData<Identity>(a).Id.CompareTo(em.GetComponentData<Identity>(b).Id);
            });
            foreach (var unit in pool)
                SoldierCard(unit, SecondaryRows, true, day, unlocked, "待分配");
            if (pool.Count == 0)
                Row("当前筛选下无待分配士兵", right: true);
        }

        void SoldierCard(Entity unit, RectTransform rows, bool pending, bool day, bool unlocked, string slot)
        {
            var identity = em.GetComponentData<Identity>(unit);
            var row = Rows.Item(SoldierTemplate, "", parent: rows, key: "soldier:" + identity.Id, prepare: candidate => candidate.Soldier.PortraitBinding.Cache = Portraits);
            if (row == null || !row.CanRebind)
                return;
            var card = row.Soldier;
            if (card == null)
                throw new InvalidOperationException("通用行模板缺少 SoldierItem 引用。");
            card.gameObject.SetActive(true);
            row.Layout.minHeight = row.Layout.preferredHeight = UI_GamePanel_SoldierItem.Height;
            ulong id = identity.Id;
            var soldier = em.GetComponentData<Soldier>(unit);
            ref var definition = ref SoldierDefinitions.Get(em, root, em.GetComponentData<SoldierDefinitionRef>(unit).Definition);
            var stats = SoldierOps.SoldierStats(em, root, unit);
            bool alive = EntityState.Alive(em, unit);
            var health = em.GetComponentData<Health>(unit);
            bool person = em.HasComponent<SoldierPerson>(unit);
            bool watched = person && em.GetComponentData<SoldierPerson>(unit).SpecialAttention != 0;
            string title = identity.Name.ToString(), rank = $"{slot} · {definition.Metadata.Name} · Lv.{UnitProgression.Level(definition.Growth, soldier.Experience)}" + (!alive ? " · 阵亡" : pending && SelectedSoldier == id ? " · 已选中" : "");
            card.Bind(id, title, rank, $"力量 —    知识 —\n敏捷 {stats.Speed:0.#}    血量 {health.Current:0.#}/{health.Maximum:0.#}", pending && SelectedSoldier == id, watched, unlocked && alive && person, pending && day && alive ? () => SelectSoldier(id) : null, () => Soldiers.OpenSoldierDetails(id), !pending && day && alive ? () => Commands.TryQueue(new UnassignSoldierRequest { Soldier = id }) : null, day && alive ? () => Commands.TryQueue(new DismissSoldierRequest { Soldier = id, Confirmed = true }) : null, v => Commands.TryQueue(new SetSoldierAttentionRequest { Soldier = id, Watched = (v ? 1 : 0) != 0 }));
            card.PortraitBinding.Bind(em, root, id);
            card.Remove.gameObject.SetActive(!pending);
        }

        internal void LocateGarrison(ulong id)
        {
            var site = WorldQueries.Find(em, id);
            if (site == Entity.Null)
                return;
            WorldSelection.SelectedEntityId = id;
            var position = (Vector3)EntityState.Position(em, site);
            if (World.Camera != null)
                World.LocateHistory(id, position);
            Refresh.NextPanel = 0;
        }

        void ConfirmSoldierRecruit(ulong home, SoldierId definition)
        {
            int quantity = recruitQuantity;
            var quote = SoldierOps.RecruitQuote(em, root, home, definition, quantity, true);
            Buildings.ShowBuildingConfirmation("招募 " + SoldierDefinitions.Get(em, root, definition).Metadata.Name.ToString() + " ×" + quantity, new List<string> { Buildings.CostText(quote.Costs), "募兵后进入待分配池并占用人口。先选择右侧士兵，再点击左侧空槽分配；下次入夜前未分配将解散。" }, () => Commands.TryQueue(new RecruitSoldiersRequest { Garrison = home, Soldier = definition, Quantity = quantity, Pending = true }));
        }

        internal void ResetSession()
        {
            SelectedSoldier = 0;
            soldierFilter = default;
            soldierSort = 0;
            recruitQuantity = 1;
        }
    }
}
