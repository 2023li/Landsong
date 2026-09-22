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
    [DisallowMultipleComponent]
    public sealed class CourtSettingsAuthoring : MonoBehaviour
    {
        [LabelText("宫廷设置")]
        public CourtSettings Settings = new CourtSettings
        {
            ExpeditionRequestCooldown = 8,
            ExpeditionRequestChance = 0.08f,
            MarriageRequestCooldown = 5,
            MarriageRequestChance = 0.12f,
            MarriageRefusalGrievanceChance = 0.5f,
            MarriageRefusalGrievance = 0.2f,
            MarriageAge = 18,
            CaptainAge = 16,
            GiftCost = 10,
            GiftAffection = 10,
            RecruitAffection = 30,
            MarriageAffection = 60,
            StableDesignationTurns = 3,
            MinimumReign = 5,
            TemporaryTurns = 5,
            ElectionTurns = 8,
            DisorderTurns = 5,
            VisitInterval = 5,
            VisitDuration = 3,
            VisitCost = 20,
            InitialOpinion = 50,
            OpinionRecovery = 1,
            DisorderOpinionCost = 5,
            PrinceGrowth = 1.5f,
            StrongInfluence = 40,
            UsurpGap = 25,
            UsurpChance = 0.35f,
            RegicideGap = 10,
            RegicideChance = 0.08f,
            PrinceRisk = 0.25f,
            StableProduction = 0.05f,
            StableAttack = 0.05f,
            WeakProduction = -0.1f,
            WeakAttack = -0.05f,
            ElectionProduction = -0.15f,
            ElectionAttack = -0.1f,
            UsurpProduction = -0.25f,
            UsurpAttack = -0.2f,
            RegicideProduction = -0.35f,
            RegicideAttack = -0.3f,
            DisorderPerStack = 0.05f,
            DisorderCap = 0.25f,
            DeathYoung = 0.001f,
            DeathAdult = 0.003f,
            DeathMature = 0.015f,
            DeathOld = 0.05f,
            DeathAncient = 0.12f
        };
        public static void Validate(CourtSettings s)
        {
            if (s.ExpeditionRequestCooldown < 1 || !math.isfinite(s.ExpeditionRequestChance) || s.ExpeditionRequestChance < 0 || s.ExpeditionRequestChance > 1)
                throw new InvalidOperationException("Court: invalid expedition request settings");
            if (s.MarriageRequestCooldown < 1)
                throw new InvalidOperationException("Court: invalid marriage request cooldown");
            foreach (var value in new[]
            {
                s.MarriageRequestChance,
                s.MarriageRefusalGrievanceChance,
                s.MarriageRefusalGrievance
            }

            )
                if (!math.isfinite(value) || value < 0 || value > 1)
                    throw new InvalidOperationException("Court: invalid marriage request probability/grievance");
            if (s.InitialOpinion < 0 || s.InitialOpinion > 100 || s.OpinionRecovery < 0 || s.DisorderOpinionCost < 0)
                throw new InvalidOperationException("Court: invalid public opinion settings");
            if (s.MarriageAge < 18 || s.CaptainAge < 1 || s.GiftCost < 0 || s.GiftAffection < 1 || s.RecruitAffection < 0 || s.MarriageAffection < s.RecruitAffection || s.MarriageAffection > 100 || s.PrinceGrowth < 1 || s.MinimumReign < 1 || s.VisitInterval < 1 || s.VisitDuration < 1 || s.VisitCost < 0 || s.TemporaryTurns < 1 || s.ElectionTurns < 1 || s.DisorderTurns < 1)
                throw new InvalidOperationException("Court: invalid ages, interaction or duration settings");
            foreach (var chance in new[]
            {
                s.DeathYoung,
                s.DeathAdult,
                s.DeathMature,
                s.DeathOld,
                s.DeathAncient,
                s.PrinceRisk,
                s.RegicideChance,
                s.UsurpChance
            }

            )
                if (!math.isfinite(chance) || chance < 0 || chance > 1)
                    throw new InvalidOperationException("Court: probability must be 0..1");
        }

        public sealed class Baker : Baker<CourtSettingsAuthoring>
        {
            public override void Bake(CourtSettingsAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
