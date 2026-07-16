using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.ExpeditionSystem;
using Landsong.GameEventSystem;
using Landsong.InheritanceSystem;
using Landsong.InventorySystem;
using Landsong.PolicySystem;
using Landsong.TalentSystem;
using Landsong.TechnologySystem;
using Landsong.TurnSystem;

namespace Landsong
{
    /// <summary>
    /// GameSystem 的唯一公开领域入口。
    /// 业务代码通过该目录访问服务，避免同时使用 GameSystem 代理方法和服务本体。
    /// </summary>
    public sealed class GameServices
    {
        private readonly GameSystem owner;

        internal GameServices(GameSystem owner)
        {
            this.owner = owner;
        }

        public InventoryService Inventory => owner.Inventory;
        public EconomyForecastService EconomyForecast => owner.EconomyForecast;
        public TurnService Turn => owner.Turn;
        public DynastyService Dynasty => owner.Dynasty;
        public BuildingService Buildings => owner.Buildings;
        public GameEventService Events => owner.Events;
        public TechnologyService Technology => owner.Technology;
        public PolicyService Policies => owner.Policies;
        public QuestService Quest => owner.Quest;
        public ExpeditionService Expeditions => owner.Expeditions;
        public TalentService Talents => owner.Talents;
        public RoyalInheritanceService Inheritance => owner.Inheritance;
        public BuildingBlueprintService BuildingBlueprints => owner.BuildingBlueprints;
        public BuildingSelectionController BuildingSelection => owner.BuildingSelection;
        public BuildingCatalog BuildingCatalog => owner.BuildingCatalog;
        public PolicyCatalog PolicyCatalog => owner.PolicyCatalog;

        public TechnologyUnlockContentRegistry TechnologyUnlockContents => owner.TechnologyUnlockContents;
        public TechnologyGlobalBuffService GlobalBuffs => owner.GlobalBuffs;
    }

    public sealed partial class GameSystem
    {
        private GameServices gameServices;

        public GameServices Services => gameServices ??= new GameServices(this);

        internal QuestService Quest { get; private set; }
        internal BuildingBlueprintService BuildingBlueprints { get; private set; }
    }
}
