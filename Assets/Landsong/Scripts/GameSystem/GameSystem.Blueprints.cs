using Landsong.BuildingSystem;

namespace Landsong
{
    public sealed partial class GameSystem
    {
        private void CreateBuildingBlueprintService()
        {
            BuildingBlueprints = new BuildingBlueprintService(startingUnlockedBuildingBlueprintIds);
            ReconcileInitiallyUnlockedBuildingBlueprints();
            ReconcileBuildingBlueprintsFromAutomaticConditions();
        }

        internal void ReconcileInitiallyUnlockedBuildingBlueprints()
        {
            if (BuildingBlueprints == null || BuildingCatalog == null)
            {
                return;
            }

            var prefabs = BuildingCatalog.BuildingPrefabs;
            for (var i = 0; i < prefabs.Count; i++)
            {
                var building = prefabs[i];
                if (building != null
                    && building.HasDefinition
                    && !building.Definition.BlueprintInitiallyLocked)
                {
                    BuildingBlueprints.Unlock(building.FamilyId);
                }
            }
        }

        internal void ReconcileBuildingBlueprintsFromAutomaticConditions()
        {
            if (BuildingCatalog == null || BuildingBlueprints == null)
            {
                return;
            }

            var families = BuildingCatalog.Families;
            for (var i = 0; i < families.Count; i++)
            {
                var definition = families[i]?.Definition;
                var condition = definition?.AutomaticBlueprintUnlockCondition;
                if (definition == null
                    || condition == null
                    || !condition.IsMet(this))
                {
                    continue;
                }

                BuildingBlueprints.Unlock(definition.FamilyId);
            }
        }
    }
}
