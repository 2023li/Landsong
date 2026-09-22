#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Persistence;
using Landsong.EditorTools;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    public static class DocumentationVerification
    {
        [MenuItem("Landsong/ECS/Verification/Current authoring documentation")]
        public static string Run()
        {
            var report = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            int checks = 0;
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
                checks++;
                report.AppendLine("PASS " + message);
            }
            try
            {
                foreach (var document in Directory.EnumerateFiles("Document", "*.md", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(document);
                    foreach (Match match in Regex.Matches(source, @"(?<!!)\[[^\]]*\]\(([^)]+)\)"))
                    {
                        string link = match.Groups[1].Value.Trim('<', '>');
                        if (Regex.IsMatch(link, @"^(https?:|#|mailto:|codex:)")) continue;
                        string path = Regex.Replace(link.Split('#')[0], @":\d+$", "");
                        path = Uri.UnescapeDataString(path);
                        string target = Path.IsPathRooted(path) ? path : Path.Combine(Path.GetDirectoryName(document), path);
                        Check(File.Exists(target) || Directory.Exists(target), document + " link resolves: " + link);
                    }
                }
                foreach (var path in new[] { "Document/README.md", "Document/定义系统.md", "Document/开发规范.md", "Document/架构决策.md", "Document/运行时与存档/README.md", "Document/ECS/当前限制.md" })
                {
                    var text = File.ReadAllText(path);
                    Check(text.Contains("v" + SnapshotCodec.CurrentVersion), path + " documents the current snapshot version");
                    Check(!Regex.IsMatch(text, @"\bv(?:28|30)\b"), path + " has no superseded current-version claim");
                }
                string building = File.ReadAllText("Document/ECS/建筑玩法与制作工作流.md");
                string unit = File.ReadAllText("Document/昼夜与战斗系统/单位制作流程.md");
                string technology = File.ReadAllText("Document/科技系统/README.md");
                int modules = typeof(BuildingCapabilitiesSource).GetFields(BindingFlags.Instance | BindingFlags.Public).Length;
                Check(building.Contains(modules + " 个具名") && building.Contains(nameof(BuildingDefinitionAsset.Footprint)) && building.Contains("SelectionAnchor"), "Building SOP matches current modules, footprint and selection contract");
                foreach (string member in new[] { nameof(TechnologyDefinitionAsset.ResearchPointCost), nameof(TechnologyDefinitionAsset.Repeatable), nameof(TechnologyDefinitionAsset.HasTreePosition), nameof(TechnologyDefinitionAsset.TreePosition) })
                    Check(technology.Contains(member), "Technology SOP documents current field " + member);
                Check(!Regex.IsMatch(technology, @"ResearchEntry|CommandRequests|HasTechnologyPosition|\bFlags\b"), "Technology SOP contains no removed field or request API");
                void Menu(Type owner, string method, string text)
                {
                    var item = owner.GetMethod(method, BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null).GetCustomAttribute<MenuItem>();
                    Check(item != null && text.Contains(item.menuItem.Replace("/", " → ")), "Documented menu matches " + owner.Name + "." + method);
                }
                Menu(typeof(BuildingCreationWindow), nameof(BuildingCreationWindow.Open), building);
                Menu(typeof(UnitCreationWindow), nameof(UnitCreationWindow.Open), unit);
                Menu(typeof(TechnologyEditorWindow), nameof(TechnologyEditorWindow.Open), technology);
                Menu(typeof(ContentAuthoringValidation), nameof(ContentAuthoringValidation.Validate), unit);
                Check(File.Exists("Document/ECS/新增建筑能力开发流程.md"), "Code extension recipe has a maintained entry point");
                report.AppendLine("Assertions: " + checks);
                return report.ToString();
            }
            catch (Exception error) { report.AppendLine("FAIL " + error); throw; }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/documentation-verification.txt", report.ToString());
            }
        }
    }
}
#endif
