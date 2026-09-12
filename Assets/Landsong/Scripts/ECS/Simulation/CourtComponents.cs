using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct CourtSettings
    {
        [LabelText("最低婚龄")] public int MarriageAge;
        [LabelText("最低队长年龄")] public int CaptainAge;
        [LabelText("赠礼费用")] public int GiftCost;
        [LabelText("赠礼好感增量")] public int GiftAffection;
        [LabelText("招募所需好感")] public int RecruitAffection;
        [LabelText("结婚所需好感")] public int MarriageAffection;
        [LabelText("稳固储位所需回合")] public int StableDesignationTurns;
        [LabelText("最短在位回合")] public int MinimumReign;
        [LabelText("临时政治影响回合")] public int TemporaryTurns;
        [LabelText("选举影响回合")] public int ElectionTurns;
        [LabelText("动荡持续回合")] public int DisorderTurns;
        [LabelText("外交出访间隔回合")] public int VisitInterval;
        [LabelText("外交出访持续回合")] public int VisitDuration;
        [LabelText("外交出访费用")] public int VisitCost;
        [LabelText("赐婚请求冷却回合")] public int MarriageRequestCooldown;
        [LabelText("远征请求冷却回合")] public int ExpeditionRequestCooldown;
        [LabelText("远征请求概率")] public float ExpeditionRequestChance;
        [LabelText("赐婚请求概率")] public float MarriageRequestChance;
        [LabelText("拒婚引发怨恨概率")] public float MarriageRefusalGrievanceChance;
        [LabelText("拒婚怨恨增量")] public float MarriageRefusalGrievance;
        [LabelText("初始民意")] public int InitialOpinion;
        [LabelText("每回合民意恢复")] public int OpinionRecovery;
        [LabelText("动荡民意损失")] public int DisorderOpinionCost;
        [LabelText("储君影响力成长系数")] public float PrinceGrowth;
        [LabelText("强势继承影响力阈值")] public float StrongInfluence;
        [LabelText("夺位影响力差值")] public float UsurpGap;
        [LabelText("夺位概率")] public float UsurpChance;
        [LabelText("弑君影响力差值")] public float RegicideGap;
        [LabelText("弑君概率")] public float RegicideChance;
        [LabelText("高风险出访死亡概率")] public float PrinceRisk;
        [LabelText("稳固继承生产加成")] public float StableProduction;
        [LabelText("稳固继承攻击加成")] public float StableAttack;
        [LabelText("弱势继承生产影响")] public float WeakProduction;
        [LabelText("弱势继承攻击影响")] public float WeakAttack;
        [LabelText("选举生产影响")] public float ElectionProduction;
        [LabelText("选举攻击影响")] public float ElectionAttack;
        [LabelText("夺位生产影响")] public float UsurpProduction;
        [LabelText("夺位攻击影响")] public float UsurpAttack;
        [LabelText("弑君生产影响")] public float RegicideProduction;
        [LabelText("弑君攻击影响")] public float RegicideAttack;
        [LabelText("每层动荡影响")] public float DisorderPerStack;
        [LabelText("动荡影响上限")] public float DisorderCap;
        [LabelText("幼年死亡概率")] public float DeathYoung;
        [LabelText("成年死亡概率")] public float DeathAdult;
        [LabelText("壮年死亡概率")] public float DeathMature;
        [LabelText("老年死亡概率")] public float DeathOld;
        [LabelText("高龄死亡概率")] public float DeathAncient;
        public static CourtSettings Default => new CourtSettings {
            MarriageAge=18, CaptainAge=16, GiftCost=10, GiftAffection=10, RecruitAffection=30, MarriageAffection=60,
            MarriageRequestCooldown=5, MarriageRequestChance=.12f, MarriageRefusalGrievanceChance=.5f, MarriageRefusalGrievance=.2f,
            ExpeditionRequestCooldown=8, ExpeditionRequestChance=.08f,
            StableDesignationTurns=3, MinimumReign=5, TemporaryTurns=5, ElectionTurns=8, DisorderTurns=5,
            VisitInterval=5, VisitDuration=3, VisitCost=20, InitialOpinion=50, OpinionRecovery=1, DisorderOpinionCost=5, PrinceGrowth=1.5f, StrongInfluence=40,
            UsurpGap=25, UsurpChance=.35f, RegicideGap=10, RegicideChance=.08f, PrinceRisk=.25f,
            StableProduction=.05f, StableAttack=.05f, WeakProduction=-.1f, WeakAttack=-.05f,
            ElectionProduction=-.15f, ElectionAttack=-.1f, UsurpProduction=-.25f, UsurpAttack=-.2f,
            RegicideProduction=-.35f, RegicideAttack=-.3f, DisorderPerStack=.05f, DisorderCap=.25f,
            DeathYoung=.001f, DeathAdult=.003f, DeathMature=.015f, DeathOld=.05f, DeathAncient=.12f };

    }
    public struct CourtState : IComponentData
    {
        public ulong Crown, LegacyFounder;
        public int CrownSince, LastSettledTurn, TemporaryUntil, DisorderUntil, LegacyGeneration, VisitOfferTurn;
        public float TemporaryProduction, TemporaryAttack, LegacyProduction, LegacyAttack, Disorder;
        public byte Extinction, LegacySeverity, VisitResolved;
    }
    [InternalBufferCapacity(0)] public struct CourtLogEntry : IBufferElementData
    { public int Turn; public ulong Person; public FixedString128Bytes Message; }
    public enum PersonRequestKind : byte { Marriage, Expedition, SocialTask, Portrait }
    public enum PersonRequestStatus : byte { Pending, Travelling, Completed, Refused, Cancelled }
    [InternalBufferCapacity(0)] public struct PersonRequestEntry : IBufferElementData
    { public PersonRequestKind Kind; public PersonRequestStatus Status; public int CreatedTurn, ResolvedTurn; public ulong Journey; }
}
