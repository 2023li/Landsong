using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;

namespace Landsong.ECS.Authoring
{
    public static class NightEventCatalogCompiler
    {
        public static BlobAssetReference<NightEventCatalogBlob> Build(NightEventCatalogAsset catalog, EnemyCatalogIndex enemies, BuildingCatalogIndex buildings, ItemCatalogIndex items, TechnologyCatalogIndex technologies, BuffCatalogIndex buffs, FeatureCatalogIndex features, QuestCatalogIndex quests, ExpeditionCatalogIndex expeditions, float nightSeconds = 0)
        {
            if (catalog != null && catalog.Nights != null && catalog.Nights.Length > 0)
                return BuildNights(catalog, enemies, buildings, items, technologies, buffs, features, quests, expeditions, nightSeconds);
            if (catalog == null || catalog.Events == null)
                throw new InvalidOperationException("缺少夜晚事件目录。");
            var generator = (catalog.WaveGenerator ?? new BudgetNightWaveGeneratorSource()).Compile();
            if (generator.Kind == NightWaveGeneratorKind.Budget && (!math.isfinite(generator.MinimumCountScale) || generator.MinimumCountScale <= 0 || !math.isfinite(generator.MaximumCountScale) || generator.MaximumCountScale < generator.MinimumCountScale) ||
                generator.Kind == NightWaveGeneratorKind.FixedCount && (generator.FixedCount < 1 || generator.FixedCount > 256) ||
                (byte)generator.Kind > (byte)NightWaveGeneratorKind.FixedCount)
                throw new InvalidOperationException("夜晚波次生成器配置无效。");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            bool fallback = false;
            foreach (var e in catalog.Events)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.Id) || System.Text.Encoding.UTF8.GetByteCount(e.Id) > 60 || !ids.Add(e.Id) || (byte)e.Kind > 2 || e.MinTurn < 1 || e.MaxTurn != 0 && e.MaxTurn < e.MinTurn || e.Interval < 0 || e.Cooldown < 0 || e.ReturnDelay < 1 || e.WaveCount < 1 || e.WaveCount > 32 || !math.isfinite(e.Weight) || e.Weight <= 0 || !math.isfinite(e.BudgetScale) || e.BudgetScale <= 0)
                    throw new InvalidOperationException("夜晚事件配置无效：" + e?.Id);
                if (e.WaveTimes == null || e.WaveTimes.Length != 0 && e.WaveTimes.Length != e.WaveCount)
                    throw new InvalidOperationException("波次时间数量不符：" + e.Id);
                for (int i = 0; i < e.WaveTimes.Length; i++)
                    if (!math.isfinite(e.WaveTimes[i]) || e.WaveTimes[i] < 0 || e.WaveTimes[i] >= 1 || i > 0 && e.WaveTimes[i] <= e.WaveTimes[i - 1])
                        throw new InvalidOperationException("波次时间必须非负、严格递增且小于一：" + e.Id);
                bool boss = false, ordinary = false;
                if (e.Enemies == null || e.Conditions == null)
                    throw new InvalidOperationException("缺少敌军池或夜晚条件：" + e.Id);
                foreach (var choice in e.Enemies)
                {
                    if (choice == null || choice.Enemy == null || !math.isfinite(choice.Weight) || choice.Weight <= 0 || choice.Enemy.ThreatValue <= 0)
                        throw new InvalidOperationException("夜晚敌军池无效：" + e.Id);
                    enemies.Resolve(choice.Enemy);
                    if ((choice.Enemy.Behavior & EnemyBehaviorFlags.Boss) != 0)
                        boss = true;
                    else
                        ordinary = true;
                }

                if (e.Kind == NightKind.Peaceful && (boss || ordinary) || e.Kind == NightKind.Invasion && (!ordinary || boss) || e.Kind == NightKind.Boss && !boss)
                    throw new InvalidOperationException("夜晚类型与敌军池不符：" + e.Id);
                if (e.Kind == NightKind.Peaceful && !e.Once && !e.ReturnOnly && e.MinTurn == 1 && e.MaxTurn == 0 && e.Cooldown == 0 && e.Interval == 0 && Unconditional(e.Conditions))
                    fallback = true;
                if (!string.IsNullOrEmpty(e.FollowUp))
                {
                    var next = Array.Find(catalog.Events, v => v != null && v.Id == e.FollowUp);
                    if (next == null || next.Kind != NightKind.Boss || !next.ReturnOnly || e.Kind != NightKind.Boss)
                        throw new InvalidOperationException("首领后续必须指向仅返回的首领事件。");
                }
            }

            if (!fallback)
                throw new InvalidOperationException("夜晚事件目录需要无条件、可重复的平安夜后备事件。");
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<NightEventCatalogBlob>();
                var events = builder.Allocate(ref root.Events, catalog.Events.Length);
                for (int i = 0; i < events.Length; i++)
                {
                    var source = catalog.Events[i];
                    ref var target = ref events[i];
                    target.Id = new FixedString64Bytes(source.Id);
                    target.FollowUp = new FixedString64Bytes(source.FollowUp ?? "");
                    target.Kind = source.Kind;
                    target.Priority = source.Priority;
                    target.MinTurn = source.MinTurn;
                    target.MaxTurn = source.MaxTurn;
                    target.Interval = source.Interval;
                    target.Cooldown = source.Cooldown;
                    target.WaveCount = source.WaveCount;
                    target.ReturnDelay = source.ReturnDelay;
                    target.Weight = source.Weight;
                    target.BudgetScale = source.BudgetScale;
                    target.WaveGenerator = generator;
                    target.Once = (byte)(source.Once ? 1 : 0);
                    target.ReturnOnly = (byte)(source.ReturnOnly ? 1 : 0);
                    target.Forced = (byte)(source.Forced ? 1 : 0);
                    foreach (var time in source.WaveTimes)
                        target.WaveTimes.Add(time);
                    var pool = builder.Allocate(ref target.Enemies, source.Enemies.Length);
                    for (int n = 0; n < pool.Length; n++)
                        pool[n] = new NightEnemyChoice
                        {
                            Definition = enemies.Resolve(source.Enemies[n].Enemy),
                            Weight = source.Enemies[n].Weight
                        };
                    var condition = source.Conditions;
                    if (condition.MinimumTurn < 0)
                        throw new InvalidOperationException("夜晚最低回合不能为负数。");
                    target.Conditions.MinimumTurn = condition.MinimumTurn;
                    var bs = builder.Allocate(ref target.Conditions.Buildings, condition.Buildings.Length);
                    for (int n = 0; n < bs.Length; n++)
                    {
                        var b = condition.Buildings[n];
                        var id = buildings.Resolve(b.Building);
                        if (b.Count < 0 || b.MinimumLevel < 0 || b.MinimumLevel > b.Building.MaximumLevel)
                            throw new InvalidOperationException("夜晚建筑条件数量或等级无效。");
                        bs[n] = new NightBuildingCondition
                        {
                            Building = id,
                            Count = b.Count,
                            MinimumLevel = b.MinimumLevel
                        };
                    }

                    var its = builder.Allocate(ref target.Conditions.Items, condition.Items.Length);
                    for (int n = 0; n < its.Length; n++)
                    {
                        var item = condition.Items[n];
                        if (item.Quantity < 0)
                            throw new InvalidOperationException("夜晚物品条件数量无效。");
                        its[n] = new NightItemCondition
                        {
                            Item = items.Resolve(item.Item),
                            Quantity = item.Quantity
                        };
                    }

                    var tech = builder.Allocate(ref target.Conditions.Technologies, condition.Technologies.Length);
                    for (int n = 0; n < tech.Length; n++)
                    {
                        var t = condition.Technologies[n];
                        if (t.Count < 0)
                            throw new InvalidOperationException("夜晚科技完成次数无效。");
                        tech[n] = new NightTechnologyCondition
                        {
                            Technology = technologies.Resolve(t.Technology),
                            Count = t.Count
                        };
                    }

                    DefinitionPrerequisitesCompiler.Compile(ref builder, condition.Completions, ref target.Conditions.Completions, buffs, buildings, expeditions, features, quests, technologies);
                }

                return builder.CreateBlobAssetReference<NightEventCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        static BlobAssetReference<NightEventCatalogBlob> BuildNights(NightEventCatalogAsset catalog, EnemyCatalogIndex enemies, BuildingCatalogIndex buildings, ItemCatalogIndex items, TechnologyCatalogIndex technologies, BuffCatalogIndex buffs, FeatureCatalogIndex features, QuestCatalogIndex quests, ExpeditionCatalogIndex expeditions, float nightSeconds)
        {
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            bool fallback = false;
            foreach (var night in catalog.Nights)
            {
                if (night == null || string.IsNullOrWhiteSpace(night.Id) || System.Text.Encoding.UTF8.GetByteCount(night.Id) > 60 || !ids.Add(night.Id) ||
                    (byte)night.Kind > 2 || night.MinTurn < 1 || night.MaxTurn != 0 && night.MaxTurn < night.MinTurn || night.Interval < 0 || night.Cooldown < 0 ||
                    !math.isfinite(night.Weight) || night.Weight <= 0 || (int)night.AllowedWeather == 0 || ((int)night.AllowedWeather & ~31) != 0 || night.Conditions == null || night.Waves == null || night.Waves.Length > 32)
                    throw new InvalidOperationException("夜晚定义配置无效：" + (night == null ? "<空>" : night.Id));
                if (night.Kind == NightKind.Peaceful && night.Waves.Length != 0 || night.Kind != NightKind.Peaceful && night.Waves.Length == 0)
                    throw new InvalidOperationException("平安夜不能出兵，战斗夜必须有波次：" + night.Id);
                if (night.Kind == NightKind.Peaceful && !night.Once && !night.Guaranteed && night.MinTurn == 1 && night.MaxTurn == 0 && night.Interval == 0 && night.Cooldown == 0 && night.AllowedWeather == NightWeatherMask.Any && Unconditional(night.Conditions))
                    fallback = true;
                float previousLatest = -1;
                bool hasBoss = false;
                foreach (var wave in night.Waves)
                {
                    if (wave == null || !math.isfinite(wave.AtSeconds) || !math.isfinite(wave.JitterSeconds) || wave.JitterSeconds < 0 ||
                        wave.AtSeconds - wave.JitterSeconds < 0 || nightSeconds > 0 && wave.AtSeconds + wave.JitterSeconds >= nightSeconds ||
                        wave.AtSeconds - wave.JitterSeconds <= previousLatest || wave.Enemies == null || wave.Enemies.Length == 0)
                        throw new InvalidOperationException("夜晚波次时间或敌人列表无效：" + night.Id);
                    previousLatest = wave.AtSeconds + wave.JitterSeconds;
                    foreach (var row in wave.Enemies)
                    {
                        if (row == null || row.Enemy == null || !math.isfinite(row.Weight) || row.Weight < 0 || row.Weight > 256 || row.Fixed && (row.FixedCount < 1 || row.FixedCount > 256))
                            throw new InvalidOperationException("夜晚敌人条目无效：" + night.Id);
                        enemies.Resolve(row.Enemy);
                        bool boss = (row.Enemy.Behavior & EnemyBehaviorFlags.Boss) != 0;
                        if (boss && (!row.Fixed || row.FixedCount != 1))
                            throw new InvalidOperationException("首领必须固定生成一只：" + night.Id);
                        hasBoss |= boss;
                    }
                }
                if (night.Kind == NightKind.Boss && !hasBoss || night.Kind != NightKind.Boss && hasBoss)
                    throw new InvalidOperationException("夜晚类型与首领波次不一致：" + night.Id);
            }
            if (!fallback)
                throw new InvalidOperationException("夜晚目录需要无条件、可重复的平安夜后备定义。");

            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<NightEventCatalogBlob>();
                var events = builder.Allocate(ref root.Events, catalog.Nights.Length);
                for (int i = 0; i < events.Length; i++)
                {
                    var source = catalog.Nights[i];
                    ref var target = ref events[i];
                    target.Id = new FixedString64Bytes(source.Id);
                    target.Kind = source.Kind;
                    target.Priority = source.Priority;
                    target.MinTurn = source.MinTurn;
                    target.MaxTurn = source.MaxTurn;
                    target.Interval = source.Interval;
                    target.Cooldown = source.Cooldown;
                    target.WaveCount = source.Waves.Length;
                    target.Weight = source.Weight;
                    target.BudgetScale = 1;
                    target.Once = (byte)(source.Once ? 1 : 0);
                    target.Forced = (byte)(source.Guaranteed ? 1 : 0);
                    target.AllowedWeather = (int)source.AllowedWeather;
                    target.OpeningCaption = new FixedString512Bytes(source.OpeningCaption ?? "");
                    target.SpecialCaption = new FixedString512Bytes(source.SpecialCaption ?? "");
                    target.VictoryCaption = new FixedString512Bytes(source.VictoryCaption ?? "");
                    var waves = builder.Allocate(ref target.Waves, source.Waves.Length);
                    var flattened = new System.Collections.Generic.List<NightEnemyChoice>();
                    for (int w = 0; w < waves.Length; w++)
                    {
                        var template = source.Waves[w];
                        waves[w].AtSeconds = template.AtSeconds;
                        waves[w].JitterSeconds = template.JitterSeconds;
                        var rows = builder.Allocate(ref waves[w].Enemies, template.Enemies.Length);
                        for (int n = 0; n < rows.Length; n++)
                        {
                            var row = template.Enemies[n];
                            rows[n] = new NightWaveEnemyTemplate
                            {
                                Definition = enemies.Resolve(row.Enemy), Weight = row.Weight,
                                Fixed = (byte)(row.Fixed ? 1 : 0), FixedCount = row.FixedCount
                            };
                            flattened.Add(new NightEnemyChoice { Definition = rows[n].Definition, Weight = math.max(.001f, row.Weight) });
                        }
                    }
                    var pool = builder.Allocate(ref target.Enemies, flattened.Count);
                    for (int n = 0; n < pool.Length; n++) pool[n] = flattened[n];
                    CompileConditions(ref builder, source.Conditions, ref target.Conditions, buildings, items, technologies, buffs, features, quests, expeditions);
                }
                return builder.CreateBlobAssetReference<NightEventCatalogBlob>(Allocator.Persistent);
            }
            finally { builder.Dispose(); }
        }

        static void CompileConditions(ref BlobBuilder builder, NightEventConditionsSource condition, ref NightEventConditions target, BuildingCatalogIndex buildings, ItemCatalogIndex items, TechnologyCatalogIndex technologies, BuffCatalogIndex buffs, FeatureCatalogIndex features, QuestCatalogIndex quests, ExpeditionCatalogIndex expeditions)
        {
            if (condition.MinimumTurn < 0 || condition.Buildings == null || condition.Items == null || condition.Technologies == null || condition.Completions == null)
                throw new InvalidOperationException("夜晚前置条件无效。");
            target.MinimumTurn = condition.MinimumTurn;
            var bs = builder.Allocate(ref target.Buildings, condition.Buildings.Length);
            for (int n = 0; n < bs.Length; n++)
            {
                var source = condition.Buildings[n];
                if (source == null || source.Count < 0 || source.MinimumLevel < 0 || source.Building == null || source.MinimumLevel > source.Building.MaximumLevel)
                    throw new InvalidOperationException("夜晚建筑条件无效。");
                bs[n] = new NightBuildingCondition { Building = buildings.Resolve(source.Building), Count = source.Count, MinimumLevel = source.MinimumLevel };
            }
            var its = builder.Allocate(ref target.Items, condition.Items.Length);
            for (int n = 0; n < its.Length; n++)
            {
                var source = condition.Items[n];
                if (source == null || source.Quantity < 0) throw new InvalidOperationException("夜晚物品条件无效。");
                its[n] = new NightItemCondition { Item = items.Resolve(source.Item), Quantity = source.Quantity };
            }
            var tech = builder.Allocate(ref target.Technologies, condition.Technologies.Length);
            for (int n = 0; n < tech.Length; n++)
            {
                var source = condition.Technologies[n];
                if (source == null || source.Count < 0) throw new InvalidOperationException("夜晚科技条件无效。");
                tech[n] = new NightTechnologyCondition { Technology = technologies.Resolve(source.Technology), Count = source.Count };
            }
            DefinitionPrerequisitesCompiler.Compile(ref builder, condition.Completions, ref target.Completions, buffs, buildings, expeditions, features, quests, technologies);
        }

        static bool Unconditional(NightEventConditionsSource source)
        {
            var c = source.Completions;
            return source.MinimumTurn <= 1 && source.Buildings.Length == 0 && source.Items.Length == 0 && source.Technologies.Length == 0 && c.BuildingRequirements.Length == 0 && c.TechnologyRequirements.Length == 0 && c.BuffRequirements.Length == 0 && c.FeatureRequirements.Length == 0 && c.QuestRequirements.Length == 0 && c.ExpeditionRequirements.Length == 0;
        }
    }
}
