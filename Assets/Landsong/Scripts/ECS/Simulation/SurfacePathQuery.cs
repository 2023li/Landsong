using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct SurfacePathQuery
    {
        [ReadOnly] public NativeArray<SurfaceNavNode> Nodes;
        [ReadOnly] public NativeArray<SurfaceNavEdge> Edges;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> Cells;
        public GridData Grid;
        public float Radius;
        public bool Open(int index) => index >= 0 && Nodes[index].Open != 0 && Nodes[index].SideClearance + .001f >= Radius;
        public float Height(int index, float2 xz) => Nodes[index].Position.y + math.dot(Nodes[index].Gradient, xz - Nodes[index].Position.xz);
        public int Locate(float3 position, float tolerance = .55f)
        {
            int result = -1; float best = tolerance;
            if (!Cells.TryGetFirstValue(GridOps.Cell(Grid, position), out var i, out var it)) return -1;
            do
            {
                if (!Open(i)) continue;
                float error = math.abs(Height(i, position.xz) - position.y);
                if (error <= best) { best = error; result = i; }
            } while (Cells.TryGetNextValue(out i, ref it));
            return result;
        }
        public bool Connected(int a, int b)
        {
            if (!Open(a) || !Open(b)) return false;
            if (a == b) return true;
            for (int edge = Nodes[a].FirstEdge; edge >= 0; edge = Edges[edge].Next) if (Edges[edge].Target == b) return true;
            return false;
        }
        public bool Shift(float3 from, float3 to, out float3 result)
        {
            result = from; int a = Locate(from); if (a < 0) return false;
            int b = Locate(to); if (!Connected(a, b) && !DiagonalConnected(a, b)) return false;
            // Keep radius inside the authored width, not just the centerline sample.
            var n = Nodes[b];
            if (n.Corridor != 0)
            {
                float2 lateral = n.Lateral;
                if (math.any(lateral != 0) && math.abs(math.dot(to.xz - n.Position.xz, lateral)) + Radius > n.SideClearance) return false;
            }
            to.y = Height(b, to.xz); result = to; return true;
        }
        bool DiagonalConnected(int a, int b)
        {
            if (!Open(a) || !Open(b) || Nodes[a].Surface != Nodes[b].Surface || Nodes[a].Elevation != Nodes[b].Elevation)
                return false;
            int2 delta = Nodes[b].Cell - Nodes[a].Cell;
            if (math.abs(delta.x) != 1 || math.abs(delta.y) != 1)
                return false;
            return Through(a, b, Nodes[a].Cell + new int2(delta.x, 0)) || Through(a, b, Nodes[a].Cell + new int2(0, delta.y));
        }
        bool Through(int a, int b, int2 cell)
        {
            if (!Cells.TryGetFirstValue(cell, out int middle, out var iterator))
                return false;
            do
            {
                if (Nodes[middle].Surface == Nodes[a].Surface && Nodes[middle].Elevation == Nodes[a].Elevation
                    && Connected(a, middle) && Connected(middle, b))
                    return true;
            }
            while (Cells.TryGetNextValue(out middle, ref iterator));
            return false;
        }
        struct HeapEntry { public int Index; public float Score; }
        public bool Find(float3 from, float3 destination, DynamicBuffer<Waypoint> result)
        {
            result.Clear(); int first = Locate(from), last = Locate(destination); if (first < 0) return false;
            if (last < 0)
            {
                // Blocked building targets resolve to the same-height accessible perimeter.
                float best = float.MaxValue; var cell = GridOps.Cell(Grid, destination);
                for (int r = 1; r <= 10 && last < 0; r++) for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++)
                {
                    if (math.max(math.abs(x), math.abs(y)) != r || !Cells.TryGetFirstValue(cell + new int2(x, y), out var j, out var it)) continue;
                    do
                    {
                        if (!Open(j) || math.abs(Nodes[j].Position.y - destination.y) > .55f) continue;
                        float score = math.distancesq(Nodes[j].Position, from);
                        if (score < best) { best = score; last = j; }
                    } while (Cells.TryGetNextValue(out j, ref it));
                }
            }
            if (last < 0) return false;
            if (last == first) { result.Add(new Waypoint { Position = Nodes[last].Position }); return true; }
            var costs = new NativeArray<float>(Nodes.Length, Allocator.Temp);
            var previous = new NativeArray<int>(Nodes.Length, Allocator.Temp);
            var closed = new NativeArray<byte>(Nodes.Length, Allocator.Temp);
            var heap = new NativeList<HeapEntry>(128, Allocator.Temp);
            for (int i = 0; i < Nodes.Length; i++) { costs[i] = float.MaxValue; previous[i] = -1; }
            costs[first] = 0; Push(ref heap, new HeapEntry { Index = first }); bool found = false;
            while (heap.Length > 0)
            {
                int current = Pop(ref heap).Index; if (closed[current] != 0) continue; closed[current] = 1;
                if (current == last) { found = true; break; }
                for (int edge = Nodes[current].FirstEdge; edge >= 0; edge = Edges[edge].Next)
                {
                    int at = Edges[edge].Target; if (!Open(at) || closed[at] != 0) continue;
                    float cost = costs[current] + math.distance(Nodes[current].Position, Nodes[at].Position) * Nodes[at].Cost;
                    if (cost >= costs[at]) continue;
                    costs[at] = cost; previous[at] = current;
                    Push(ref heap, new HeapEntry { Index = at, Score = cost + math.distance(Nodes[at].Position, Nodes[last].Position) });
                }
            }
            if (found)
            {
                for (int at = last; at != first && at >= 0; at = previous[at]) result.Add(new Waypoint { Position = Nodes[at].Position });
                for (int a = 0; a < result.Length / 2; a++) { var t = result[a]; result[a] = result[result.Length - a - 1]; result[result.Length - a - 1] = t; }
            }
            costs.Dispose(); previous.Dispose(); closed.Dispose(); heap.Dispose(); return found;
        }
        static void Push(ref NativeList<HeapEntry> heap, HeapEntry value)
        {
            heap.Add(value); int i = heap.Length - 1;
            while (i > 0) { int parent = (i - 1) / 2; if (heap[parent].Score <= value.Score) break; heap[i] = heap[parent]; i = parent; }
            heap[i] = value;
        }
        static HeapEntry Pop(ref NativeList<HeapEntry> heap)
        {
            var first = heap[0]; var value = heap[heap.Length - 1]; heap.RemoveAt(heap.Length - 1); if (heap.Length == 0) return first;
            int i = 0;
            while (i * 2 + 1 < heap.Length) { int child = i * 2 + 1; if (child + 1 < heap.Length && heap[child + 1].Score < heap[child].Score) child++; if (heap[child].Score >= value.Score) break; heap[i] = heap[child]; i = child; }
            heap[i] = value; return first;
        }
    }
}
