using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 通用运营经验模块。把它放在岗位和维护费之后：前置门控失败时本模块不会执行，
    /// 因而经验只会在岗位达标且本回合维护成功时增长。
    /// </summary>
    [Serializable]
    [BuildingModuleId("operational_experience")]
    public sealed class BM_运营经验 : BuildingModuleBase,
        IBuildingAutomaticTurnModule,
        IBuildingUpgradeRequirementSource,
        IBuildingModuleStateSerializer
    {
        [Serializable]
        private sealed class OperationalExperienceState
        {
            public int Experience;
            public int LastExperienceGained;
        }

        [TitleGroup("经验条件")]
        [SerializeField, LabelText("获得经验所需工人"), Min(0)]
        private int requiredWorkers;

        [SerializeField, LabelText("每回合经验"), Min(0)]
        private int experiencePerTurn;

        [SerializeField, LabelText("升下一级所需经验"), Min(0)]
        private int experienceRequiredForNextLevel;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("当前经验")]
        private int experience;

        [SerializeField, ReadOnly, LabelText("上回合获得经验")]
        private int lastExperienceGained;

        public override string ModuleDescription =>
            "在前置岗位和维护门控成功后累计运营经验，并作为统一升级条件参与校验。";

        public int RequiredWorkers => Mathf.Max(0, requiredWorkers);
        public int ExperiencePerTurn => Mathf.Max(0, experiencePerTurn);
        public int ExperienceRequiredForNextLevel => Mathf.Max(0, experienceRequiredForNextLevel);
        public int Experience => Mathf.Max(0, experience);
        public int LastExperienceGained => Mathf.Max(0, lastExperienceGained);

        public void ApplyConfiguration(
            int workerRequirement,
            int experienceGainPerTurn,
            int nextLevelExperience)
        {
            requiredWorkers = workerRequirement;
            experiencePerTurn = experienceGainPerTurn;
            experienceRequiredForNextLevel = nextLevelExperience;
            Normalize();
        }

        public override void Normalize()
        {
            requiredWorkers = Mathf.Max(0, requiredWorkers);
            experiencePerTurn = Mathf.Max(0, experiencePerTurn);
            experienceRequiredForNextLevel = Mathf.Max(0, experienceRequiredForNextLevel);
            experience = Mathf.Max(0, experience);
            lastExperienceGained = Mathf.Max(0, lastExperienceGained);
        }

        public bool ProcessAutomaticTurn(BuildingBase building)
        {
            Normalize();
            lastExperienceGained = 0;
            if (ExperiencePerTurn <= 0)
            {
                return true;
            }

            if (!BuildingWorkforceUtility.TryGetSource(building, out var workforce)
                || workforce.CurrentWorkers < RequiredWorkers)
            {
                return true;
            }

            lastExperienceGained = ExperiencePerTurn;
            experience += lastExperienceGained;
            building?.NotifyStateChanged();
            return true;
        }

        public bool CanUpgradeToLevel(
            BuildingBase building,
            int targetLevel,
            out string failureMessage)
        {
            if (building == null
                || targetLevel != building.CurrentLevel + 1
                || ExperienceRequiredForNextLevel <= 0)
            {
                failureMessage = string.Empty;
                return true;
            }

            if (Experience >= ExperienceRequiredForNextLevel)
            {
                failureMessage = string.Empty;
                return true;
            }

            failureMessage = $"运营经验不足：{Experience}/{ExperienceRequiredForNextLevel}。";
            return false;
        }

        public override string GetOverviewFragment(BuildingBase building)
        {
            return ExperienceRequiredForNextLevel > 0
                ? $"经验 {Experience}/{ExperienceRequiredForNextLevel}"
                : $"经验 {Experience}（最高等级）";
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "运营经验",
                    Experience,
                    new[]
                    {
                        new BuildingFunctionBlockSidebarRow(
                            "工人要求",
                            RequiredWorkers <= 0 ? "无" : $"至少 {RequiredWorkers}"),
                        new BuildingFunctionBlockSidebarRow(
                            "每回合经验",
                            ExperiencePerTurn.ToString()),
                        new BuildingFunctionBlockSidebarRow(
                            "升级要求",
                            ExperienceRequiredForNextLevel <= 0
                                ? "最高等级"
                                : $"{Experience}/{ExperienceRequiredForNextLevel}")
                    }));
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new OperationalExperienceState
            {
                Experience = Experience,
                LastExperienceGained = LastExperienceGained
            });
            return true;
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<OperationalExperienceState>(json);
            experience = Mathf.Max(0, state?.Experience ?? 0);
            lastExperienceGained = Mathf.Max(0, state?.LastExperienceGained ?? 0);
        }
    }
}
