using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS.Persistence
{
    internal static class MilitaryStateValidation
    {
        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data)
        {
            var sites = data.Records.OfType<BuildingSnapshot>().ToDictionary(row => row.Identity.Id);
            var slots = new HashSet<(ulong, int)>();
            foreach (var home in sites.Values)
                if (home.BuildingRecruitment.Turn < 0 || home.BuildingRecruitment.Turn > data.Clock.Turn || home.BuildingRecruitment.Count < 0 || home.BuildingRecruitment.Turn == 0 && home.BuildingRecruitment.Count != 0)
                    throw new InvalidDataException("Invalid garrison recruitment ledger");
            foreach (var record in data.Records.OfType<SoldierSnapshot>())
            {
                var soldier = record.Soldier;
                ref var definition = ref SoldierDefinitions.Get(em, root, record.Definition);
                if (soldier.PopulationCost < 0 || soldier.Experience < 0 || soldier.PendingSince < 0 || soldier.PendingSince > data.Clock.Turn || soldier.LastExperienceTurn < 0 || soldier.LastExperienceTurn > data.Clock.Turn || soldier.RecallState != 0 || soldier.Weapon > SoldierWeaponKind.Club || soldier.Slot < 0 || soldier.Experience > UnitProgression.LevelThreshold(definition.Growth, definition.Growth.MaxLevel))
                    throw new InvalidDataException("Invalid soldier state");
                if (soldier.Garrison == 0)
                {
                    if (soldier.Slot != 0)
                        throw new InvalidDataException("Unassigned soldier owns a slot");
                    continue;
                }

                if (!sites.TryGetValue(soldier.Garrison, out var home) || soldier.Slot < 1 || !slots.Add((soldier.Garrison, soldier.Slot)))
                    throw new InvalidDataException("Missing or duplicate garrison slot");
                ref var building = ref BuildingDefinitions.Get(em, root, home.Definition);
                int capacity = 0;
                for (int i = 0; i < building.Capabilities.Garrison.Levels.Length; i++)
                {
                    var row = building.Capabilities.Garrison.Levels[i];
                    if (row.Level == 0 || row.Level == home.Building.Level)
                        capacity = row.Capacity;
                }

                if (data.Session.Phase == Phase.Deployment && (home.Building.Stage != LifeStage.Operational || soldier.Slot > capacity))
                    throw new InvalidDataException("Invalid dusk garrison capacity or stage");
            }

            var heroes = new HashSet<HeroId>();
            foreach (var record in data.Records.OfType<HeroSnapshot>())
            {
                var hero = record.Hero;
                if (!heroes.Add(record.Definition) || hero.Experience < 0 || hero.CooldownUntil < 0 || hero.LastCombatTurn < 0 || hero.LastCombatTurn > data.Clock.Turn || hero.Recruited > 1 || hero.DeathPending != 0 || hero.Recruited == 0 && hero.Experience != 0)
                    throw new InvalidDataException("Invalid hero checkpoint state");
                if (hero.Recruited != 0 && (!sites.TryGetValue(hero.Sanctum, out var sanctum) || sanctum.Building.Stage == LifeStage.Ruined || sanctum.Building.Stage == LifeStage.Repairing))
                    throw new InvalidDataException("Living hero requires an operational sanctum");
            }
        }
    }
}
