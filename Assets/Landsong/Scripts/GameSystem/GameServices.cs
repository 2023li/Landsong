using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.ExpeditionSystem;
using Landsong.GameEventSystem;
using Landsong.InheritanceSystem;
using Landsong.InventorySystem;
using Landsong.TalentSystem;
using Landsong.TechnologySystem;
using Landsong.TurnSystem;

namespace Landsong
{
    /// <summary>
    /// GameSystem 组合根中的类型化服务目录。
    /// 它只提供当前服务引用，不复制业务门面，避免 GameSystem 继续增加同义转发 API。
    /// </summary>
    public sealed class GameServices
    {
        private readonly GameSystem owner;

        internal GameServices(GameSystem owner)
        {
            this.owner = owner;
        }

        public InventoryService Inventory => owner.Inventory;
        public TurnService Turn => owner.Turn;
        public DynastyService Dynasty => owner.Dynasty;
        public BuildingService Buildings => owner.Buildings;
        public GameEventService Events => owner.Events;
        public TechnologyService Technology => owner.Technology;
        public QuestService Quest => owner.Quest;
        public ExpeditionService Expeditions => owner.Expeditions;
        public TalentService Talents => owner.Talents;
        public RoyalInheritanceService Inheritance => owner.Inheritance;
    }

    public sealed partial class GameSystem
    {
        private GameServices gameServices;

        public GameServices Services => gameServices ??= new GameServices(this);
        public QuestService Quest { get; private set; }

        private void EnsureQuestFacade()
        {
            Quest ??= new QuestService(this);
        }
    }
}
