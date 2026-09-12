using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class IntelSource
    {
        public ulong Building;
        public int Definition, Points, Effective;
        public string Name, Reason;
    }
    public struct IntelArea
    {
        public float3 Center, Size;
        public bool Target, Secondary;
        public int Direction;
        public bool Contains(float3 point) => math.all(math.abs(point.xz - Center.xz) <= Size.xz * .5f + .01f);
    }
    // Sanitized view data: no seeds, unit attributes, exact time, coordinates or retry counters.
    public sealed class IntelView
    {
        public int Current, Known, Tier, NextPoints;
        public bool Unread, Day;
        public readonly List<string> Lines = new List<string>();
        public readonly List<IntelSource> Sources = new List<IntelSource>();
        public readonly List<IntelArea> Areas = new List<IntelArea>();
        public int WaveChoices, SelectedWave, CompletedGroups;
    }
    public static class IntelOps
    {
        public static int Tier(GameSettings settings, int value) => value >= settings.HighIntel ? 3 : value >= settings.MediumIntel ? 2 : value >= settings.LowIntel ? 1 : 0;
        public static List<IntelSource> Sources(EntityManager em, Entity root)
        {
            var result = new List<IntelSource>();
            var quote = EffectOps.Query(em, root, new EffectQuery(RuleKind.Intelligence, domain: EffectDomain.Intelligence));
            foreach (var source in quote.Sources)
                result.Add(new IntelSource { Building = source.Owner, Definition = source.Definition,
                    Name = source.Name, Points = (int)source.Value, Effective = (int)source.Applied, Reason = source.Reason });
            return result;
        }
        public static int Current(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            return s.Phase != Phase.Day && s.Phase != Phase.Settlement ? math.clamp(s.IntelAtNight, 0, 100) : (int)Math.Min(100L, Sources(em, root).Sum(x => (long)x.Effective));
        }
        public static int Known(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            var r = em.HasComponent<RecoveryState>(root) ? em.GetComponentData<RecoveryState>(root) : default;
            return math.max(Current(em, root), r.Turn == s.Turn ? r.KnownIntel : 0);
        }
        public static void Refresh(EntityManager em, Entity root)
        {
            Sim.Buffer<IntelGeometry>(em, root);
            var geometry = em.GetBuffer<IntelGeometry>(root); var revision = em.GetComponentData<GridData>(root).Revision;
            if (geometry.Length > 0 && geometry[0].Revision != revision) geometry.Clear();
            foreach (var wave in em.GetBuffer<NightWave>(root))
                for (int tier = 2; tier <= 3; tier++)
                {
                    if (wave.Region < 0 || wave.Region >= em.GetBuffer<SpawnRegion>(root).Length || wave.Spawned != 0) continue;
                    bool exists = false;
                    foreach (var entry in geometry) if (Matches(entry, wave, tier, revision)) { exists = true; break; }
                    if (!exists) geometry.Add(new IntelGeometry { Region = wave.Region, Count = wave.Count, Position = wave.Position, Revision = revision, Tier = tier, Area = ComputeSpawnArea(em, root, wave, tier) });
                }
            // The cache is disposable, never knowledge or a second authority.
            if (geometry.Length > 2048) geometry.Clear();
            var s = em.GetComponentData<Session>(root); var r = em.HasComponent<RecoveryState>(root) ? em.GetComponentData<RecoveryState>(root) : new RecoveryState { Turn = s.Turn };
            if (r.Turn != s.Turn) r = new RecoveryState { Turn = s.Turn };
            r.KnownIntel = math.max(r.KnownIntel, Current(em, root)); Sim.Set(em, root, r);
            var view = Read(em, root);
            // Only publicly visible knowledge affects unread. Hidden wave/target changes cannot signal a leak.
            ulong fingerprint = 14695981039346656037ul;
            void Hash(string value) { foreach (char c in value) { fingerprint ^= c; fingerprint *= 1099511628211ul; } }
            Hash(s.Turn.ToString()); Hash(view.Known.ToString());
            foreach (var line in view.Lines) Hash(line);
            foreach (var line in CourtLines(em, root, view.Known)) Hash(line);
            foreach (var source in view.Sources) { Hash(source.Name); Hash(source.Effective.ToString()); Hash(source.Reason); }
            foreach (var area in view.Areas) { Hash(area.Center.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Hash(area.Center.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Hash(area.Size.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Hash(area.Size.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); }
            // Reopening a night gives a normal new report, even if the random public composition happens to match.
            Hash(s.NightSeed.ToString());
            r.IntelFingerprint = fingerprint; Sim.Set(em, root, r);
            if (s.Phase == Phase.GameOver || s.Phase == Phase.Ended) { s.IntelligenceMode = 0; em.SetComponentData(root, s); }
        }
        public static void MarkRead(EntityManager em, Entity root, ulong fingerprint)
        {
            if (!em.HasComponent<RecoveryState>(root)) return;
            var r = em.GetComponentData<RecoveryState>(root); if (r.IntelFingerprint != fingerprint) return;
            r.IntelReadFingerprint = fingerprint; em.SetComponentData(root, r);
        }
        public static string Threat(int baseThreat) => baseThreat < 35 ? "较低" : baseThreat < 90 ? "中等" : "较高";
        public static int2 CountRange(int count) { int width = math.max(5, (int)math.ceil(count * .3f)); return new int2(math.max(1, count - width), count + width); }
        public static IntelArea SpawnArea(EntityManager em, Entity root, NightWave wave, int tier)
        {
            if (em.HasBuffer<IntelGeometry>(root))
                foreach (var entry in em.GetBuffer<IntelGeometry>(root))
                    if (Matches(entry, wave, tier, em.GetComponentData<GridData>(root).Revision)) return entry.Area;
            return ComputeSpawnArea(em, root, wave, tier);
        }
        static bool Matches(IntelGeometry entry, NightWave wave, int tier, int revision) => entry.Region == wave.Region && entry.Count == wave.Count && math.all(entry.Position == wave.Position) && entry.Tier == tier && entry.Revision == revision;
        static IntelArea ComputeSpawnArea(EntityManager em, Entity root, NightWave wave, int tier)
        {
            var region = em.GetBuffer<SpawnRegion>(root)[wave.Region]; var grid = em.GetComponentData<GridData>(root);
            var boundsMin = wave.Position.xz; var boundsMax = boundsMin;
            for (int n = 0; n < wave.Count; n++)
            {
                var preferred = wave.Position + new float3((n % 5 - 2) * .6f, 0, n / 5 * .6f);
                if (NightSpatialOps.SpawnPoint(em, root, wave.Region, preferred, out var point)) { boundsMin = math.min(boundsMin, point.xz); boundsMax = math.max(boundsMax, point.xz); }
            }
            var low = region.Center.xz - region.Size.xz * .5f; var high = low + region.Size.xz;
            // Quantized subregions with padding, never a point or exact building footprint.
            float quantum = grid.CellSize * (tier >= 3 ? 4 : 12);
            var min = math.max(low, low + math.floor((boundsMin - low) / quantum) * quantum - grid.CellSize);
            var max = math.min(high, low + math.ceil((boundsMax - low) / quantum) * quantum + grid.CellSize);
            var center = (min + max) * .5f; return new IntelArea { Center = new float3(center.x, region.Center.y, center.y), Size = new float3(max.x - min.x, .1f, max.y - min.y), Direction = wave.Direction };
        }
        static IntelArea TargetArea(EntityManager em, Entity root, Entity target, int tier, bool harassment)
        {
            var grid = em.GetComponentData<GridData>(root); var b = em.GetComponentData<Building>(target); var position = Sim.Position(em, target);
            float quantum = math.max(grid.CellSize * (tier >= 3 ? 6 : 16), harassment ? NightPlanOps.Rules(em, root).TargetRadius : 0);
            var lo = position.xz - (float2)b.Size * grid.CellSize * .5f; var hi = position.xz + (float2)b.Size * grid.CellSize * .5f;
            lo = math.floor(lo / quantum) * quantum - grid.CellSize; hi = math.ceil(hi / quantum) * quantum + grid.CellSize;
            if (harassment) { lo -= NightPlanOps.Rules(em, root).TargetRadius; hi += NightPlanOps.Rules(em, root).TargetRadius; }
            var center = (lo + hi) * .5f;
            return new IntelArea { Center = new float3(center.x, position.y, center.y), Size = new float3(hi.x - lo.x, .1f, hi.y - lo.y), Target = true };
        }
        public static IntelView Read(EntityManager em, Entity root, int selectedWave = 0)
        {
            var s = em.GetComponentData<Session>(root); var cfg = em.GetComponentData<GameSettings>(root); var plan = NightPlanOps.State(em, root);
            var view = new IntelView { Current = Current(em, root), Known = Known(em, root), Day = s.Phase == Phase.Day || s.Phase == Phase.Settlement };
            view.Tier = Tier(cfg, view.Known); view.NextPoints = view.Tier == 3 ? 0 : (view.Tier == 0 ? cfg.LowIntel : view.Tier == 1 ? cfg.MediumIntel : cfg.HighIntel) - view.Current;
            if (em.HasComponent<RecoveryState>(root)) { var r = em.GetComponentData<RecoveryState>(root); view.Unread = r.IntelFingerprint != r.IntelReadFingerprint; }
            view.Sources.AddRange(Sources(em, root));
            if (view.Tier == 0) { view.Lines.Add("暂无可用军情"); return view; }
            view.Lines.Add(s.NightKind == NightKind.Peaceful ? "当晚平安" : s.NightKind == NightKind.Boss ? "当晚存在大型威胁" : "当晚发现入侵迹象");
            if (s.NightKind == NightKind.Peaceful) return view;
            view.Lines.Add("回合初威胁：" + Threat(plan.BaseThreat));
            if (view.Tier == 1) return view;
            var waves = em.GetBuffer<NightWave>(root).ToNativeArray(Unity.Collections.Allocator.Temp);
            try
            {
                view.CompletedGroups = waves.ToArray().Where(w => w.Spawned != 0).Select(w => w.At).Distinct().Count();
                var groups = waves.ToArray().Where(w => w.Spawned == 0 && w.SpatiallyBlocked == 0).Select(w => w.At).Distinct().OrderBy(t => t).ToArray();
                var lead = view.Tier == 3 ? cfg.HighIntelLead : cfg.MediumIntelLead;
                var visible = groups.Where(t => t * s.NightDuration - plan.CombatElapsed <= lead).ToArray();
                view.WaveChoices = view.Day ? 0 : visible.Length; view.SelectedWave = math.clamp(selectedWave, 0, math.max(0, visible.Length - 1));
                var focus = visible.Length == 0 ? -1 : visible[view.SelectedWave];
                if (view.Day)
                {
                    foreach (var group in waves.ToArray().GroupBy(w => w.Definition).OrderBy(g => g.Key))
                    {
                        var name = Sim.Definition(em, root, group.Key).Name.ToString(); int count = group.Sum(w => w.Count); var range = CountRange(count);
                        view.Lines.Add(view.Tier == 3 ? name + " × " + count : name + " · 约 " + range.x + "～" + range.y);
                        if (view.Tier == 3 && (Sim.Definition(em, root, group.Key).Flags & 1) != 0)
                            view.Lines.Add("大型威胁方位：" + string.Join("、", group.Select(w => Direction(w.Direction)).Distinct()));
                    }
                }
                else view.Lines.Add(focus < 0 ? "暂无新的来袭方向" : "来袭方向：" + string.Join("、", waves.ToArray().Where(w => w.Spawned == 0 && w.SpatiallyBlocked == 0 && w.At == focus).Select(w => Direction(w.Direction)).Distinct()));
                foreach (var w in waves)
                {
                    if (w.SpatiallyBlocked != 0 || w.Spawned != 0) continue;
                    var area = SpawnArea(em, root, w, view.Tier); area.Secondary = !view.Day && w.At != focus; view.Areas.Add(area);
                    if (!view.Day && w.At != focus) continue;
                    var target = Sim.Find(em, w.Target); if (!NightSpatialOps.ValidTarget(em, target)) continue;
                    bool harassment = ((Sim.Definition(em, root, w.Definition).Flags >> 1) & 3) == 2;
                    view.Areas.Add(TargetArea(em, root, target, view.Tier, harassment));
                }
                if (!view.Day)
                {
                    using var actors = Sim.OrderedEntities<Combatant>(em); var targets = new HashSet<ulong>();
                    foreach (var e in actors)
                    {
                        var a = em.GetComponentData<Combatant>(e); if (a.Faction != 1 || a.Deployed == 0 || !Sim.Alive(em, e) || a.HomeId == 0 || !targets.Add(a.HomeId)) continue;
                        var target = Sim.Find(em, a.HomeId); if (!NightSpatialOps.ValidTarget(em, target)) continue;
                        view.Areas.Add(TargetArea(em, root, target, view.Tier, ((Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition).Flags >> 1) & 3) == 2));
                    }
                }
            }
            finally { waves.Dispose(); }
            return view;
        }
        public static List<string> CourtLines(EntityManager em, Entity root, int known)
        {
            var lines = new List<string> { "宫廷情报（与军情共用完善度）" }; int tier = Tier(em.GetComponentData<GameSettings>(root), known);
            if (tier == 0) { lines.Add("暂无宫廷情报"); return lines; }
            var king = CourtOps.Monarch(em); bool threat = false;
            using var royals = Sim.OrderedEntities<Royal>(em);
            foreach (var e in royals)
            {
                var p = em.GetComponentData<Royal>(e); if (!CourtOps.Eligible(em, king, e) || CourtOps.PlotChance(em, root, king, e) <= 0 && p.Evidence == 0) continue;
                threat = true;
                if (tier >= 2) lines.Add(em.GetComponentData<Identity>(e).Name + "：" + (tier == 3 ? (p.Evidence != 0 ? "已掌握弑君阴谋确凿证据" : "势力接近君王，有夺权动机，暂无确凿证据") : "其势力值得关注"));
            }
            lines.Add(threat ? "宫廷存在动荡迹象" : "目前未发现明显夺权迹象"); return lines;
        }
        public static string Direction(int value) => value == 10 ? "北方" : value == 20 ? "东方" : value == 30 ? "南方" : value == 40 ? "西方" : "未知方向";
    }
}
