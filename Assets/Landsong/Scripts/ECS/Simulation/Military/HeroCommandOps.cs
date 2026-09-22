using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class HeroCommandOps
    {
        public static ResultCode Select(EntityManager em, Entity root, SelectHeroRequest request)
        {
            var target = WorldQueries.Find(em, request.Hero);
            if (target != Entity.Null && (!em.HasComponent<Hero>(target) || !EntityState.Alive(em, target) || em.GetComponentData<Combatant>(target).Deployed == 0))
                return ResultCode.InvalidTarget;
            em.SetComponentData(root, new HeroSelection { SelectedHero = target });
            return ResultCode.Success;
        }

        public static ResultCode Move(EntityManager em, Entity root, MoveHeroRequest request) => Issue(em, root, OrderKind.Move, Entity.Null, request.Destination);
        public static ResultCode Recall(EntityManager em, Entity root)
        {
            var hero = em.GetComponentData<HeroSelection>(root).SelectedHero;
            if (!EntityState.Alive(em, hero))
                return ResultCode.InvalidTarget;
            return Issue(em, root, OrderKind.Recall, Entity.Null, em.GetComponentData<Combatant>(hero).Home);
        }

        public static ResultCode Focus(EntityManager em, Entity root, FocusHeroRequest request)
        {
            var target = WorldQueries.Find(em, request.Enemy);
            if (!EntityState.Alive(em, target) || !em.HasComponent<Combatant>(target) || em.GetComponentData<Combatant>(target).Faction != 1)
                return ResultCode.InvalidTarget;
            return Issue(em, root, OrderKind.Focus, target, EntityState.Position(em, target));
        }

        static ResultCode Issue(EntityManager em, Entity root, OrderKind kind, Entity target, float3 destination)
        {
            var hero = em.GetComponentData<HeroSelection>(root).SelectedHero;
            if (!EntityState.Alive(em, hero) || !math.all(math.isfinite(destination)))
                return ResultCode.InvalidTarget;
            var grid = em.GetComponentData<GridData>(root);
            if (!GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, destination)))
                return ResultCode.InvalidTarget;
            using (var reach = new NightSpatialOps.Reach(em, root, EntityState.Position(em, hero)))
                if (!reach.Point(destination))
                    return ResultCode.InvalidTarget;
            if (kind == OrderKind.Move)
            {
                var actor = em.GetComponentData<Combatant>(hero);
                actor.Home = destination;
                em.SetComponentData(hero, actor);
            }

            em.SetComponentData(hero, new NavigationState { Revision = -1 });
            em.GetBuffer<Waypoint>(hero).Clear();
            em.SetComponentData(hero, new Steering());
            em.SetComponentData(hero, new UnitOrder { Kind = kind, Target = target, Destination = destination });
            return ResultCode.Success;
        }
    }
}
