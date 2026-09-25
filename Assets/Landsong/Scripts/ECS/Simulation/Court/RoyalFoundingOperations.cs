using Unity.Collections;
using Unity.Entities;
using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    public static class RoyalFoundingOps
    {
        public static ResultCode Found(EntityManager em, Entity root, FixedString128Bytes name, PersonGender gender)
        {
            if (!CanFound(em, root) || string.IsNullOrWhiteSpace(name.ToString())
                || gender != PersonGender.Male && gender != PersonGender.Female)
                return ResultCode.Unavailable;

            var monarch = DynastyOps.CreateRoyal(em, root, name, 0, 20);
            var person = em.GetComponentData<Royal>(monarch);
            person.Gender = gender;
            em.SetComponentData(monarch, person);
            PortraitOps.Ensure(em, root, monarch);
            var court = CourtOps.State(em, root);
            court.VisitOfferTurn = 0;
            court.VisitResolved = 0;
            em.SetComponentData(root, court);
            var feature = FeatureDefinitions.Find(em, root, new FixedString128Bytes("feature.Royal"));
            FeatureUnlocks.Unlock(em, root, feature);
            CourtOps.Log(em, root, "民众拥立新君主", em.GetComponentData<Identity>(monarch).Id,
                HistoryCategory.Important);
            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }

        public static bool CanFound(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasBuffer<UnlockedFeature>(root)
                || !em.HasComponent<Session>(root)
                || em.GetComponentData<Session>(root).Phase != Phase.Day)
                return false;
            var feature = FeatureDefinitions.Find(em, root, new FixedString128Bytes("feature.Royal"));
            if (!feature.IsValid || FeatureUnlocks.Has(em, root, feature) || CourtOps.Monarch(em) != Entity.Null)
                return false;
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var building in buildings)
                if (em.HasComponent<SimulationOwner>(building)
                    && em.GetComponentData<SimulationOwner>(building).Root == root
                    && em.HasComponent<BuildingHousingStats>(building)
                    && em.GetComponentData<BuildingHousingStats>(building).IsCore != 0
                    && BuildingStatus.Operational(em, building)
                    && em.GetComponentData<Building>(building).Level >= 2)
                    return true;
            return false;
        }
    }
}
