using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class BuildingRangeOps
    {
        public static int ActionPower(EntityManager em, Entity root, Entity building) => math.max(0, em.GetComponentData<BuildingStats>(building).ActionPower + (int)math.floor(Sim.Modifier(em, root, RuleKind.ActionPowerBonus, em.GetComponentData<Identity>(building).Definition)));
        // Full weighted flood for the selected building. Provider selection and overlay share this graph.
        public static NativeArray<float> Reach(EntityManager em, Entity root, Entity building, Allocator allocator)
        {
            var grid = em.GetComponentData<GridData>(root); var occupancy = em.GetBuffer<Occupancy>(root);
            var b = em.GetComponentData<Building>(building); var budget = ActionPower(em, root, building);
            var distance = new NativeArray<float>(occupancy.Length, allocator);
            for (var i = 0; i < distance.Length; i++) distance[i] = float.PositiveInfinity;
            using var endpoints = new NativeHashSet<int>(math.max(1, occupancy.Length), Allocator.Temp);
            using (var buildings = Sim.Entities<Building>(em)) foreach (var candidate in buildings)
            {
                if (!IsProvider(em, candidate)) continue; var p = em.GetComponentData<Building>(candidate);
                for (var y = 0; y < p.Size.y; y++) for (var x = 0; x < p.Size.x; x++) { var index = GridOps.Index(grid, p.Cell + new int2(x, y)); if (index >= 0) endpoints.Add(index); }
            }
            using var queue = new NativeQueue<int>(Allocator.Temp);
            for (var y = 0; y < b.Size.y; y++) for (var x = 0; x < b.Size.x; x++) { var index = GridOps.Index(grid, b.Cell + new int2(x, y)); if (index >= 0) { distance[index] = 0; queue.Enqueue(index); } }
            while (queue.TryDequeue(out var current))
            {
                var cell = grid.Value.Value.Min + new int2(current % grid.Value.Value.Size.x, current / grid.Value.Value.Size.x);
                for (var side = 0; side < 4; side++)
                {
                    var nextCell = cell + (side == 0 ? new int2(1, 0) : side == 1 ? new int2(-1, 0) : side == 2 ? new int2(0, 1) : new int2(0, -1));
                    var next = GridOps.Index(grid, nextCell); if (next < 0 || grid.Value.Value.Cells[next].Exists == 0) continue;
                    if (!endpoints.Contains(next) && !GridOps.Traversable(grid, occupancy, nextCell)) continue;
                    var cost = distance[current] + (occupancy[next].MovementCost > 0 ? occupancy[next].MovementCost : 1);
                    if (cost <= budget && cost < distance[next]) { distance[next] = cost; queue.Enqueue(next); }
                }
            }
            return distance;
        }
        public static bool IsProvider(EntityManager em, Entity e)
        {
            if (!Sim.Operational(em, e)) return false;
            var b = em.GetComponentData<Building>(e); var s = em.GetComponentData<BuildingStats>(e);
            return s.IsProvider != 0 && b.Maintained != 0 && (s.JobCapacity == 0 || b.Workers > 0);
        }
        // Backtrack the same weighted flood used to choose the provider, without a visual-only route solver.
        public static System.Collections.Generic.List<int2> ProviderPath(EntityManager em,Entity root,Entity provider,NativeArray<float> distances)
        {
            var result=new System.Collections.Generic.List<int2>();if(provider==Entity.Null)return result;
            var grid=em.GetComponentData<GridData>(root);var occupancy=em.GetBuffer<Occupancy>(root);var b=em.GetComponentData<Building>(provider);int current=-1;float best=float.PositiveInfinity;
            for(int y=0;y<b.Size.y;y++)for(int x=0;x<b.Size.x;x++){int i=GridOps.Index(grid,b.Cell+new int2(x,y));if(i>=0&&distances[i]<best){best=distances[i];current=i;}}
            for(int step=0;current>=0&&step<distances.Length;step++)
            {
                var cell=grid.Value.Value.Min+new int2(current%grid.Value.Value.Size.x,current/grid.Value.Value.Size.x);result.Add(cell);if(distances[current]<=0)break;
                int previous=-1;float cost=occupancy[current].MovementCost>0?occupancy[current].MovementCost:1;
                for(int side=0;side<4;side++){var adjacent=cell+(side==0?new int2(1,0):side==1?new int2(-1,0):side==2?new int2(0,1):new int2(0,-1));int i=GridOps.Index(grid,adjacent);if(i>=0&&distances[i]<distances[current]&&math.abs(distances[i]+cost-distances[current])<.001f){previous=i;break;}}
                if(previous<0){result.Clear();break;}current=previous;
            }
            return result;
        }
    }
}
