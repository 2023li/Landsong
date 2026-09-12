#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    /// <summary>Opt-in main-thread microbenchmark of the production query helpers.</summary>
    public static class SimulationQueryBenchmark
    {
        const uint Seed = 0x51A7C0DE;
        const int WarmupBatches = 8, Samples = 25, FindBatch = 8, OrderBatch = 2;
        const ulong HashStart = 14695981039346656037UL;
        const string Source = "Assets/Landsong/Scripts/ECS/Simulation/SimulationAccess.cs";
        static readonly int[] Sizes = { 100, 1000, 10000 };
        static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        static ulong observedChecksum;

        [Serializable]
        sealed class RunReport
        {
            public string timestampUtc, unityVersion, os, processor, clrVersion, entitiesAssemblyVersion;
            public string sourceSha256, coreAssemblySha256, packageLockSha256, seedHex, methodology;
            public int processorCount, memoryMegabytes, warmupBatches, measuredBatches, findCallsPerBatch, orderedCallsPerBatch;
            public long stopwatchFrequency;
            public bool collectionChecks;
            public List<Result> results = new List<Result>();
        }

        [Serializable]
        sealed class Result
        {
            public int entityCount, callsPerBatch, gc0, gc1, gc2;
            public string operation, distribution, validatedChecksum;
            public long minimumNativePayloadBytesPerCall;
            public double meanMicroseconds, medianMicroseconds, p95Microseconds, minMicroseconds, maxMicroseconds;
            public double meanManagedBytesPerCall, minManagedBytesPerCall, maxManagedBytesPerCall;
            public List<Sample> samples = new List<Sample>();
        }

        [Serializable]
        sealed class Sample
        {
            public int index;
            public long elapsedTicks, managedBytes;
            public double microsecondsPerCall, managedBytesPerCall;
        }

        [MenuItem("Landsong/ECS/Performance/Sample simulation queries")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请退出 Play 并等待编译完成后执行查询采样。");
            var report = new RunReport
            {
                timestampUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                os = SystemInfo.operatingSystem, processor = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount, memoryMegabytes = SystemInfo.systemMemorySize,
                clrVersion = Environment.Version.ToString(), entitiesAssemblyVersion = typeof(World).Assembly.GetName().Version.ToString(),
                sourceSha256 = Sha256(Source), coreAssemblySha256 = Sha256(typeof(Sim).Assembly.Location),
                packageLockSha256 = Sha256("Packages/packages-lock.json"), seedHex = Seed.ToString("X8"),
                warmupBatches = WarmupBatches, measuredBatches = Samples, findCallsPerBatch = FindBatch,
                orderedCallsPerBatch = OrderBatch, stopwatchFrequency = Stopwatch.Frequency,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                collectionChecks = true,
#else
                collectionChecks = false,
#endif
                methodology = "Isolated World; no systems, scene, catalog, save store or default World. Identity+Building archetype; unique IDs assigned by fixed xorshift32/Fisher-Yates permutation. Production Sim.Find and Sim.OrderedEntities<Building> execute on the Editor main thread. Warmup precedes each case; one explicit GC outside measurement; no per-sample GC. Timings include batch loop/checksum and query/native-array disposal. Result validation, fixture creation and report I/O are excluded. Managed allocation is GC.GetAllocatedBytesForCurrentThread delta; native payload is a source-derived lower bound, not a native-allocation measurement. No performance pass/fail threshold or FPS inference."
            };
            foreach (int count in Sizes) SampleSize(report, count);
            string directory = "Library/LandsongEcs/Performance";
            Directory.CreateDirectory(directory);
            string stem = Path.Combine(directory, "simulation-queries-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            string markdown = Markdown(report);
            File.WriteAllText(stem + ".json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            File.WriteAllText(stem + ".csv", Csv(report), new UTF8Encoding(false));
            File.WriteAllText(stem + ".md", markdown, new UTF8Encoding(false));
            File.WriteAllText("Library/LandsongEcs/performance-query-benchmark.md", markdown, new UTF8Encoding(false));
            return "查询采样完成（正确性校验通过；无性能阈值判断）：" + stem + ".md / .csv / .json";
        }

        static void SampleSize(RunReport report, int count)
        {
            using var world = new World("Owned query benchmark " + count);
            var em = world.EntityManager;
            var archetype = em.CreateArchetype(typeof(Identity), typeof(Building));
            using var created = new NativeArray<Entity>(count, Allocator.Persistent);
            em.CreateEntity(archetype, created);
            var ranks = new int[count];
            for (int i = 0; i < count; i++) ranks[i] = i;
            uint random = Seed;
            for (int i = count - 1; i > 0; i--)
            {
                int swap = (int)(Next(ref random) % (uint)(i + 1));
                int rank = ranks[i]; ranks[i] = ranks[swap]; ranks[swap] = rank;
            }
            var sorted = new Entity[count];
            for (int i = 0; i < count; i++)
            {
                sorted[ranks[i]] = created[i];
                em.SetComponentData(created[i], new Identity { Id = Id(ranks[i]), Definition = -1 });
            }
            Entity first, middle, last;
            Entity[] uniform = new Entity[FindBatch];
            // Measure early/late positions in the actual query enumeration, not an assumed creation order.
            using (var actual = Sim.Entities<Identity>(em))
            {
                if (actual.Length != count) throw new InvalidOperationException("性能夹具实体数量不符。");
                first = actual[0]; middle = actual[count / 2]; last = actual[count - 1];
                random = Seed ^ 0x9E3779B9;
                for (int i = 0; i < uniform.Length; i++) uniform[i] = actual[(int)(Next(ref random) % (uint)count)];
            }
            ValidateOrdered(em, sorted);
            var firstSequence = Repeat(first);
            AddFind(report, em, count, "Find.zero", "ID=0 fast-path; not representative of a world scan", Repeat(Entity.Null), new ulong[FindBatch], 0);
            AddFind(report, em, count, "Find.first", "First entity in actual Identity query order", firstSequence, Ids(em, firstSequence));
            AddFind(report, em, count, "Find.middle", "Entity at query position N/2", Repeat(middle), Ids(em, Repeat(middle)));
            AddFind(report, em, count, "Find.last", "Last entity in actual Identity query order", Repeat(last), Ids(em, Repeat(last)));
            var missing = new ulong[FindBatch]; for (int i = 0; i < missing.Length; i++) missing[i] = ulong.MaxValue;
            AddFind(report, em, count, "Find.missing", "Absent nonzero ID; complete scan", Repeat(Entity.Null), missing);
            AddFind(report, em, count, "Find.uniform", "Eight seeded uniform query positions, repeated identically per batch", uniform, Ids(em, uniform));
            ulong expectedFind = EntitySequenceHash(firstSequence);
            report.results.Add(Measure(count, "Harness.find-checksum", "Loop/checksum reference; reported separately, never subtracted", FindBatch,
                () => EntitySequenceHash(firstSequence), expectedFind, 0));
            ulong expectedOrdered = HashStart;
            for (int i = 0; i < OrderBatch; i++) expectedOrdered = OrderHash(expectedOrdered, count, sorted[0], sorted[count - 1]);
            report.results.Add(Measure(count, "OrderedEntities.Building", "Shuffled unique IDs; full ascending identity sort", OrderBatch, () =>
            {
                ulong checksum = HashStart;
                for (int i = 0; i < OrderBatch; i++)
                {
                    using var ordered = Sim.OrderedEntities<Building>(em);
                    checksum = OrderHash(checksum, ordered.Length, ordered[0], ordered[ordered.Length - 1]);
                }
                return checksum;
            }, expectedOrdered, count * (long)Marshal.SizeOf<Entity>()));
            ValidateOrdered(em, sorted);
        }

        static void AddFind(RunReport report, EntityManager em, int count, string name, string distribution, Entity[] expected, ulong[] ids, long payload = -1)
        {
            for (int i = 0; i < ids.Length; i++)
                if (Sim.Find(em, ids[i]) != expected[i]) throw new InvalidOperationException("Find 夹具结果不符：" + name);
            report.results.Add(Measure(count, name, distribution, FindBatch, () =>
            {
                ulong checksum = HashStart;
                for (int i = 0; i < ids.Length; i++) checksum = EntityHash(checksum, Sim.Find(em, ids[i]));
                return checksum;
            }, EntitySequenceHash(expected), payload < 0 ? count * (long)Marshal.SizeOf<Entity>() : payload));
        }

        static Result Measure(int count, string operation, string distribution, int calls, Func<ulong> invoke, ulong expected, long nativePayload)
        {
            for (int i = 0; i < WarmupBatches; i++)
                if (invoke() != expected) throw new InvalidOperationException("预热结果校验失败：" + operation);
            // Warm the counters before the measured interval as well.
            _ = GC.GetAllocatedBytesForCurrentThread(); _ = Stopwatch.GetTimestamp();
            var elapsed = new long[Samples]; var allocated = new long[Samples];
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            for (int i = 0; i < Samples; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                ulong checksum = invoke();
                elapsed[i] = Stopwatch.GetTimestamp() - start;
                allocated[i] = GC.GetAllocatedBytesForCurrentThread() - before;
                // Consume every result after stopping the timer; checks are not timed.
                if (checksum != expected || allocated[i] < 0) throw new InvalidOperationException("采样结果或分配计数校验失败：" + operation);
                observedChecksum ^= checksum;
            }
            var result = new Result
            {
                entityCount = count, operation = operation, distribution = distribution, callsPerBatch = calls,
                gc0 = GC.CollectionCount(0) - gc0, gc1 = GC.CollectionCount(1) - gc1, gc2 = GC.CollectionCount(2) - gc2,
                validatedChecksum = expected.ToString("X16"), minimumNativePayloadBytesPerCall = nativePayload
            };
            var times = new double[Samples]; var bytes = new double[Samples];
            for (int i = 0; i < Samples; i++)
            {
                times[i] = elapsed[i] * 1000000d / Stopwatch.Frequency / calls;
                bytes[i] = allocated[i] / (double)calls;
                result.samples.Add(new Sample { index = i, elapsedTicks = elapsed[i], managedBytes = allocated[i], microsecondsPerCall = times[i], managedBytesPerCall = bytes[i] });
                result.meanMicroseconds += times[i] / Samples; result.meanManagedBytesPerCall += bytes[i] / Samples;
            }
            Array.Sort(times); Array.Sort(bytes);
            result.minMicroseconds = times[0]; result.maxMicroseconds = times[Samples - 1];
            result.medianMicroseconds = times[Samples / 2]; result.p95Microseconds = times[(int)Math.Ceiling(Samples * .95) - 1];
            result.minManagedBytesPerCall = bytes[0]; result.maxManagedBytesPerCall = bytes[Samples - 1];
            return result;
        }

        static void ValidateOrdered(EntityManager em, Entity[] expected)
        {
            using var ordered = Sim.OrderedEntities<Building>(em);
            if (ordered.Length != expected.Length) throw new InvalidOperationException("排序数量不符。");
            for (int i = 0; i < ordered.Length; i++)
                if (ordered[i] != expected[i] || em.GetComponentData<Identity>(ordered[i]).Id != Id(i))
                    throw new InvalidOperationException("排序结果、标识或原实体发生变化。");
        }
        static Entity[] Repeat(Entity entity) { var values = new Entity[FindBatch]; for (int i = 0; i < values.Length; i++) values[i] = entity; return values; }
        static ulong[] Ids(EntityManager em, Entity[] values) { var ids = new ulong[values.Length]; for (int i = 0; i < values.Length; i++) ids[i] = em.GetComponentData<Identity>(values[i]).Id; return ids; }
        static ulong Id(int rank) => 100001UL + (ulong)rank * 17UL;
        static uint Next(ref uint value) { value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value; }
        static ulong Mix(ulong hash, ulong value) { unchecked { return (hash ^ value) * 1099511628211UL; } }
        static ulong EntityHash(ulong hash, Entity entity) => Mix(Mix(hash, (uint)entity.Index), (uint)entity.Version);
        static ulong EntitySequenceHash(Entity[] values) { ulong hash = HashStart; for (int i = 0; i < values.Length; i++) hash = EntityHash(hash, values[i]); return hash; }
        static ulong OrderHash(ulong hash, int count, Entity first, Entity last) => EntityHash(EntityHash(Mix(hash, (ulong)count), first), last);
        static string Sha256(string path) { using var sha = SHA256.Create(); using var input = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant(); }
        static string Number(double value) => value.ToString("F3", Culture);
        static string Markdown(RunReport report)
        {
            var text = new StringBuilder("# Sim 查询性能采样\n\n");
            text.Append("采样时间（UTC）：").Append(report.timestampUtc).Append("。正确性校验通过；未设置性能通过阈值。\n\n");
            text.Append("Unity ").Append(report.unityVersion).Append("；").Append(report.os).Append("；").Append(report.processor).Append("；逻辑处理器 ").Append(report.processorCount)
                .Append("；内存 ").Append(report.memoryMegabytes).Append(" MB；Collections 检查 ").Append(report.collectionChecks).Append("。\n\n");
            text.Append("每个操作预热 ").Append(WarmupBatches).Append(" 批，采样 ").Append(Samples).Append(" 批；Find 每批 ").Append(FindBatch).Append(" 次，排序每批 ").Append(OrderBatch).Append(" 次；固定种子 0x").Append(report.seedHex).Append("。p95 是每批平均单次耗时的 nearest-rank 分位数，不是单次尾延迟。\n\n");
            text.Append("| 实体数 | 操作 | p50 μs/次 | p95 μs/次 | 均值 μs/次 | managed B/次均值 | native实体数组最低载荷 B/次 | GC 0/1/2 |\n|---:|---|---:|---:|---:|---:|---:|---|\n");
            foreach (var r in report.results)
                text.Append("| ").Append(r.entityCount).Append(" | ").Append(r.operation).Append(" | ").Append(Number(r.medianMicroseconds)).Append(" | ").Append(Number(r.p95Microseconds)).Append(" | ").Append(Number(r.meanMicroseconds)).Append(" | ").Append(Number(r.meanManagedBytesPerCall)).Append(" | ").Append(r.minimumNativePayloadBytesPerCall).Append(" | ").Append(r.gc0).Append('/').Append(r.gc1).Append('/').Append(r.gc2).Append(" |\n");
            text.Append("\n夹具仅含同一 archetype 的 Identity + Building，ID 经固定洗牌后写入。first/middle/last 按实际查询顺序取目标；missing 是不存在的非零标识；uniform 为固定种子选出的八个命中位置。每个规模都逐项验证完整排序，Find 验证目标实体，每个预热和测量批都检查结果校验和。创建、验证与输出不计入耗时。没有启动系统或触碰正式场景、默认 World、玩家存档。\n\n");
            text.Append("限制：这是 Editor 主线程实际辅助函数的微基准，不是完整游戏帧率或 Player/Burst 结果。单一 archetype、无并发 jobs 和结构变更；没有覆盖真实地图各类实体占比及调用频率。热缓存与 Editor/操作系统调度会影响结果；显式 GC 仅在每个 case 预热后执行，采样期间自然 GC 仍计入。计时包含循环、校验和计算和 NativeArray.Dispose；Harness 行只提供参考，未从结果中扣除。当前线程 managed 分配不含 NativeArray/EntityQuery 原生内存和其他线程分配；native列仅由源码及 Entity 大小推导最低数组载荷，绝非实测原生总分配。Temp 分配器可能在 Editor 帧尾回收底层块。零 managed 分配不等于零分配。\n\n");
            text.Append("后续判断：结合 Player Profiler 中的实际调用次数、主线程预算和地图规模，再决定是否建立查询/身份索引；当前采样不引入索引，不改变任何玩法实现。\n\n");
            text.Append("源码 SHA-256：`").Append(report.sourceSha256).Append("`；Core DLL：`").Append(report.coreAssemblySha256).Append("`；packages-lock：`").Append(report.packageLockSha256).Append("`。原始逐批时间、分配、环境与校验和见同名 JSON/CSV。\n");
            return text.ToString();
        }
        static string Csv(RunReport report)
        {
            var text = new StringBuilder("entity_count,operation,sample,calls,elapsed_ticks,microseconds_per_call,managed_bytes,managed_bytes_per_call\n");
            foreach (var result in report.results) foreach (var sample in result.samples)
                text.Append(result.entityCount).Append(',').Append(result.operation).Append(',').Append(sample.index).Append(',').Append(result.callsPerBatch).Append(',').Append(sample.elapsedTicks).Append(',').Append(sample.microsecondsPerCall.ToString("R", Culture)).Append(',').Append(sample.managedBytes).Append(',').Append(sample.managedBytesPerCall.ToString("R", Culture)).Append('\n');
            return text.ToString();
        }
    }
}
#endif
