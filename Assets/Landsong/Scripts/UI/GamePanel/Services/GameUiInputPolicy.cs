using System;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [Flags]
    public enum GameUiInputOwner
    {
        [LabelText("无模态")] None = 0,
        [LabelText("应用共享模态")] Application = 1,
        [LabelText("士兵详情")] SoldierDetails = 2,
        [LabelText("塑造容貌")] Portrait = 4,
        [LabelText("人物请求")] PersonRequests = 8,
        [LabelText("婚姻交互")] Marriage = 16,
        [LabelText("暂停菜单")] Pause = 32,
        [LabelText("建筑操作确认")] BuildingConfirmation = 64
    }

    /// <summary>由 Game 根注入固定所有者；每次事件读取当前状态，不维护另一份模态开关。</summary>
    public sealed class GameUiInputPolicy
    {
        readonly GameUiSession session;
        readonly IGameUiNavigation navigation;
        readonly UI_GamePanel_Building building;
        readonly UI_GamePanel_Soldier soldier;
        readonly UI_GamePanel_Portrait portrait;
        readonly UI_GamePanel_PersonRequests requests;
        readonly UI_GamePanel_Marriage marriage;

        public GameUiInputPolicy(GameUiSession session, IGameUiNavigation navigation,
            UI_GamePanel_Building building, UI_GamePanel_Soldier soldier, UI_GamePanel_Portrait portrait,
            UI_GamePanel_PersonRequests requests, UI_GamePanel_Marriage marriage)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.building = building != null ? building : throw new ArgumentNullException(nameof(building));
            this.soldier = soldier != null ? soldier : throw new ArgumentNullException(nameof(soldier));
            this.portrait = portrait != null ? portrait : throw new ArgumentNullException(nameof(portrait));
            this.requests = requests != null ? requests : throw new ArgumentNullException(nameof(requests));
            this.marriage = marriage != null ? marriage : throw new ArgumentNullException(nameof(marriage));
        }

        public GameUiInputSnapshot Capture()
        {
            var owners = UiInputState.GlobalModalOpen ? GameUiInputOwner.Application : GameUiInputOwner.None;
            if (soldier.SoldierDetailsOpen) owners |= GameUiInputOwner.SoldierDetails;
            if (portrait.PortraitOpen) owners |= GameUiInputOwner.Portrait;
            if (requests.PersonRequestsOpen) owners |= GameUiInputOwner.PersonRequests;
            if (marriage.MarriageOpen) owners |= GameUiInputOwner.Marriage;
            var pause = navigation.PauseMenu;
            if (pause != null && pause.IsOpen) owners |= GameUiInputOwner.Pause;
            if (building.BuildingConfirmPanel != null && building.BuildingConfirmPanel.activeSelf)
                owners |= GameUiInputOwner.BuildingConfirmation;
            return Evaluate(session.IsBound, EcsSceneFlow.GameReady, owners, session.intel,
                UiInputState.TextFocused, navigation.Panel, pause != null);
        }

        public static GameUiInputSnapshot Evaluate(bool sessionBound, bool gameReady,
            GameUiInputOwner owners, bool intelligence, bool textFocused, GamePanelId panel, bool hasPauseMenu)
            => new GameUiInputSnapshot(sessionBound, gameReady, owners, intelligence, textFocused, panel, hasPauseMenu);
    }

    /// <summary>同一次输入分派使用同一快照；命令限制取所有活跃模态的交集，返回只交给最高优先级。</summary>
    public readonly struct GameUiInputSnapshot
    {
        [Flags]
        enum CommandAccess { Gameplay = 1, Pause = 2, Archive = 4, SoldierRename = 8, Intelligence = 16, Camera = 32, All = 63 }

        static readonly (GameUiInputOwner Owner, CommandAccess Commands)[] Rules =
        {
            (GameUiInputOwner.Application, CommandAccess.Pause | CommandAccess.Archive),
            (GameUiInputOwner.SoldierDetails, CommandAccess.Pause | CommandAccess.SoldierRename),
            (GameUiInputOwner.Portrait, CommandAccess.Pause),
            (GameUiInputOwner.PersonRequests, CommandAccess.Pause),
            (GameUiInputOwner.Marriage, CommandAccess.Pause),
            (GameUiInputOwner.Pause, CommandAccess.Pause | CommandAccess.Archive),
            (GameUiInputOwner.BuildingConfirmation, CommandAccess.Pause)
        };

        public GameUiInputOwner Owners { get; }
        public bool SessionBound { get; }
        public bool GameReady { get; }
        public bool Intelligence { get; }
        public bool TextFocused { get; }
        public GamePanelId Panel { get; }
        readonly bool hasPauseMenu;

        internal GameUiInputSnapshot(bool sessionBound, bool gameReady, GameUiInputOwner owners,
            bool intelligence, bool textFocused, GamePanelId panel, bool hasPauseMenu)
        {
            SessionBound = sessionBound; GameReady = gameReady; Owners = owners;
            Intelligence = intelligence; TextFocused = textFocused; Panel = panel; this.hasPauseMenu = hasPauseMenu;
        }

        public bool HasOwner(GameUiInputOwner owner) => (Owners & owner) != 0;
        public bool CanNavigate => SessionBound && Owners == GameUiInputOwner.None;
        public bool CanHandleBack => SessionBound && !HasOwner(GameUiInputOwner.Application);
        public bool CanWorldInput => CanNavigate && Panel != GamePanelId.Technology && Panel != GamePanelId.Quest;
        public bool CanWorldShortcuts => CanWorldInput && !TextFocused && !Intelligence;
        public bool CanWorldActions => CanWorldInput && !TextFocused && !Intelligence;
        public bool CanTogglePause => CanHandleBack && hasPauseMenu
            && (Owners & ~(GameUiInputOwner.Pause | GameUiInputOwner.BuildingConfirmation)) == 0;

        // 此组不包含私有模态本身；它保留现有 CanvasGroup 分组的交互边界。
        public bool CanInteractWithBackgroundGroup => SessionBound
            && !HasOwner(GameUiInputOwner.Application | GameUiInputOwner.Pause | GameUiInputOwner.BuildingConfirmation);

        public bool CanOpenModal(GameUiInputOwner requested, bool allowReopen = false)
        {
            if (requested != GameUiInputOwner.SoldierDetails && requested != GameUiInputOwner.Portrait
                && requested != GameUiInputOwner.PersonRequests && requested != GameUiInputOwner.Marriage
                && requested != GameUiInputOwner.BuildingConfirmation)
                throw new ArgumentOutOfRangeException(nameof(requested));
            return SessionBound && !Intelligence && (Owners & ~(allowReopen ? requested : GameUiInputOwner.None)) == 0;
        }

        public GameUiInputOwner BackOwner(bool includeClosedPause = false)
        {
            if (!CanHandleBack) return GameUiInputOwner.None;
            // Rules 的顺序同时定义既有局部返回顺序；建筑取消由调用方的本地返回处理。
            foreach (var rule in Rules)
            {
                if (rule.Owner == GameUiInputOwner.Application || rule.Owner == GameUiInputOwner.BuildingConfirmation) continue;
                if (HasOwner(rule.Owner) || rule.Owner == GameUiInputOwner.Pause && includeClosedPause && hasPauseMenu)
                    return rule.Owner;
            }
            return GameUiInputOwner.None;
        }

        public bool CanQueue(CommandKind kind)
        {
            if (!SessionBound || !GameReady) return false;
            var access = Intelligence
                ? CommandAccess.Pause | CommandAccess.Archive | CommandAccess.Intelligence | CommandAccess.Camera
                : CommandAccess.All;
            foreach (var rule in Rules)
                if (HasOwner(rule.Owner)) access &= rule.Commands;
            return (access & AccessFor(kind)) != 0;
        }

        static CommandAccess AccessFor(CommandKind kind)
        {
            switch (kind)
            {
                case CommandKind.Pause: return CommandAccess.Pause;
                case CommandKind.Save:
                case CommandKind.Load: return CommandAccess.Archive;
                case CommandKind.RenameSoldier: return CommandAccess.SoldierRename;
                case CommandKind.IntelligenceMode:
                case CommandKind.ReadIntelligence: return CommandAccess.Intelligence;
                case CommandKind.CameraMoved:
                case CommandKind.CameraZoomed: return CommandAccess.Camera;
                default: return CommandAccess.Gameplay;
            }
        }
    }
}
