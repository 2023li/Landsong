using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/Presentation/Night Caption Effects")]
    public sealed class NightCaptionDefinition : ScriptableObject
    {
        [LabelText("正式夜晚提示延迟（秒）"),MinValue(0)]
        public float NightCaptionDelay = 3;
        [LabelText("平安夜提示后按钮延迟（秒）"), MinValue(0)]
        public float PeacefulAdvanceDelay = 2;
        [Header("字幕动画时间（秒）")]
        [LabelText("普通字幕展示时长"), MinValue(0)]
        public float StandardCaptionSeconds = 2f;
        [LabelText("平安夜淡入"), MinValue(0)]
        public float PeacefulFadeInSeconds = .35f;
        [LabelText("平安夜淡出"), MinValue(0)]
        public float PeacefulFadeOutSeconds = .35f;
        [LabelText("战斗夜淡入"), MinValue(0)]
        public float InvasionFadeInSeconds = .12f;
        [LabelText("战斗夜淡出"), MinValue(0)]
        public float InvasionFadeOutSeconds = .12f;
        [LabelText("Boss 开场淡入"), MinValue(0)]
        public float BossOpeningFadeInSeconds = .3f;
        [LabelText("Boss 开场抖动时长"), MinValue(0)]
        public float BossShakeSeconds = .3f;
        [LabelText("Boss 开场横向抖动幅度"), MinValue(0)]
        public float BossShakeHorizontal = 10f;
        [LabelText("Boss 开场纵向抖动幅度"), MinValue(0)]
        public float BossShakeVertical = 6f;
        [LabelText("Boss 开场碎散"), MinValue(0)]
        public float BossShatterSeconds = .35f;
        [LabelText("Boss 警告淡入"), MinValue(0)]
        public float BossWarningFadeInSeconds = .12f;
        [LabelText("Boss 警告淡出"), MinValue(0)]
        public float BossWarningFadeOutSeconds = .12f;

        public bool PeacefulAdvanceReady(Phase phase, NightKind kind, float nightElapsed)
            => phase == Phase.Night && kind == NightKind.Peaceful
                && nightElapsed >= Mathf.Max(0, NightCaptionDelay) + Mathf.Max(0, PeacefulAdvanceDelay);
    }
}
