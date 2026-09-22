using Sirenix.OdinInspector;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct NightSettings : IComponentData
    {
        [LabelText("正式夜晚时长（秒）")]
        public float NightSeconds;
        [LabelText("出勤批次间隔（秒）")]
        public float DeployInterval;
        [LabelText("入夜准备阶段（秒）"), MinValue(0)]
        public float NightPreparationSeconds;
        [LabelText("夜晚收尾阶段（秒）"), MinValue(0)]
        public float NightClosureSeconds;
        [LabelText("收尾开始后，怪物逃跑延迟（秒）"), MinValue(0)]
        public float RetreatDelaySeconds;
        [LabelText("怪物逃跑后，胜利提示延迟（秒）"), MinValue(0)]
        public float VictoryCaptionDelaySeconds;
        [LabelText("胜利提示后，士兵欢呼延迟（秒）"), MinValue(0)]
        public float CelebrationDelaySeconds;
        [LabelText("士兵欢呼后，下一阶段按钮延迟（秒）"), MinValue(0)]
        public float VictoryAdvanceDelaySeconds;
        [LabelText("默认波次最大间隔（秒）"), MinValue(0.01)]
        public float WaveIntervalSeconds;
        public readonly float ClosureVictoryCaptionAt => RetreatDelaySeconds + VictoryCaptionDelaySeconds;
        public readonly float ClosureCelebrationAt => ClosureVictoryCaptionAt + CelebrationDelaySeconds;
        public readonly float BattleVictoryCaptionAt => VictoryCaptionDelaySeconds;
        public readonly float BattleCelebrationAt => BattleVictoryCaptionAt + CelebrationDelaySeconds;
        public readonly float BattleAdvanceAt => BattleCelebrationAt + VictoryAdvanceDelaySeconds;
        public readonly float TotalNightSeconds => NightPreparationSeconds + NightSeconds + NightClosureSeconds;
        [LabelText("黎明阶段（秒，属于白天）"), MinValue(0)]
        public float DawnSeconds;
        [LabelText("入侵概率")]
        public float InvasionChance;
        [LabelText("战力预算系数")]
        public float StrengthRatio;
        [LabelText("每次重试预算降幅")]
        public float RetryStep;
        [LabelText("重试预算降幅上限")]
        public float RetryCap;
        [LabelText("首次入侵回合")]
        public int FirstInvasion;
        [LabelText("首次首领回合")]
        public int FirstBoss;
        [LabelText("首领间隔回合")]
        public int BossInterval;
        [LabelText("每回合威胁预算")]
        public int ThreatPerTurn;
    }
}
