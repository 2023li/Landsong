using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    [Serializable] public sealed class NightEnemySource { public string Enemy; public float Weight = 1; }
    [Serializable] public sealed class NightEventSource
    {
        public string Id, FollowUp;
        public NightKind Kind;
        public int Priority, MinTurn = 1, MaxTurn, Interval, Cooldown, WaveCount = 3, ReturnDelay = 5;
        public float Weight = 1, BudgetScale = 1, Duration;
        public bool Once, ReturnOnly, Forced;
        public float[] WaveTimes = Array.Empty<float>(); // Empty: evenly spaced over 0..80%; otherwise exactly WaveCount entries.
        public NightEnemySource[] Enemies = Array.Empty<NightEnemySource>();
        public RuleSource[] Conditions = Array.Empty<RuleSource>();
        public static NightEventSource[] Defaults() => new[] {
            new NightEventSource { Id = "night.patrol", Kind = NightKind.Peaceful },
            new NightEventSource { Id = "night.raid", Kind = NightKind.Invasion, Enemies = new[] { new NightEnemySource { Enemy = "raider" } } },
            new NightEventSource { Id = "night.boss", Kind = NightKind.Boss, Priority = 10, FollowUp = "night.boss.return", Enemies = new[] { new NightEnemySource { Enemy = "raider" }, new NightEnemySource { Enemy = "boss" } } },
            new NightEventSource { Id = "night.boss.return", Kind = NightKind.Boss, Priority = 20, ReturnOnly = true, FollowUp = "night.boss.return", Enemies = new[] { new NightEnemySource { Enemy = "raider" }, new NightEnemySource { Enemy = "boss" } } }
        };
    }
    public static class NightContentValidation
    {
        public static void Validate(GameCatalogAsset catalog)
        {
            var n = catalog.Night;
            foreach (var v in new[] { n.EntryLeadSeconds, n.WarningSeconds, n.ProtectionSeconds, n.SpawnSafety, n.BorderBuffer, n.HeroWeight, n.FacilityWeight, n.TargetRadius, n.ThreatFloor, n.ThreatPerStrengthCap })
                if (!math.isfinite(v) || v < 0) throw new InvalidOperationException("Invalid night safety/strength settings.");
            if (n.HeroWeight > 1 || n.TargetRadius <= 0 || n.ThreatFloor < 1 || n.ThreatPerStrengthCap <= 0) throw new InvalidOperationException("Invalid hero weight/target radius/threat cap.");
            var ids = new HashSet<string>(StringComparer.Ordinal); bool peaceful = false;
            foreach (var e in catalog.NightEvents ?? Array.Empty<NightEventSource>())
            {
                if (e == null || string.IsNullOrWhiteSpace(e.Id) || System.Text.Encoding.UTF8.GetByteCount(e.Id) > 60 || !ids.Add(e.Id) || (byte)e.Kind > 2 || e.MinTurn < 1 || e.MaxTurn != 0 && e.MaxTurn < e.MinTurn || e.Interval < 0 || e.Cooldown < 0 || e.ReturnDelay < 1 || e.WaveCount < 1 || e.WaveCount > 32 || !math.isfinite(e.Weight) || e.Weight <= 0 || !math.isfinite(e.BudgetScale) || e.BudgetScale <= 0 || !math.isfinite(e.Duration) || e.Duration < 0 || e.Duration > 0 && e.Duration < 10)
                    throw new InvalidOperationException("Invalid/duplicate night event: " + e?.Id);
                bool boss = false, ordinary = false;
                if (e.WaveTimes == null || e.WaveTimes.Length != 0 && e.WaveTimes.Length != e.WaveCount) throw new InvalidOperationException("Wave times must match wave count: " + e.Id);
                for (int i = 0; i < e.WaveTimes.Length; i++) if (!math.isfinite(e.WaveTimes[i]) || e.WaveTimes[i] < 0 || e.WaveTimes[i] >= 1 || i == 0 && e.WaveTimes[i] != 0 || i > 0 && e.WaveTimes[i] <= e.WaveTimes[i - 1]) throw new InvalidOperationException("Wave times must start at zero and increase below one: " + e.Id);
                foreach (var p in e.Enemies ?? Array.Empty<NightEnemySource>())
                {
                    var d = p == null ? -1 : catalog.Find(p.Enemy);
                    if (d < 0 || catalog.Content[d].Kind != ContentKind.Enemy || !math.isfinite(p.Weight) || p.Weight <= 0 || catalog.Content[d].Value <= 0) throw new InvalidOperationException("Invalid night enemy pool: " + e.Id);
                    if ((catalog.Content[d].Flags & 1) != 0) boss = true; else ordinary = true;
                }
                if (e.Kind == NightKind.Peaceful && (boss || ordinary) || e.Kind == NightKind.Invasion && (!ordinary || boss) || e.Kind == NightKind.Boss && !boss) throw new InvalidOperationException("Night kind and enemy pool disagree: " + e.Id);
                if (e.Kind == NightKind.Peaceful && !e.Once && !e.ReturnOnly && e.MinTurn == 1 && e.MaxTurn == 0 && e.Cooldown == 0 && e.Interval == 0 && e.Conditions.Length == 0) peaceful = true;
                foreach (var r in e.Conditions)
                {
                    if (r.Kind != RuleKind.RequireTurn && r.Kind != RuleKind.RequireBuilding && r.Kind != RuleKind.RequireTechnology && r.Kind != RuleKind.RequireItem && r.Kind != RuleKind.Prerequisite) throw new InvalidOperationException("Unsupported night condition: " + r.Kind);
                    var rule = GameWorldAuthoring.ConvertRule(catalog, r);
                    if (r.Amount < 0 || r.Kind != RuleKind.RequireTurn && rule.Target < 0) throw new InvalidOperationException("Invalid night prerequisite: " + e.Id);
                }
            }
            if (!peaceful) throw new InvalidOperationException("Night catalog needs an unconditional repeatable peaceful fallback.");
            foreach (var e in catalog.NightEvents) if (!string.IsNullOrEmpty(e.FollowUp))
            { var next = Array.Find(catalog.NightEvents, v => v.Id == e.FollowUp); if (next == null || next.Kind != NightKind.Boss || !next.ReturnOnly || e.Kind != NightKind.Boss) throw new InvalidOperationException("Boss aftermath must reference a return-only boss event."); }
        }
        public static void Bake(GameCatalogAsset c, BlobBuilder builder, ref ContentBlob blob)
        {
            blob.Night = c.Night;
            int pc = 0, rc = 0; foreach (var e in c.NightEvents) { pc += e.Enemies.Length; rc += e.Conditions.Length; }
            var events = builder.Allocate(ref blob.NightEvents, c.NightEvents.Length); var pools = builder.Allocate(ref blob.NightEnemies, pc); var rules = builder.Allocate(ref blob.NightConditions, rc); pc = rc = 0;
            for (int i = 0; i < events.Length; i++)
            {
                var e = c.NightEvents[i]; events[i] = new NightEventDefinition { Id = new FixedString64Bytes(e.Id), FollowUp = new FixedString64Bytes(e.FollowUp ?? ""), Kind = e.Kind, Priority = e.Priority, MinTurn = e.MinTurn, MaxTurn = e.MaxTurn, Interval = e.Interval, Cooldown = e.Cooldown, WaveCount = e.WaveCount, ReturnDelay = e.ReturnDelay, Weight = e.Weight, BudgetScale = e.BudgetScale, Duration = e.Duration, Once = (byte)(e.Once ? 1 : 0), ReturnOnly = (byte)(e.ReturnOnly ? 1 : 0), Forced = (byte)(e.Forced ? 1 : 0), PoolStart = pc, PoolCount = e.Enemies.Length, ConditionStart = rc, ConditionCount = e.Conditions.Length };
                foreach (var p in e.Enemies) pools[pc++] = new NightEnemyChoice { Definition = c.Find(p.Enemy), Weight = p.Weight };
                var entry = events[i]; foreach (var time in e.WaveTimes) entry.WaveTimes.Add(time); events[i] = entry;
                foreach (var r in e.Conditions) rules[rc++] = GameWorldAuthoring.ConvertRule(c, r);
            }
        }
    }
}
