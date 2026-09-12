#if UNITY_EDITOR
using System;
using Landsong.ECS.Presentation;

namespace Landsong.ECS.Editor
{
    public static class GameUiInputPolicyVerification
    {
        public static string Run()
        {
            int assertions = 0;
            void Check(bool condition, string message)
            {
                assertions++;
                if (!condition) throw new InvalidOperationException("游戏输入所有权验证失败：" + message);
            }
            GameUiInputSnapshot State(GameUiInputOwner owners = GameUiInputOwner.None, bool intel = false,
                bool focus = false, GamePanelId panel = GamePanelId.Building, bool bound = true, bool ready = true)
                => GameUiInputPolicy.Evaluate(bound, ready, owners, intel, focus, panel, true);

            var empty = State(bound: false);
            Check(!empty.CanNavigate && !empty.CanHandleBack && !empty.CanWorldInput && !empty.CanQueue(CommandKind.Pause), "失效会话没有输入所有权");
            Check(!State(ready: false).CanQueue(CommandKind.Save), "场景事务期间不能提前提交命令");
            var normal = State();
            Check(normal.CanNavigate && normal.CanWorldShortcuts && normal.CanQueue(CommandKind.Advance), "正常游戏保留导航与命令");
            Check(normal.BackOwner(true) == GameUiInputOwner.Pause && normal.BackOwner() == GameUiInputOwner.None, "Esc 打开暂停，返回按钮进入本地导航");

            var shared = State(GameUiInputOwner.Application | GameUiInputOwner.Pause);
            Check(!shared.CanNavigate && !shared.CanHandleBack && !shared.CanWorldInput, "共享页占有输入，不穿透 Game");
            Check(shared.CanQueue(CommandKind.Save) && shared.CanQueue(CommandKind.Load) && shared.CanQueue(CommandKind.Pause), "共享存档页保留暂停和存档操作");
            Check(!shared.CanQueue(CommandKind.RenameSoldier) && !shared.CanQueue(CommandKind.Advance), "共享页拒绝底层玩法操作");
            var soldier = State(GameUiInputOwner.SoldierDetails);
            Check(soldier.CanQueue(CommandKind.RenameSoldier) && soldier.CanQueue(CommandKind.Pause) && !soldier.CanQueue(CommandKind.Save), "士兵改名属于详情拥有者");
            Check(soldier.CanOpenModal(GameUiInputOwner.SoldierDetails, true) && !soldier.CanOpenModal(GameUiInputOwner.Portrait), "士兵详情可切人，不能穿透打开其他私有模态");
            var overlap = State(GameUiInputOwner.Application | GameUiInputOwner.SoldierDetails);
            Check(overlap.CanQueue(CommandKind.Pause) && !overlap.CanQueue(CommandKind.Save) && !overlap.CanQueue(CommandKind.RenameSoldier), "叠加拥有者取权限交集，不能只取顶层白名单");

            foreach (var owner in new[] { GameUiInputOwner.Portrait, GameUiInputOwner.PersonRequests, GameUiInputOwner.Marriage, GameUiInputOwner.BuildingConfirmation })
            {
                var modal = State(owner);
                Check(!modal.CanNavigate && !modal.CanWorldInput && !modal.CanQueue(CommandKind.Advance)
                    && modal.CanQueue(CommandKind.Pause), owner + "阻断底层，保留暂停协议");
            }
            var confirm = State(GameUiInputOwner.BuildingConfirmation);
            Check(confirm.BackOwner(true) == GameUiInputOwner.Pause && confirm.BackOwner() == GameUiInputOwner.None, "建筑确认保留 Esc 暂停、返回按钮取消的区别");
            Check(confirm.CanTogglePause && !State(GameUiInputOwner.Marriage).CanTogglePause, "暂停覆盖建筑确认，不抢婚姻返回");
            var owners = GameUiInputOwner.SoldierDetails | GameUiInputOwner.Portrait | GameUiInputOwner.PersonRequests | GameUiInputOwner.Marriage | GameUiInputOwner.Pause;
            foreach (var expected in new[] { GameUiInputOwner.SoldierDetails, GameUiInputOwner.Portrait, GameUiInputOwner.PersonRequests, GameUiInputOwner.Marriage, GameUiInputOwner.Pause })
            {
                Check(State(owners).BackOwner() == expected, "既有局部返回优先级：" + expected);
                owners &= ~expected;
            }

            var intel = State(intel: true);
            Check(intel.CanWorldInput && !intel.CanWorldActions && !intel.CanWorldShortcuts, "情报可看地图，不发玩法快捷键");
            Check(intel.CanQueue(CommandKind.CameraMoved) && intel.CanQueue(CommandKind.ReadIntelligence)
                && intel.CanQueue(CommandKind.IntelligenceMode) && intel.CanQueue(CommandKind.Save), "情报保留镜头、阅读、退出与存档");
            Check(!intel.CanQueue(CommandKind.MoveHero) && !intel.CanQueue(CommandKind.Advance)
                && !intel.CanOpenModal(GameUiInputOwner.Marriage), "情报不穿透英雄和私有交互");
            var focus = State(focus: true);
            Check(focus.CanWorldInput && !focus.CanWorldActions && !focus.CanWorldShortcuts
                && focus.CanQueue(CommandKind.Rename), "输入框占用快捷键，不阻止其自身提交改名");
            Check(!State(panel: GamePanelId.Technology).CanWorldInput && !State(panel: GamePanelId.Quest).CanWorldInput
                && State(panel: GamePanelId.Technology).CanNavigate, "科技与任务隔离地图输入但允许功能导航");
            var noPause = GameUiInputPolicy.Evaluate(true, true, GameUiInputOwner.None, false, false, GamePanelId.Building, false);
            Check(noPause.BackOwner(true) == GameUiInputOwner.None && !noPause.CanTogglePause
                && noPause.CanOpenModal(GameUiInputOwner.Portrait), "无暂停组件保留本地返回和有效私有视图");
            return "GameUiInputPolicy Assertions: " + assertions + " passed";
        }
    }
}
#endif
