using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class NightSettingsAuthoring : MonoBehaviour
    {
        GameContentSetAsset Content => GetComponent<GameContentSetAuthoring>()?.Content;
        [ShowInInspector, ReadOnly, LabelText("昼夜流程（来自游戏内容集合）")]
        public NightSettings Settings => Content == null ? default : Content.Night;
        [ShowInInspector, ReadOnly, LabelText("白天归营（来自游戏内容集合）")]
        public DayReturnSettings DayReturn => Content == null ? default : Content.DayReturn;

        public static void Validate(NightSettings s)
        {
            if (!math.isfinite(s.NightSeconds) || s.NightSeconds < 10 || !math.isfinite(s.NightPreparationSeconds) || s.NightPreparationSeconds < 0 || !math.isfinite(s.DawnSeconds) || s.DawnSeconds < 0 || !math.isfinite(s.NightClosureSeconds) || s.NightClosureSeconds < 0 || !math.isfinite(s.TotalNightSeconds))
                throw new InvalidOperationException("正式夜晚至少十秒；准备、收尾与黎明时间须为有限非负数。夜晚总时长为准备＋正式夜晚＋收尾。");
            if (!math.isfinite(s.RetreatDelaySeconds) || s.RetreatDelaySeconds < 0 || !math.isfinite(s.VictoryCaptionDelaySeconds) || s.VictoryCaptionDelaySeconds < 0 || !math.isfinite(s.CelebrationDelaySeconds) || s.CelebrationDelaySeconds < 0 || !math.isfinite(s.ClosureCelebrationAt) || s.ClosureCelebrationAt > s.NightClosureSeconds)
                throw new InvalidOperationException("收尾三段延迟必须为非负有限秒数，且合计不超过收尾时长。");
            if (!math.isfinite(s.VictoryAdvanceDelaySeconds) || s.VictoryAdvanceDelaySeconds < 0 || !math.isfinite(s.BattleAdvanceAt))
                throw new InvalidOperationException("提前胜利按钮延迟必须为非负有限秒数。");
            if (!math.isfinite(s.WaveIntervalSeconds) || s.WaveIntervalSeconds <= 0 || s.WaveIntervalSeconds >= s.NightSeconds)
                throw new InvalidOperationException("默认波次间隔必须为正的有限秒数，且小于正式夜晚时长。");
        }

        public static void Validate(DayReturnSettings s)
        {
            if (!math.isfinite(s.MinimumSeconds) || !math.isfinite(s.MaximumSeconds) || s.MinimumSeconds <= 0 || s.MaximumSeconds < s.MinimumSeconds)
                throw new InvalidOperationException("白天归营超时区间须为正数，且上限不得小于下限。");
        }

        public sealed class Baker : Baker<NightSettingsAuthoring>
        {
            public override void Bake(NightSettingsAuthoring authoring)
            {
                var content = authoring.Content;
                if (content == null)
                    throw new InvalidOperationException("昼夜流程必须由 GameContentSetAuthoring 的正式游戏内容集合提供。");
                DependsOn(content);
                var s = authoring.Settings;
                Validate(s);
                Validate(authoring.DayReturn);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
                AddComponent(GetEntity(TransformUsageFlags.None), authoring.DayReturn);
            }
        }
    }
}
