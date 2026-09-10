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
        public const string ReportPath = "Library/LandsongEcs/regressions.txt";
        static readonly Func<string>[] Suites =
        {
            PresentationVerification.Run, InterfaceArchiveVerification.Run, PeacefulVerification.Run,
            IntelligenceVerification.Run, BuildingCatalogVerification.Run, PauseMenuVerification.Run,
            CombatVerification.Run, HeroVerification.Run, SoldierVerification.Run, NightPlanningVerification.Run,
            CourtVerification.Run, InvitationExpeditionVerification.Run, QuestVerification.Run,
            TechnologyVerification.Run, WorkforceVerification.Run, InventoryVerification.Run,
            EconomyVerification.Run, TransactionVerification.Run, CoreRulesVerification.Run,
            EcsVerification.Run, BuildingFeatureVerification.Run, PortraitVerification.Run, PortraitImportVerification.Run, ArchitectureVerification.Run
        };

        [MenuItem("Landsong/ECS/Verification/Run all")]
        public static string RunAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Library/LandsongEcs");
            var report = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            int assertions = 0, passed = 0, failed = 0;
            try { ContentValidation.Validate(); report.AppendLine("PASS native content validation"); }
            catch (Exception error) { failed++; report.AppendLine("FAIL content validation: " + error); }
            foreach (var run in Suites)
            {
                try
                {
                    var result = run();
                    var matches = Regex.Matches(result, @"Assertions: (\d+)");
                    if (matches.Count != 1) throw new InvalidOperationException("Suite must report exactly one assertion total.");
                    int count = int.Parse(matches[0].Groups[1].Value);
                    if (count <= 0) throw new InvalidOperationException("Suite executed no assertions.");
                    assertions += count; passed++;
                    report.AppendLine("PASS " + run.Method.DeclaringType.Name + ": " + count + " assertions");
                }
                catch (Exception error) { failed++; report.AppendLine("FAIL " + run.Method.DeclaringType.Name + ": " + error); }
                File.WriteAllText(ReportPath, report.ToString());
            }
            report.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O"));
            report.AppendLine((failed == 0 ? "PASS" : "FAIL") + " — suites=" + passed + ", failures=" + failed + ", assertions=" + assertions);
            File.WriteAllText(ReportPath, report.ToString());
            if (failed != 0) throw new InvalidOperationException(report.ToString());
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
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch entry requires Unity -batchmode.");
            // A headless editor may start without a scene; plugin build validators restore the saved setup.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Landsong.ECS.Presentation.EcsSceneFlow.Boot);
            RunAll();
        }
    }
}
#endif
