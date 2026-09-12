using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.ECS.Authoring;
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
    public sealed class UI_GamePanel_Phase : Moyo.Unity.UIViewBase, IGameFeatureRenderer
    {
        public void BindFeature(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_RowRenderer rows)
        {
            if (session == null || commands == null || navigation == null || rows == null)
                throw new System.ArgumentException("功能展示器缺少会话服务。");
            sessionController = session;
            rowsController = rows;
            commandsController = commands;
            this.navigation = navigation;
        }

        public void Render()
        {
            if (navigation.Panel == GamePanelId.DynastyEnd)
                GameOver();
            else if (navigation.Panel == GamePanelId.NightConfirmation)
                ConfirmNight();
            else
                throw new InvalidOperationException("阶段展示器只能配置到终局或入夜确认面板。");
        }

        internal GameUiCommandWriter commandsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
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
                rowsController.Row("回到本回合白天", () => commandsController.Send(CommandKind.RetryDay));
                rowsController.Row("重新开始本夜", () => commandsController.Send(CommandKind.RetryDusk));
            }

            rowsController.Row("结束王朝（永久删除存档）", () =>
            {
                rowsController.Clear(navigation.PrimaryRows);
                rowsController.Row("确认结束并永久删除，仅保留王朝记录", () => commandsController.Send(CommandKind.EndDynasty));
                rowsController.Row("取消", () => navigation.OpenPanel(GamePanelId.DynastyEnd));
                sessionController.nextRefresh = float.PositiveInfinity;
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
                rowsController.Row(loss.Soldier == 0 ? sessionController.Name(loss.Item) + " × " + loss.Amount : loss.Name + " #" + loss.Soldier + " 将解散");
            rowsController.Row("返回白天调整后请重新检查；原确认不会放弃后来新增的物资或士兵。");
            rowsController.Row("返回整理库存", () => navigation.OpenPanel(GamePanelId.Inventory));
            rowsController.Row("返回安排驻军", () => navigation.OpenPanel(GamePanelId.Garrison));
            rowsController.Row("重新检查", () => commandsController.TryQueue(CommandRequests.Advance()));
            rowsController.Row("确认放弃以上物资与士兵并入夜", () =>
            {
                commandsController.TryQueue(CommandRequests.Advance(token));
                navigation.OpenPanel(GamePanelId.Building);
            });
        }
    }
}
