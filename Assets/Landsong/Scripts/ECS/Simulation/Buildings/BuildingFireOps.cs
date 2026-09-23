using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class BuildingFireOps
    {
        public static int CurrentPhase(EntityManager em, Entity root)
        {
            var turn = em.GetComponentData<GameClock>(root).Turn;
            var phase = em.GetComponentData<Session>(root).Phase;
            return turn * 2 + (phase == Phase.Day || phase == Phase.Settlement ? 0 : 1);
        }

        public static bool Ignite(EntityManager em, Entity root, Entity building)
        {
            if (!BuildingStatus.Operational(em, building) || em.GetComponentData<BuildingHousingStats>(building).IsCore != 0)
                return false;
            var weather = em.GetComponentData<SeasonWeatherState>(root);
            var fire = em.GetComponentData<BuildingFireState>(building);
            fire.Burning = 1;
            fire.StartedTurn = em.GetComponentData<GameClock>(root).Turn;
            fire.StartStrike = weather.LightningCount;
            fire.DeadlinePhase = fire.StartedTurn * 2 + 1;
            fire.FailedStation = 0;
            em.SetComponentData(building, fire);
            SetInventoryAvailability(em, root, building, 1);
            var id = em.GetComponentData<Identity>(building);
            SimulationEvents.Emit(em, root, EventKind.Message, id.Name + "遭雷击起火", id.Id, category: HistoryCategory.Important);
            BuildingChangeNotifications.Publish(em, root);
            return true;
        }

        public static void Extinguish(EntityManager em, Entity root, Entity building)
        {
            if (!em.Exists(building) || !em.HasComponent<BuildingFireState>(building))
                return;
            var fire = em.GetComponentData<BuildingFireState>(building);
            if (fire.Burning == 0)
                return;
            fire.Burning = 0;
            em.SetComponentData(building, fire);
            if (em.GetComponentData<Building>(building).Stage == LifeStage.Operational)
                SetInventoryAvailability(em, root, building, 0);
            var id = em.GetComponentData<Identity>(building);
            SimulationEvents.Emit(em, root, EventKind.Message, id.Name + "的火灾已扑灭", id.Id);
            BuildingChangeNotifications.Publish(em, root);
        }

        public static void TaskFailed(EntityManager em, Entity root, Entity building, ulong stationId)
        {
            if (building == Entity.Null || !em.HasComponent<BuildingFireState>(building))
                return;
            var fire = em.GetComponentData<BuildingFireState>(building);
            if (fire.Burning == 0)
                return;
            fire.DeadlinePhase = CurrentPhase(em, root) + 1;
            fire.FailedStation = stationId;
            em.SetComponentData(building, fire);
        }

        public static void EnterNight(EntityManager em, Entity root)
            => EnterPhase(em, root, em.GetComponentData<GameClock>(root).Turn * 2 + 1);

        public static void EnterDay(EntityManager em, Entity root)
            => EnterPhase(em, root, em.GetComponentData<GameClock>(root).Turn * 2 + 2);

        static void EnterPhase(EntityManager em, Entity root, int nextPhase)
        {
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var building in buildings)
            {
                if (!em.HasComponent<BuildingFireState>(building))
                    continue;
                var fire = em.GetComponentData<BuildingFireState>(building);
                if (fire.Burning == 0 || HasOutboundResponder(em, root, em.GetComponentData<Identity>(building).Id) || fire.DeadlinePhase > nextPhase)
                    continue;
                fire.Burning = 0;
                em.SetComponentData(building, fire);
                BuildingLifecycle.Ruin(em, root, building);
            }
        }

        public static bool HasOutboundResponder(EntityManager em, Entity root, ulong fireId)
        {
            using var responders = WorldQueries.Entities<Firefighter>(em);
            foreach (var entity in responders)
            {
                var response = em.GetComponentData<Firefighter>(entity);
                if (response.Fire != fireId || response.Stage != FirefighterStage.Outbound || !EntityState.Alive(em, entity))
                    continue;
                var station = WorldQueries.Find(em, response.Station);
                if (BuildingStatus.Operational(em, station))
                    return true;
            }
            return false;
        }

        static void SetInventoryAvailability(EntityManager em, Entity root, Entity building, byte unavailable)
        {
            var id = em.GetComponentData<Identity>(building).Id;
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].Provider == id)
                {
                    var slot = slots[i];
                    slot.Unavailable = unavailable;
                    slots[i] = slot;
                }
        }
    }
}
