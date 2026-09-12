using System;
using System.Linq;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Intelligence : UI_GamePanel_List
    {
        protected override bool UsesConfiguredPresenter => false;

        internal IGameCourtUi Court;
        internal IGameWorldUi World;
        int intelligenceWave;
        [Sirenix.OdinInspector.LabelText("视图")]
        public IntelView View;
        public void ResetView()
        {
            intelligenceWave = 0;
            View = null;
        }

        public override void Render()
        {
            var updated = IntelOps.Read(em, root, intelligenceWave);
            if (View != null && View.CompletedGroups != updated.CompletedGroups)
                updated = IntelOps.Read(em, root, intelligenceWave = 0);
            View = updated;
            intelligenceWave = View.SelectedWave;
            var view = View;
            Row("情报模式 · 只查看与移动镜头，不暂停夜晚");
            Row("退出情报模式", () => Navigation.OpenPanel(GamePanelId.Building));
            Row("当前完善度 " + view.Current + "/100 · 本夜已知 " + view.Known + "/100 · " + new[] { "未知", "低档", "中档", "高档" }[view.Tier] + (view.Tier == 3 ? "" : "\n下一档还需 " + math.max(0, view.NextPoints) + " 点"));
            Row("当晚军情");
            foreach (var line in view.Lines)
                Row(line);
            if (!view.Day && view.WaveChoices > 1)
            {
                Row("查看前一组已侦察方向", intelligenceWave > 0 ? () =>
                {
                    intelligenceWave--;
                    Session.NextRefresh = 0;
                } : null);
                Row("查看后一组已侦察方向", intelligenceWave + 1 < view.WaveChoices ? () =>
                {
                    intelligenceWave++;
                    Session.NextRefresh = 0;
                } : null);
            }

            Row("图例：橙色虚线区域＝可能来袭入口；红色十字区域＝可能受袭建筑区域。浅色表示其他已知入口。区域不是精确格或路线。");
            Court.CourtIntelRows(view.Known);
            Row(view.Day ? "情报来源（白天实时更新）" : "情报来源现状（本夜已锁定，变化不撤销已知军情）");
            if (view.Sources.Count == 0)
                Row("暂无情报来源");
            foreach (var source in view.Sources)
            {
                var owner = source.Building;
                Row(source.Name + " · " + source.Effective + "/" + source.Points + " 点 · " + source.Reason, owner == 0 ? null : () =>
                {
                    var e = Sim.Find(em, owner);
                    if (e == Entity.Null)
                        return;
                    World.LocateHistory(owner, Sim.Position(em, e));
                });
            }

            if (view.Unread && em.HasComponent<RecoveryState>(root))
                Commands.TryQueue(CommandRequests.ReadIntelligence(em.GetComponentData<RecoveryState>(root).IntelFingerprint));
        }
    }
}
