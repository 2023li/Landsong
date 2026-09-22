using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/Presentation/Night Captions")]
    public sealed class NightCaptionDefinition : ScriptableObject
    {
        [LabelText("正式夜晚提示延迟（秒）"), Min(0)]
        public float NightCaptionDelay = 3;
        [LabelText("平安夜提示后按钮延迟（秒）"), Min(0)]
        public float PeacefulAdvanceDelay = 2;
        [LabelText("入侵夜字幕"), TextArea]
        public string InvasionNightCaption = "今晚有敌军来袭";
        [LabelText("平安夜字幕"), TextArea]
        public string PeacefulNightCaption = "今晚似乎是个平安夜";
        [LabelText("战斗胜利字幕"), TextArea]
        public string VictoryNightCaption = "胜利属于我们";

        public string Caption(Phase phase, NightKind kind, float nightElapsed, float closureElapsed = -1, float closureVictoryAt = 0, float battleVictoryElapsed = -1, float battleVictoryAt = 0)
        {
            if (phase == Phase.Celebration && battleVictoryElapsed >= battleVictoryAt) return VictoryNightCaption;
            if (phase == Phase.Retreat && closureElapsed >= closureVictoryAt) return VictoryNightCaption;
            if (phase != Phase.Night || nightElapsed < Mathf.Max(0, NightCaptionDelay)) return "";
            return kind == NightKind.Peaceful ? PeacefulNightCaption : InvasionNightCaption;
        }

        public bool PeacefulAdvanceReady(Phase phase, NightKind kind, float nightElapsed)
            => phase == Phase.Night && kind == NightKind.Peaceful
                && nightElapsed >= Mathf.Max(0, NightCaptionDelay) + Mathf.Max(0, PeacefulAdvanceDelay);
    }
}
