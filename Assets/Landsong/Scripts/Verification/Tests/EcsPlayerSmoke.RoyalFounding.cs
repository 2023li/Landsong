#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator FoundRoyalUi(UI_GamePanel view, EntityManager em, Entity root)
        {
            Require(CourtOps.Monarch(em) == Entity.Null && !FeatureOps.Unlocked(em, root, "Royal"),
                "New game starts without a monarch and keeps royal features locked");
            Entity palace = Entity.Null;
            using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                foreach (var building in buildings)
                    if (em.HasComponent<BuildingHousingStats>(building)
                        && em.GetComponentData<BuildingHousingStats>(building).IsCore != 0)
                    {
                        palace = building;
                        break;
                    }
            Require(palace != Entity.Null, "New game has a core palace");
            var experience = em.GetComponentData<BuildingExperienceState>(palace);
            experience.Experience = 10;
            em.SetComponentData(palace, experience);
            Require(GameRequestExecution.Execute(em, root, new UpgradeBuildingRequest
            {
                Building = em.GetComponentData<Identity>(palace).Id
            }) == ResultCode.Success, "Palace upgrades by the normal gameplay request");
            yield return WaitFor(() => view.RoyalFounding.IsOpen, "Level 2 palace opens the royal founding dialog");
            Require(!view.InputPolicy.Capture().CanNavigate
                && view.InputPolicy.Capture().CanQueue(CommandKind.FoundRoyal)
                && !view.InputPolicy.Capture().CanQueue(CommandKind.Advance),
                "Founding dialog owns input and blocks underlying gameplay");
            view.RoyalFounding.NameInput.text = "测试君主";
            view.RoyalFounding.FemaleButton.onClick.Invoke();
            view.RoyalFounding.ConfirmButton.onClick.Invoke();
            yield return WaitFor(() => FeatureOps.Unlocked(em, root, "Royal"),
                "Confirming name and gender unlocks the royal system");
            var monarch = CourtOps.Monarch(em);
            Require(monarch != Entity.Null && em.GetComponentData<Identity>(monarch).Name == new FixedString128Bytes("测试君主")
                && em.GetComponentData<Royal>(monarch).Gender == PersonGender.Female,
                "Founding command creates the named female player monarch");
            yield return WaitFor(() => !view.RoyalFounding.IsOpen, "Founding dialog closes after success");
        }
    }
}
#endif
