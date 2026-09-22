using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Text = TMPro.TextMeshProUGUI;
using System.Text;
using Unity.Transforms;
using UnityEngine.EventSystems;
using InputField = TMPro.TMP_InputField;
using Landsong.ECS.Persistence;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Phase : GameFeatureViewBase
    {
        public override void Render()
        {
            if (navigation.Panel == GamePanelId.DynastyEnd)
                GameOver();
            else if (navigation.Panel == GamePanelId.NightConfirmation)
                ConfirmNight();
            else
                throw new InvalidOperationException("阶段展示器只能配置到终局或入夜确认面板。");
        }

        internal void GameOver()
        {
            if (sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Ended)
            {
                rowsController.Row("王朝已结束，记录已保留。存档已永久删除。");
                rowsController.Row("返回主菜单", EcsSceneFlow.ReturnToMenu);
                return;
            }

            if (CourtOps.State(sessionController.em, sessionController.root).Extinction != 0)
                rowsController.Row("王朝绝嗣：无在世直系后代，不能重试。确认结束后才删除存档。");
            else
            {
                rowsController.Row("聚落核心失守");
                rowsController.Row("回到本回合白天", () => commandsController.TryQueue(new RetryDayRequest()));
                rowsController.Row("重新开始本夜", () => commandsController.TryQueue(new RetryDuskRequest()));
            }

            rowsController.Row("结束王朝（永久删除存档）", () =>
            {
                rowsController.Clear(rowsController.PrimaryRows);
                rowsController.Row("确认结束并永久删除，仅保留王朝记录", () => commandsController.TryQueue(new EndDynastyRequest()));
                rowsController.Row("取消", () => navigation.OpenPanel(GamePanelId.DynastyEnd));
                refresh.NextPanel = float.PositiveInfinity;
            });
        }

        internal void ConfirmNight()
        {
            if (!sessionController.em.HasComponent<NightEntryReview>(sessionController.root) || sessionController.em.GetComponentData<NightEntryReview>(sessionController.root).Token == 0)
            {
                rowsController.Row("请点击下一阶段，重新检查入夜条件。");
                return;
            }

            var token = sessionController.em.GetComponentData<NightEntryReview>(sessionController.root).Token;
            rowsController.Row("按白天结算后的结果预览：以下待存放物资将清空，未驻扎士兵将解散。尚未扣款或推进回合。");
            foreach (var loss in sessionController.em.GetBuffer<NightEntryLoss>(sessionController.root))
                rowsController.Row(loss.Soldier == 0 ? ItemDefinitions.Get(sessionController.em, sessionController.root, loss.Item).Metadata.Name.ToString() + " × " + loss.Amount : loss.Name + " #" + loss.Soldier + " 将解散");
            rowsController.Row("返回白天调整后请重新检查；原确认不会放弃后来新增的物资或士兵。");
            rowsController.Row("返回整理库存", () => navigation.OpenPanel(GamePanelId.Inventory));
            rowsController.Row("返回安排驻军", () => navigation.OpenPanel(GamePanelId.Garrison));
            rowsController.Row("重新检查", () => commandsController.TryQueue(new AdvanceRequest { ReviewedLossToken = 0 }));
            rowsController.Row("确认放弃以上物资与士兵并入夜", () =>
            {
                commandsController.TryQueue(new AdvanceRequest { ReviewedLossToken = token });
                navigation.OpenPanel(GamePanelId.Building);
            });
        }
    }
}
