#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    /// <summary>One entry per domain; shared regression suites never invoke one another.</summary>
    public static class ProjectVerification
    {
        internal readonly struct VerificationSuite
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Category;
            public readonly Func<string> Run;

            public VerificationSuite(string id, string displayName, string category, Func<string> run)
            {
                Id = id;
                DisplayName = displayName;
                Category = category;
                Run = run;
            }
        }

        public const string ReportPath = "Library/LandsongEcs/regressions.txt";
        internal static readonly VerificationSuite[] Suites =
        {
            new VerificationSuite(nameof(GameMapWorkflowVerification), "地图制作工作流", "地图", GameMapWorkflowVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.InitialBuildingSetupVerification), "初始建筑绑定", "地图", Landsong.EditorTools.InitialBuildingSetupVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.MapBoundaryVerification), "地图边界", "地图", Landsong.EditorTools.MapBoundaryVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.MapPopulationVerification), "地图人口与资源生成", "地图", Landsong.EditorTools.MapPopulationVerification.Run),
            new VerificationSuite(nameof(LayerTerrainVerification), "分层地形规则", "地图", LayerTerrainVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.ProtrudingSlopeVerification), "突出斜坡", "地图", Landsong.EditorTools.ProtrudingSlopeVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.TerrainEnumVerification), "地形枚举与映射", "地图", Landsong.EditorTools.TerrainEnumVerification.Run),
            new VerificationSuite(nameof(RuntimeMapArtifactVerification), "运行时地图产物", "地图", RuntimeMapArtifactVerification.Run),
            new VerificationSuite(nameof(TerrainConnectionVerification), "地形连接与寻路", "地图", TerrainConnectionVerification.Run),
            new VerificationSuite(nameof(PresentationVerification), "表现层生命周期", "表现", PresentationVerification.Run),
            new VerificationSuite(nameof(InterfaceArchiveVerification), "界面存档契约", "界面", InterfaceArchiveVerification.Run),
            new VerificationSuite(nameof(PeacefulVerification), "和平流程", "玩法", PeacefulVerification.Run),
            new VerificationSuite(nameof(NightRewardJournalVerification), "夜间奖励记录", "玩法", NightRewardJournalVerification.Run),
            new VerificationSuite(nameof(IntelligenceVerification), "情报系统", "玩法", IntelligenceVerification.Run),
            new VerificationSuite(nameof(BuildingCatalogVerification), "建筑目录", "建筑", BuildingCatalogVerification.Run),
            new VerificationSuite(nameof(PauseMenuVerification), "暂停菜单", "界面", PauseMenuVerification.Run),
            new VerificationSuite(nameof(CombatVerification), "战斗系统", "战斗", CombatVerification.Run),
            new VerificationSuite(nameof(TacticalReservationVerification), "接战位预约", "战斗", TacticalReservationVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.WolfVerification), "狼单位", "单位", Landsong.EditorTools.WolfVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.TransportWorkerVerification), "运输工人", "单位", Landsong.EditorTools.TransportWorkerVerification.Run),
            new VerificationSuite(nameof(HeroVerification), "英雄系统", "单位", HeroVerification.Run),
            new VerificationSuite(nameof(SoldierVerification), "士兵系统", "单位", SoldierVerification.Run),
            new VerificationSuite(nameof(Landsong.EditorTools.SoldierAnimationVerification), "士兵动画配置", "动画", Landsong.EditorTools.SoldierAnimationVerification.Run),
            new VerificationSuite(nameof(NightPlanningVerification), "夜间规划", "玩法", NightPlanningVerification.Run),
            new VerificationSuite(nameof(NightLightingVerification), "昼夜光照逻辑", "表现", NightLightingVerification.Run),
            new VerificationSuite(nameof(DynamicSpawnVerification), "动态出生区域", "地图", DynamicSpawnVerification.Run),
            new VerificationSuite(nameof(CourtVerification), "宫廷系统", "宫廷", CourtVerification.Run),
            new VerificationSuite(nameof(InvitationExpeditionVerification), "邀请与远征", "宫廷", InvitationExpeditionVerification.Run),
            new VerificationSuite(nameof(QuestVerification), "任务系统", "玩法", QuestVerification.Run),
            new VerificationSuite(nameof(TechnologyVerification), "科技系统", "玩法", TechnologyVerification.Run),
            new VerificationSuite(nameof(WorkforceVerification), "劳动力系统", "经济", WorkforceVerification.Run),
            new VerificationSuite(nameof(InventoryVerification), "库存系统", "经济", InventoryVerification.Run),
            new VerificationSuite(nameof(InventoryUiVerification), "库存界面模型", "界面", InventoryUiVerification.Run),
            new VerificationSuite(nameof(EconomyVerification), "经济系统", "经济", EconomyVerification.Run),
            new VerificationSuite(nameof(BillVerification), "账单系统", "经济", BillVerification.Run),
            new VerificationSuite(nameof(TransactionVerification), "事务系统", "经济", TransactionVerification.Run),
            new VerificationSuite(nameof(PersistenceContractVerification), "存档协议", "存档", PersistenceContractVerification.Run),
            new VerificationSuite(nameof(ArchiveApplicationServiceVerification), "存档应用服务", "存档", ArchiveApplicationServiceVerification.Run),
            new VerificationSuite(nameof(CoreRulesVerification), "核心规则", "核心", CoreRulesVerification.Run),
            new VerificationSuite(nameof(UiConfigurationVerification), "界面配置", "界面", UiConfigurationVerification.Run),
            new VerificationSuite(nameof(EcsVerification), "ECS 基线", "核心", EcsVerification.Run),
            new VerificationSuite(nameof(BuildingFeatureVerification), "建筑工作流", "建筑", BuildingFeatureVerification.Run),
            new VerificationSuite(nameof(BuildingModuleVerification), "建筑模块", "建筑", BuildingModuleVerification.Run),
            new VerificationSuite(nameof(ContentInspectorVerification), "内容检查器", "内容", ContentInspectorVerification.Run),
            new VerificationSuite(nameof(ContentCompilationVerification), "内容编译", "内容", ContentCompilationVerification.Run),
            new VerificationSuite(nameof(ContentAuthoringWorkflowVerification), "内容制作工作流", "内容", ContentAuthoringWorkflowVerification.Run),
            new VerificationSuite(nameof(DocumentationVerification), "项目文档", "架构", DocumentationVerification.Run),
            new VerificationSuite(nameof(PortraitVerification), "肖像系统", "表现", PortraitVerification.Run),
            new VerificationSuite(nameof(PortraitImportVerification), "肖像导入", "内容", PortraitImportVerification.Run),
            new VerificationSuite(nameof(ArchitectureVerification), "项目架构", "架构", ArchitectureVerification.Run),
            new VerificationSuite(nameof(DomainArchitectureVerification), "领域架构边界", "架构", DomainArchitectureVerification.Run),
            new VerificationSuite(nameof(GameUiRefreshVerification), "游戏界面刷新", "界面", GameUiRefreshVerification.Run),
            new VerificationSuite(nameof(GameUiInputPolicyVerification), "游戏输入策略", "界面", GameUiInputPolicyVerification.Run),
            new VerificationSuite(nameof(GamePanelNavigationVerification), "游戏面板导航", "界面", GamePanelNavigationVerification.Run),
            new VerificationSuite(nameof(GameUiStableRowsVerification), "游戏界面稳定行", "界面", GameUiStableRowsVerification.Run),
            new VerificationSuite(nameof(ApplicationCleanupRecoveryVerification), "应用清理恢复", "生命周期", ApplicationCleanupRecoveryVerification.Run),
            new VerificationSuite(nameof(SceneRequestVerification), "场景请求状态机", "生命周期", SceneRequestVerification.Run),
            new VerificationSuite(nameof(RewardAuthoringVerification), "奖励配置", "内容", RewardAuthoringVerification.Run),
            new VerificationSuite(nameof(EntitlementRewardVerification), "权限与奖励", "玩法", EntitlementRewardVerification.Run),
            new VerificationSuite(nameof(SessionBoundaryVerification), "会话边界", "生命周期", SessionBoundaryVerification.Run),
            new VerificationSuite(nameof(GameplayRequestVerification), "玩法请求队列", "核心", GameplayRequestVerification.Run),
            new VerificationSuite(nameof(EffectVerification), "效果系统", "玩法", EffectVerification.Run),
            new VerificationSuite(nameof(ResearchAuthorityVerification), "科研权限", "玩法", ResearchAuthorityVerification.Run)
        };

        internal static VerificationSuite GetSuite(string id)
        {
            var suite = Array.Find(Suites, candidate => candidate.Id == id);
            if (suite.Run == null)
                throw new InvalidOperationException("Unknown verification suite: " + id);
            return suite;
        }

        internal static int AssertionCount(string result)
        {
            var matches = Regex.Matches(result ?? string.Empty, @"Assertions: (\d+)");
            if (matches.Count != 1)
                throw new InvalidOperationException("Suite must report exactly one assertion total.");
            int count = int.Parse(matches[0].Groups[1].Value);
            if (count <= 0)
                throw new InvalidOperationException("Suite executed no assertions.");
            return count;
        }
        [MenuItem("Landsong/ECS/Verification/Run all")]
        public static string RunAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Library/LandsongEcs");
            var report = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            int assertions = 0, passed = 0, failed = 0;
            try
            {
                ContentValidation.Validate();
                report.AppendLine("PASS native content validation");
            }
            catch (Exception error)
            {
                failed++;
                report.AppendLine("FAIL content validation: " + error);
            }

            foreach (var suite in Suites)
            {
                try
                {
                    var result = suite.Run();
                    int count = AssertionCount(result);
                    assertions += count;
                    passed++;
                    report.AppendLine("PASS " + suite.Id + ": " + count + " assertions");
                }
                catch (Exception error)
                {
                    failed++;
                    report.AppendLine("FAIL " + suite.Id + ": " + error);
                }

                File.WriteAllText(ReportPath, report.ToString());
            }

            report.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O"));
            report.AppendLine((failed == 0 ? "PASS" : "FAIL") + " — suites=" + passed + ", failures=" + failed + ", assertions=" + assertions);
            File.WriteAllText(ReportPath, report.ToString());
            if (failed != 0)
                throw new InvalidOperationException(report.ToString());
            Debug.Log(report.ToString());
            return report.ToString();
        }

        // Unity command line: -batchmode -quit -executeMethod Landsong.ECS.Editor.ProjectVerification.Batch
        public static void Batch()
        {
            ValidateBatch();
            Debug.Log(EcsVerification.Build());
            Debug.Log(EcsVerification.BuildRelease());
        }

        public static void ValidateBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Batch entry requires Unity -batchmode.");
            // A headless editor may start without a scene; plugin build validators restore the saved setup.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Landsong.ECS.Presentation.EcsSceneFlow.Boot);
            RunAll();
        }
    }
}
#endif
