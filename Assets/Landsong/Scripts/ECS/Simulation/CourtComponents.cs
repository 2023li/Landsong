using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct CourtSettings
    {
        public int MarriageAge, CaptainAge, GiftCost, GiftAffection, RecruitAffection, MarriageAffection;
        public int StableDesignationTurns, MinimumReign, TemporaryTurns, ElectionTurns, DisorderTurns;
        public int VisitInterval, VisitDuration, VisitCost;
        public int MarriageRequestCooldown;
        public int ExpeditionRequestCooldown;
        public float ExpeditionRequestChance;
        public float MarriageRequestChance, MarriageRefusalGrievanceChance, MarriageRefusalGrievance;
        public int InitialOpinion, OpinionRecovery, DisorderOpinionCost;
        public float PrinceGrowth, StrongInfluence, UsurpGap, UsurpChance, RegicideGap, RegicideChance, PrinceRisk;
        public float StableProduction, StableAttack, WeakProduction, WeakAttack, ElectionProduction, ElectionAttack;
        public float UsurpProduction, UsurpAttack, RegicideProduction, RegicideAttack, DisorderPerStack, DisorderCap;
        public float DeathYoung, DeathAdult, DeathMature, DeathOld, DeathAncient;
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
