using System;
using System.Linq;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        TMP_Text intelligenceButtonLabel;
        int intelligenceWave;
        IntelView intelligenceView;
        public bool InIntelligenceMode => intel;
        void InitializeIntelligence()
        {
            var button = GetComponentInParent<Canvas>(true).GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.GetComponentInChildren<TMP_Text>(true)?.text == "情报");
            if (button != null) intelligenceButtonLabel = button.GetComponentInChildren<TMP_Text>(true);
        }
        void RefreshIntelligenceBadge()
        {
            if (intelligenceButtonLabel == null || !em.HasComponent<RecoveryState>(root)) return;
            var r = em.GetComponentData<RecoveryState>(root);
            intelligenceButtonLabel.text = r.IntelFingerprint != r.IntelReadFingerprint ? "情报 •" : "情报";
        }
        void Intelligence()
        {
            var updated = IntelOps.Read(em, root, intelligenceWave);
            if (intelligenceView != null && intelligenceView.CompletedGroups != updated.CompletedGroups) updated = IntelOps.Read(em, root, intelligenceWave = 0);
            intelligenceView = updated; intelligenceWave = intelligenceView.SelectedWave;
            var view = intelligenceView;
            Row("情报模式 · 只查看与移动镜头，不暂停夜晚");
            Row("退出情报模式", () => OpenPanel("建筑"));
            Row("当前完善度 " + view.Current + "/100 · 本夜已知 " + view.Known + "/100 · " + new[] { "未知", "低档", "中档", "高档" }[view.Tier]
                + (view.Tier == 3 ? "" : "\n下一档还需 " + math.max(0, view.NextPoints) + " 点"));
            Row("当晚军情");
            foreach (var line in view.Lines) Row(line);
            if (!view.Day && view.WaveChoices > 1)
            {
                Row("查看前一组已侦察方向", intelligenceWave > 0 ? () => { intelligenceWave--; nextRefresh = 0; } : null);
                Row("查看后一组已侦察方向", intelligenceWave + 1 < view.WaveChoices ? () => { intelligenceWave++; nextRefresh = 0; } : null);
            }
            Row("图例：橙色虚线区域＝可能来袭入口；红色十字区域＝可能受袭建筑区域。浅色表示其他已知入口。区域不是精确格或路线。");
            CourtIntelRows(view.Known);
            Row(view.Day ? "情报来源（白天实时更新）" : "情报来源现状（本夜已锁定，变化不撤销已知军情）");
            if (view.Sources.Count == 0) Row("暂无情报来源");
            foreach (var source in view.Sources)
            {
                var owner = source.Building;
                Row(source.Name + " · " + source.Effective + "/" + source.Points + " 点 · " + source.Reason, owner == 0 ? null : () =>
                {
                    var e = Sim.Find(em, owner); if (e == Entity.Null) return;
                    LocateHistory(owner,Sim.Position(em,e));
                });
            }
            if (view.Unread && em.HasComponent<RecoveryState>(root))
                Send(CommandKind.ReadIntelligence, other: em.GetComponentData<RecoveryState>(root).IntelFingerprint);
        }
        void DrawIntelligence(Action<float3, Vector3, Color> draw)
        {
            if (!intel || intelligenceView == null || intelligenceView.Tier < 2) return;
            foreach (var area in intelligenceView.Areas)
            {
                var color = area.Target ? new Color(1, .12f, .15f, .20f) : new Color(1, .57f, .06f, area.Secondary ? .07f : .20f);
                var grid = em.GetComponentData<GridData>(root); var p = area.Center;
                p.y = GridOps.Position(grid, GridOps.Cell(grid, p), new int2(1)).y + .15f;
                draw(p, new Vector3(area.Size.x, .06f, area.Size.z), color);
                color.a = area.Secondary ? .25f : .85f;
                // Broken outline versus central cross remains distinguishable without red/orange color perception.
                float width = Mathf.Clamp(Camera.orthographicSize * .018f, .12f, .8f);
                if (area.Target)
                {
                    draw(p, new Vector3(area.Size.x * .7f, .08f, width), color);
                    draw(p, new Vector3(width, .08f, area.Size.z * .7f), color);
                }
                else for (int i = 0; i < 8; i++)
                {
                    float t = (i + .5f) / 8 - .5f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        draw(p + new float3(t * area.Size.x, 0, side * area.Size.z * .5f), new Vector3(area.Size.x / 16, .08f, width), color);
                        draw(p + new float3(side * area.Size.x * .5f, 0, t * area.Size.z), new Vector3(width, .08f, area.Size.z / 16), color);
                    }
                }
            }
        }
    }
}
