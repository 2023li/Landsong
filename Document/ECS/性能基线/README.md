# 查询性能基线

N08 本轮按 P3「先采样」建立基线，已在 Unity Editor 调用实际 `Sim.Find` 与 `Sim.OrderedEntities<Building>`，完成结果正确性校验。尚无 Player 中的实际调用频率、主线程占比与目标帧预算证据，因此本轮没有引入实体索引，也不宣称性能优化达标。

## 本次原始结果

采样开始时间为 2026-09-11 14:26:01 UTC（北京时间 22:26:01）。以下三份文件按原样保存，不以人工填写的数字替代原始数据：

- [完整报告与限制](simulation-queries-20260911-142621-013.md)
- [逐批原始 CSV](simulation-queries-20260911-142621-013.csv)
- [环境、源码及程序集 SHA-256、校验和、逐批数据 JSON](simulation-queries-20260911-142621-013.json)

在本机 Unity 6000.3.5f2、Windows 11、i7-14650HX、Collections 检查开启的 Editor 环境中，10,000 实体时，末尾命中 `Find` 的 p50 为 **1.309 ms/次**，完整排序的 p50 为 **37.064 ms/次**。这是批平均单次耗时的分位数，不能换算为游戏帧率，也不能当成 Player 性能结果。

所有被测操作的当前线程 managed 分配均值为 0 B/次。它不包括原生内存；除 ID=0 的快速返回与计时辅助项外，这一规模下每次查询的实体数组最低载荷为 80,000 B。该数字按源码及 `Entity` 大小推导，**不是实测原生总分配量**。

## 复现方法

工具源码：[SimulationQueryBenchmark.cs](../../../Assets/Landsong/Scripts/Editor/Tests/Verification/SimulationQueryBenchmark.cs)。退出 Play、等待编译完成后执行菜单 `Landsong/ECS/Performance/Sample simulation queries`，或由编辑器自动化调用 `SampleSimulationQueries`，实际入口为 `Landsong.ECS.Editor.SimulationQueryBenchmark.Run()`。

工具只创建和销毁隔离 `World`，不设置默认 World，不运行系统，不依赖正式场景或玩家存档。成功后在 `Library/LandsongEcs/Performance/` 输出同时间戳的 Markdown、JSON、CSV，并更新 `Library/LandsongEcs/performance-query-benchmark.md`。它没有加入常规 `VerifyAll`，正确性通过不表示达到性能阈值。

固定夹具与采样参数：

- 实体规模为 100 / 1,000 / 10,000，均为 `Identity + Building` 的单一 archetype。这是受控的小、中、大量级，不代表当前典型地图的实际实体数量。
- 使用固定种子 `0x51A7C0DE` 洗牌唯一标识。Find 分别采样 ID=0、查询首/中/末命中、不命中、固定均匀命中；排序输入为洗牌后的完整实体集。
- 每项预热 8 批、测量 25 批；Find 每批 8 次，排序每批 2 次。每项预热后在计时区间外显式 GC，样本间不强制 GC；自然 GC 次数保留在报告中。
- 每个预热和测量批检查结果校验和，采样前后完整检查排序实体、标识及数量。创建、校验、报告输出不计时；循环、校验和计算和数组释放计时。
- `Stopwatch` 记录耗时，`GC.GetAllocatedBytesForCurrentThread` 记录当前线程 managed 分配。p95 是 25 个批平均值的 nearest-rank 分位数，不是逐次调用的尾延迟。

## 解读与后续取证

本次没有覆盖混合 archetype、并发 jobs、结构变化、长期王朝、超大军团、真实地图中的函数调用次数，也没有测量整帧、GPU 或 Player/Burst 成本。Editor 安全检查、热缓存、CPU 频率和系统调度会影响结果。零 managed 分配不等于零分配；Temp 分配器还可能在帧尾才回收底层原生块。

下一步应在开发版 Player 的真实地图和目标设备中，用 Profiler 记录上述调用次数与占用时间，结合主线程预算判断热点。若确需引入查询缓存或身份索引，再单独设计实体创建、销毁、存档恢复与回滚时的一致性维护，并用相同夹具及真实场景作前后对照。本次基线本身不构成改写现有实现的依据。
