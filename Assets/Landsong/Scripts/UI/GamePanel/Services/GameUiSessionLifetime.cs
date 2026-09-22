using System;
using System.Collections.Generic;

namespace Landsong.ECS.Presentation
{
    /// <summary>Owns session attachment and always attempts every registered view release before detaching.</summary>
    public sealed class GameUiSessionLifetime
    {
        readonly Action[] release;
        readonly GameUiSessionHandle session;
        readonly GameUiRefreshScheduler refresh;
        readonly WorldSelectionState selection;
        readonly IntelligenceViewState intelligence;
        readonly GameUiCommandWriter commands;
        readonly GamePanelNavigator navigation;
        public bool IsBound { get; private set; }

        public GameUiSessionLifetime(GameUiSessionHandle session, GameUiRefreshScheduler refresh, WorldSelectionState selection, IntelligenceViewState intelligence, GameUiCommandWriter commands, GamePanelNavigator navigation, Action[] release)
        {
            this.session = session;
            this.refresh = refresh;
            this.selection = selection;
            this.intelligence = intelligence;
            this.commands = commands;
            this.navigation = navigation;
            this.release = release;
        }

        public void Bind(Action initialize)
        {
            Unbind();
            IsBound = true;
            try
            {
                initialize();
            }
            catch (Exception error)
            {
                try
                {
                    Unbind();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("游戏会话绑定及清理失败。", error, cleanup);
                }

                throw;
            }
        }

        public void Unbind()
        {
            if (!IsBound)
                return;
            IsBound = false;
            var errors = new List<Exception>();
            try
            {
                foreach (var action in release)
                    try
                    {
                        action();
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
            }
            finally
            {
                navigation.Reset();
                session.Unbind();
                refresh.Reset();
                selection.Reset();
                intelligence.Reset();
                commands.ResetSequence();
            }

            if (errors.Count > 0)
                throw new AggregateException("游戏会话清理失败。", errors);
        }
    }
}
