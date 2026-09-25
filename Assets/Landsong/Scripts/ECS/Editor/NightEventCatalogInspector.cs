#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Sirenix.OdinInspector.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(NightEventCatalogAsset))]
    public sealed class NightEventCatalogInspector : OdinEditor
    {
        GameContentSetAsset content;
        int eventIndex;
        int turn = 3;
        int combatStrength = 100;
        int retryCount;
        int seed = 1;
        float threatFloor = NightRules.Default.ThreatFloor;
        float threatPerStrengthCap = NightRules.Default.ThreatPerStrengthCap;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var catalog = (NightEventCatalogAsset)target;
            EditorGUILayout.Space();
            var types = TypeCache.GetTypesDerivedFrom<NightWaveGeneratorSource>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.Name).ToArray();
            var currentKind = Array.FindIndex(types, type => type == catalog.WaveGenerator?.GetType());
            var labels = types.Select(type => ObjectNames.NicifyVariableName(type.Name.Replace("NightWaveGeneratorSource", ""))).ToArray();
            var selectedKind = EditorGUILayout.Popup("生成器类型", math.max(0, currentKind), labels);
            if (types.Length > 0 && (catalog.WaveGenerator == null || selectedKind != currentKind))
            {
                Undo.RecordObject(catalog, "切换夜晚波次生成器");
                catalog.WaveGenerator = (NightWaveGeneratorSource)Activator.CreateInstance(types[selectedKind]);
                EditorUtility.SetDirty(catalog);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("波次生成预览", EditorStyles.boldLabel);
            content = (GameContentSetAsset)EditorGUILayout.ObjectField("游戏内容集合", content, typeof(GameContentSetAsset), false);
            if (content == null)
            {
                FindContent(catalog);
                if (content == null)
                {
                    EditorGUILayout.HelpBox("请选择引用此夜晚事件目录的 GameContentSet。", MessageType.Info);
                    return;
                }
            }

            if (content.NightEvents != catalog)
            {
                EditorGUILayout.HelpBox("所选游戏内容集合未引用此夜晚事件目录。", MessageType.Warning);
                return;
            }

            var events = catalog.Events;
            if (events == null || events.Length == 0)
                return;
            var names = new string[events.Length];
            for (int i = 0; i < events.Length; i++)
                names[i] = events[i] == null ? "<空事件>" : events[i].Id;
            eventIndex = EditorGUILayout.Popup("预览事件", math.clamp(eventIndex, 0, events.Length - 1), names);
            turn = math.max(1, EditorGUILayout.IntField("回合", turn));
            combatStrength = math.max(0, EditorGUILayout.IntField("玩家战力", combatStrength));
            retryCount = math.max(0, EditorGUILayout.IntField("重试次数", retryCount));
            seed = EditorGUILayout.IntField("预览随机种子", seed);
            threatFloor = math.max(0, EditorGUILayout.FloatField("最低威胁预算", threatFloor));
            threatPerStrengthCap = math.max(0, EditorGUILayout.FloatField("每点战力预算上限", threatPerStrengthCap));
            EditorGUILayout.HelpBox("玩家战力由游戏中的 MilitaryStrength.Calculate 计算。此处预览敌人类型和数量；实际入场区域会额外消耗随机数，战局生成结果可能不同。", MessageType.None);
            if (GUILayout.Button("生成波次并打印到 Console", GUILayout.Height(30)))
                Preview(catalog, events[eventIndex]);
        }

        void FindContent(NightEventCatalogAsset catalog)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameContentSetAsset"))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<GameContentSetAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.NightEvents == catalog)
                {
                    content = candidate;
                    return;
                }
            }
        }

        void Preview(NightEventCatalogAsset catalog, NightEventSource nightEvent)
        {
            if (nightEvent == null)
            {
                Debug.LogError("夜晚事件为空。", catalog);
                return;
            }

            var settings = content.Night;
            var generator = (catalog.WaveGenerator ?? new BudgetNightWaveGeneratorSource()).Compile();
            if (nightEvent.Kind != NightKind.Peaceful && (nightEvent.WaveCount < 1 || nightEvent.Enemies == null || nightEvent.Enemies.Length == 0))
            {
                Debug.LogError("事件缺少波次或敌人池：" + nightEvent.Id, catalog);
                return;
            }
            if (nightEvent.WaveTimes != null && nightEvent.WaveTimes.Length != 0 && nightEvent.WaveTimes.Length != nightEvent.WaveCount)
            {
                Debug.LogError("波次时间数量必须与波次数量一致：" + nightEvent.Id, catalog);
                return;
            }

            var raw = turn * settings.ThreatPerTurn + combatStrength * settings.StrengthRatio;
            raw = math.min(raw, threatFloor + combatStrength * threatPerStrengthCap);
            var baseThreat = math.max(1, (int)(raw * nightEvent.BudgetScale));
            var threat = math.max(1, (int)(baseThreat * (1 - math.min(settings.RetryCap, retryCount * settings.RetryStep))));
            var rng = new Random(seed == 0 ? 1u : (uint)seed);
            var boss = nightEvent.Kind == NightKind.Boss ? Choose(nightEvent.Enemies, true, ref rng) : null;
            var lines = new StringBuilder();
            lines.Append("[夜晚波次预览] ").Append(nightEvent.Id)
                .Append(" | 回合 ").Append(turn)
                .Append(" | 玩家战力 ").Append(combatStrength)
                .Append(" | 基础威胁 ").Append(baseThreat)
                .Append(" | 本夜威胁 ").Append(threat).AppendLine();

            if (nightEvent.Kind == NightKind.Peaceful)
            {
                lines.Append("本夜为平安夜，不生成敌人。 ");
                Debug.Log(lines.ToString(), catalog);
                return;
            }

            int waves = 0;
            float power = 0;
            var detail = new StringBuilder();
            for (int i = 0; i < nightEvent.WaveCount; i++)
            {
                var enemy = nightEvent.Kind == NightKind.Boss && i == nightEvent.WaveCount - 1
                    ? boss : Choose(nightEvent.Enemies, false, ref rng);
                if (enemy == null)
                    continue;
                var isBoss = (enemy.Behavior & EnemyBehaviorFlags.Boss) != 0;
                var cost = math.max(1, enemy.ThreatValue);
                var count = NightPlanOps.WaveCount(generator, threat, nightEvent.WaveCount, cost, isBoss, ref rng);
                var fraction = nightEvent.WaveTimes != null && nightEvent.WaveTimes.Length > 0
                    ? nightEvent.WaveTimes[i] : NightPlanOps.DefaultWaveAt(settings, i, nightEvent.WaveCount);
                var seconds = fraction * settings.NightSeconds;
                detail.Append("第").Append(i + 1).Append("波  入夜")
                    .Append(seconds.ToString("0.##")).Append("秒预警  ")
                    .Append(string.IsNullOrEmpty(enemy.Metadata.Name) ? enemy.name : enemy.Metadata.Name)
                    .Append('×').Append(count).AppendLine();
                waves++;
                power += count * cost;
            }

            lines.Append("本夜会生成 ").Append(waves).Append(" 波敌人（预览样本）：").AppendLine();
            lines.Append(detail);
            lines.Append("统一属性倍率：生命、伤害各 ×")
                .Append(math.sqrt(math.max(.01f, threat / math.max(1, power))).ToString("0.###"))
                .AppendLine();
            lines.Append("所列时间为预警触发时间，敌人随后按 NightRules.WarningSeconds 延迟入场；实际游戏还会抽取入场区域、检查通路，提前消灭当前波次可让下一波提前出现。");
            Debug.Log(lines.ToString(), catalog);
        }

        static EnemyDefinitionAsset Choose(NightEnemySource[] choices, bool boss, ref Random rng)
        {
            float total = 0;
            EnemyDefinitionAsset selected = null;
            foreach (var choice in choices)
            {
                if (choice == null || choice.Enemy == null || choice.Weight <= 0 ||
                    ((choice.Enemy.Behavior & EnemyBehaviorFlags.Boss) != 0) != boss)
                    continue;
                total += choice.Weight;
                if (rng.NextFloat() * total < choice.Weight)
                    selected = choice.Enemy;
            }
            return selected;
        }
    }
}
#endif
