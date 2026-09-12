#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Landsong.ECS.Presentation;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    // Reads legacy serialized values before Unity can discard a changed field type.
    // This editor migration is the only place which understands former display-text IDs.
    public static class GamePanelNavigationMigration
    {
        const string Folder = "Assets/Landsong/Objects/Prefabs/UI/GamePanel";
        const string Evidence = "Library/LandsongEcs/NavigationMigration";
        static readonly Dictionary<string, GamePanelId> Legacy = new()
        {
            ["建筑"] = GamePanelId.Building, ["科技"] = GamePanelId.Technology, ["任务"] = GamePanelId.Quest,
            ["经济"] = GamePanelId.Economy, ["库存"] = GamePanelId.Inventory, ["驻军"] = GamePanelId.Garrison,
            ["王室"] = GamePanelId.Royal, ["人才"] = GamePanelId.Talent, ["政策"] = GamePanelId.Policy,
            ["远征"] = GamePanelId.Expedition, ["历史"] = GamePanelId.History, ["情报"] = GamePanelId.Intelligence,
            ["战报"] = GamePanelId.BattleReport, ["入夜确认"] = GamePanelId.NightConfirmation,
            ["王朝终局"] = GamePanelId.DynastyEnd, ["存档"] = GamePanelId.Pause
        };
        static GamePanelId Parse(string serialized)
        {
            string value = Regex.Unescape(serialized.Trim().Trim('"'));
            if (int.TryParse(value, out int numeric) && Enum.IsDefined(typeof(GamePanelId), numeric) && numeric != 0)
                return (GamePanelId)numeric;
            if (Legacy.TryGetValue(value, out var id)) return id;
            throw new InvalidOperationException("未知或已丢失的旧面板标识：" + serialized);
        }

        [MenuItem("Landsong/ECS/Migration/Type game panel navigation")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请退出运行并等待编译完成后迁移导航。");
            if (!Directory.Exists(Folder)) throw new InvalidOperationException("游戏面板目录不存在。");
            var changes = new Dictionary<string, (string Before, string After)>();
            int panels = 0, events = 0;
            foreach (string path in Directory.GetFiles(Folder, "*.prefab", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                string result = Regex.Replace(source, @"(?m)^  PanelId: ([^\r\n]+)\r?$", match =>
                {
                    var id = Parse(match.Groups[1].Value); panels++;
                    // Existing typed assets keep authored permission configuration on repeat runs.
                    if (int.TryParse(match.Groups[1].Value.Trim(), out _)) return match.Value;
                    string feature = id == GamePanelId.Building ? "feature.Building" : id == GamePanelId.Inventory ? "feature.Inventory"
                        : id == GamePanelId.Expedition ? "feature.Expedition" : id == GamePanelId.Technology ? "feature.Technology" : "";
                    return "  PanelId: " + (int)id + "\n  RequiredFeatureId: " + feature
                        + "\n  AllowMissingFeature: " + (id == GamePanelId.Technology ? 1 : 0)
                        + "\n  AllowLockedOpen: " + (id == GamePanelId.Building ? 1 : 0);
                });
                result = Regex.Replace(result, @"(?m)^  Panel: ([^\r\n]+)\r?$", match => "  Panel: " + (int)Parse(match.Groups[1].Value));
                result = Regex.Replace(result, @"(?m)^(\s*)m_MethodName: OpenPanel\r?\n(?<body>[\s\S]*?m_StringArgument: )(?<value>[^\r\n]*)", match =>
                {
                    string body = match.Groups["body"].Value;
                    if (!Regex.IsMatch(body, @"m_Mode: 5\b")) throw new InvalidOperationException(path + " 的旧导航事件不是静态字符串模式。");
                    var id = Parse(match.Groups["value"].Value); events++;
                    body = Regex.Replace(body, @"m_Mode: 5\b", "m_Mode: 3");
                    body = Regex.Replace(body, @"m_IntArgument: -?\d+", "m_IntArgument: " + (int)id);
                    return match.Groups[1].Value + "m_MethodName: OpenPanelFromEvent\n" + body + "\"\"";
                });
                if (Path.GetFileName(path) == "UI_GamePanel.prefab" && !result.Contains("  NavigationButtons:"))
                {
                    var buttons = new[] { (Field: "BuildingFeatureButton", Id: GamePanelId.Building), (Field: "InventoryFeatureButton", Id: GamePanelId.Inventory), (Field: "ExpeditionFeatureButton", Id: GamePanelId.Expedition) };
                    var configured = new StringBuilder("  NavigationButtons:\n");
                    foreach (var button in buttons)
                    {
                        var field = Regex.Match(result, @"(?m)^  " + button.Field + @": (\{fileID: -?\d+\})\r?$");
                        if (!field.Success || field.Groups[1].Value == "{fileID: 0}") throw new InvalidOperationException("旧功能按钮引用缺失：" + button.Field);
                        configured.Append("  - Target: ").Append((int)button.Id).Append("\n    Button: ").Append(field.Groups[1].Value).Append('\n');
                    }
                    result = Regex.Replace(result, @"(?m)^  BuildingFeatureButton: [^\r\n]*\r?\n", _ => configured.ToString());
                    result = Regex.Replace(result, @"(?m)^  (?:InventoryFeatureButton|ExpeditionFeatureButton): [^\r\n]*\r?\n", "");
                }
                if (source != result) changes.Add(path, (source, result));
            }
            if (panels == 0) throw new InvalidOperationException("未发现可迁移的游戏子面板。");
            // Complete preflight before touching any asset. Preserve original bytes for review.
            Directory.CreateDirectory(Evidence);
            foreach (var item in changes)
            {
                string backup = Path.Combine(Evidence, item.Key.Replace('\\', '/').Substring(Folder.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.WriteAllText(backup, item.Value.Before, new UTF8Encoding(false));
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var item in changes) File.WriteAllText(item.Key, item.Value.After, new UTF8Encoding(false));
                foreach (string path in changes.Keys) AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            string eventReport = GamePanelEventMigration.Run();
            string report = $"导航迁移完成：{panels} 个面板，{events} 个原事件，{changes.Count} 个资源。保留原事件参数与调用状态。" + eventReport;
            File.WriteAllText(Path.Combine(Evidence, "result.txt"), DateTimeOffset.Now.ToString("O") + "\n" + report);
            return report;
        }
    }
}
#endif
