using System;

namespace Landsong.ECS.Presentation
{
    public abstract class GameFeatureViewBase : Moyo.Unity.UIViewBase, IGameFeatureRenderer
    {
        internal GameUiSessionHandle sessionController;
        internal GameUiRefreshScheduler refresh;
        internal GameUiCommandWriter commandsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_List rowsController;
        public void BindFeature(GameUiSessionHandle session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_List rows, GameUiRefreshScheduler refresh)
        {
            if (session == null || commands == null || navigation == null || rows == null)
                throw new ArgumentException("功能展示器缺少会话服务。");
            sessionController = session;
            commandsController = commands;
            this.refresh = refresh;
            this.navigation = navigation;
            rowsController = rows;
        }

        public abstract void Render();
    }
}
