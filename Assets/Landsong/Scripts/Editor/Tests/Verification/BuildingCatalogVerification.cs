#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingCatalogVerification
    {
        public static string Run()
        {
            var log = new StringBuilder(); int count = 0;
            void Check(bool valid, string label) { if (!valid) throw new InvalidOperationException("FAIL " + label); count++; log.AppendLine("PASS " + label); }
            foreach (var path in new[] { ApplicationUiMigration.BootPath, ApplicationUiMigration.StartPath, ApplicationUiMigration.LoadingPath, ApplicationUiMigration.SettingPath, ApplicationUiMigration.SavePath, ApplicationUiMigration.ConfirmPath, ApplicationUiMigration.GamePath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null, path + " exists");
                Check(!prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(), path + " has no legacy Text");
                Check(!prefab.GetComponentsInChildren<UnityEngine.UI.InputField>(true).Any(), path + " has no legacy InputField");
                Check(!prefab.GetComponentsInChildren<UnityEngine.UI.Dropdown>(true).Any(), path + " has no legacy Dropdown");
                var texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                Check(texts.Length > 0 && texts.All(t => t.font != null && t.fontSharedMaterial != null && t.fontSize > 0), path + " TMP font/material/size valid");
                Check(prefab.GetComponentsInChildren<TMP_InputField>(true).All(i => i.textComponent != null && i.textViewport != null), path + " TMP input references preserved");
                Check(prefab.GetComponentsInChildren<TMP_Dropdown>(true).All(d => d.captionText != null && d.itemText != null && d.template != null), path + " TMP dropdown references preserved");
                if (path == ApplicationUiMigration.GamePath)
                {
                    var view = prefab.GetComponent<UI_GamePanel>();
                    Check(view != null && view.Buildings != null && view.Hud != null, "Game panel and domain controllers configured");
                    var bar = view.Buildings.BuildingBar;
                    Check(bar != null && bar.CardScroll.horizontal && !bar.CardScroll.vertical, "Authored bottom tray scrolls horizontally");
                    Check(bar.TabTemplate != null && bar.CardTemplate != null && bar.TooltipText != null, "Editable tab/icon/tooltip templates are wired");
                    Check(view.Hud.Status != null && view.Hud.Selection != null && view.Hud.Message != null && view.Buildings.NameInput.textComponent != null && view.PauseMenu.Status != null, "Game and pause serialized TMP references preserved");
                    Check(!prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Building Catalog Filters"), "Obsolete building filter removed");
                }
            }
            log.AppendLine("Assertions: " + count); Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/building-catalog-verification.txt", log.ToString()); return log.ToString();
        }
    }
}
#endif
