#if UNITY_EDITOR
using System;
using System.Text;
using Landsong.ECS.Authoring;
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
        int nightIndex;
        int turn = 3;
        int playerPower = 3;
        int seed = 1;
        WeatherKind weather = WeatherKind.Sunny;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var catalog = (NightEventCatalogAsset)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("夜晚波次预览", EditorStyles.boldLabel);
            content = (GameContentSetAsset)EditorGUILayout.ObjectField("游戏内容集合", content, typeof(GameContentSetAsset), false);
            if (content == null)
                FindContent(catalog);
            if (content == null || content.NightEvents != catalog)
            {
                EditorGUILayout.HelpBox("请选择引用此夜晚目录的 GameContentSet。", MessageType.Info);
                return;
            }
            if (catalog.Nights == null || catalog.Nights.Length == 0)
                return;
            var names = new string[catalog.Nights.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = catalog.Nights[i] == null ? "<空夜晚>" : catalog.Nights[i].Id;
            nightIndex = EditorGUILayout.Popup("预览夜晚", math.clamp(nightIndex, 0, names.Length - 1), names);
            turn = math.max(1, EditorGUILayout.IntField("回合", turn));
            playerPower = math.max(0, EditorGUILayout.IntField("玩家战力", playerPower));
            weather = (WeatherKind)EditorGUILayout.EnumPopup("天气", weather);
            seed = EditorGUILayout.IntField("时间波动样本种子", seed);
            if (GUILayout.Button("生成并打印本夜波次到 Console", GUILayout.Height(30)))
                Preview(catalog.Nights[nightIndex]);
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

        void Preview(NightDefinitionAsset night)
        {
            if (night == null)
            {
                Debug.LogError("夜晚定义为空。", target);
                return;
            }
            var scale = NightTemplatePlanning.Difficulty(content.Night, turn, playerPower);
            var eligible = turn >= night.MinTurn && (night.MaxTurn == 0 || turn <= night.MaxTurn)
                && (night.Interval == 0 || (turn - night.MinTurn) % night.Interval == 0)
                && (((int)night.AllowedWeather & (1 << (int)weather)) != 0);
            var rng = new Random(seed == 0 ? 1u : (uint)seed);
            var result = new StringBuilder();
            result.Append("[夜晚波次预览] ").Append(night.Id)
                .Append(" | 回合 ").Append(turn)
                .Append(" | 玩家战力 ").Append(playerPower)
                .Append(" | 难度系数 ").Append(scale.ToString("0.###"))
                .Append(" | 天气 ").Append(WeatherKindOps.DisplayName(weather));
            if (!eligible)
                result.Append(" | 当前回合或天气不满足基本出现条件");
            result.AppendLine();
            if (night.Waves == null || night.Waves.Length == 0)
            {
                result.AppendLine("本夜为平安夜，不生成敌人。");
                Debug.Log(result.ToString(), night);
                return;
            }
            int actualWaves = 0;
            for (int i = 0; i < night.Waves.Length; i++)
            {
                var wave = night.Waves[i];
                if (wave == null) continue;
                var seconds = wave.AtSeconds + (wave.JitterSeconds > 0 ? rng.NextFloat(-wave.JitterSeconds, wave.JitterSeconds) : 0);
                seconds = math.clamp(seconds, 0, content.Night.NightSeconds - .001f);
                var row = new StringBuilder();
                if (wave.Enemies != null)
                    foreach (var entry in wave.Enemies)
                    {
                        if (entry == null || entry.Enemy == null) continue;
                        var count = NightTemplatePlanning.Count(entry.Weight, scale, entry.Fixed, entry.FixedCount);
                        if (count <= 0) continue;
                        if (row.Length > 0) row.Append("、");
                        row.Append(string.IsNullOrEmpty(entry.Enemy.Metadata.Name) ? entry.Enemy.name : entry.Enemy.Metadata.Name)
                            .Append('×').Append(count);
                    }
                if (row.Length == 0) continue;
                actualWaves++;
                result.Append("第").Append(i + 1).Append("波 入夜")
                    .Append(seconds.ToString("0.##")).Append("秒 ").Append(row).AppendLine();
            }
            result.Insert(0, "本夜会生成 " + actualWaves + " 波敌人（时间波动样本）\n");
            result.Append("波次数量由模板固定；各敌人向下取整。实际游戏会在规划时锁定波动时间，并选择入场区域。");
            Debug.Log(result.ToString(), night);
        }
    }
}
#endif
