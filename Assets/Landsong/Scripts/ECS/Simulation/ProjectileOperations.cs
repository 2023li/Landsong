using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct ProjectileBlock { public byte Environment; public ulong Owner; }
    public struct ImpactTarget { public Entity Entity; public ulong Id; public float3 Position; public float2 HalfSize; public byte Faction; }
    public static class ProjectileOps
    {
        public static float Mitigate(float amount, float armor, float penetration, float reduction)
            => math.max(0, amount - math.max(0, armor - math.max(0, penetration))) * (1 - math.clamp(reduction, 0, .95f));
        public static NativeArray<ProjectileBlock> Blocks(EntityManager em, Entity root, Allocator allocator)
        {
            var grid = em.GetComponentData<GridData>(root); var blocks = new NativeArray<ProjectileBlock>(grid.Value.Value.Cells.Length, allocator);
            for (int i = 0; i < blocks.Length; i++) blocks[i] = new ProjectileBlock { Environment = grid.Value.Value.Cells[i].BlocksProjectile };
            using var sites = Sim.Entities<Building>(em);
            foreach (var e in sites)
            {
                if (!Sim.Operational(em, e)) continue; var id = em.GetComponentData<Identity>(e); if (!Sim.Definition(em, root, id.Definition).Combat.BlocksProjectile) continue;
                var b = em.GetComponentData<Building>(e);
                for (int y = 0; y < b.Size.y; y++) for (int x = 0; x < b.Size.x; x++) { int at = GridOps.Index(grid, b.Cell + new int2(x, y)); if (at < 0) continue; var cell = blocks[at]; cell.Owner = id.Id; blocks[at] = cell; }
            }
            return blocks;
        }
        // Swept supercover grid traversal: never sample past thin blockers at high projectile speed.
        public static bool Blocked(GridData grid, NativeArray<ProjectileBlock> blocks, float3 from, float3 to, ulong source, ulong target)
        {
            var a = (from.xz - grid.Origin.xz) / grid.CellSize; var b = (to.xz - grid.Origin.xz) / grid.CellSize; var delta = b - a; var cell = (int2)math.floor(a); var last = (int2)math.floor(b);
            var step = new int2(delta.x > 0 ? 1 : -1, delta.y > 0 ? 1 : -1);
            var stride = new float2(math.abs(delta.x) < .00001f ? float.MaxValue : math.abs(1 / delta.x), math.abs(delta.y) < .00001f ? float.MaxValue : math.abs(1 / delta.y));
            var next = new float2(math.abs(delta.x) < .00001f ? float.MaxValue : ((cell.x + (step.x > 0 ? 1 : 0)) - a.x) / delta.x, math.abs(delta.y) < .00001f ? float.MaxValue : ((cell.y + (step.y > 0 ? 1 : 0)) - a.y) / delta.y);
            for (int guard = 0; guard <= grid.Value.Value.Size.x + grid.Value.Value.Size.y + 2; guard++)
            {
                if (CellBlocked(grid, blocks, cell, source, target)) return true;
                if (math.all(cell == last)) return false;
                if (math.abs(next.x - next.y) < .00001f) { if (CellBlocked(grid, blocks, cell + new int2(step.x, 0), source, target) || CellBlocked(grid, blocks, cell + new int2(0, step.y), source, target)) return true; cell += step; next += stride; }
                else if (next.x < next.y) { cell.x += step.x; next.x += stride.x; } else { cell.y += step.y; next.y += stride.y; }
            }
            return true;
        }
        static bool CellBlocked(GridData grid, NativeArray<ProjectileBlock> blocks, int2 cell, ulong source, ulong target)
        { int at = GridOps.Index(grid, cell); if (at < 0 || grid.Value.Value.Cells[at].Exists == 0) return true; var b = blocks[at]; return b.Environment != 0 || b.Owner != 0 && b.Owner != source && b.Owner != target; }
        public static float3 Closest(ImpactTarget target, float3 from)
        { var point = target.Position; point.xz = math.clamp(from.xz, point.xz - target.HalfSize, point.xz + target.HalfSize); return point; }
    }
}
