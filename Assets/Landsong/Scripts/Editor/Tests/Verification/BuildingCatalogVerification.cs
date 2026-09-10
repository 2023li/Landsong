#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingCatalogVerification
    {
        public static string Run()
        {
            var log = new StringBuilder(); int count = 0;
            void Check(bool valid, string label) { if (!valid) throw new InvalidOperationException("FAIL " + label); count++; log.AppendLine("PASS " + label); }
            foreach (var path in new[] { EcsSceneFlow.Boot, EcsSceneFlow.Menu, EcsSceneFlow.Loading, EcsSceneFlow.Game })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    Check(!roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.UI.Text>(true)).Any(), path + " has no legacy Text");
                    Check(!roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.UI.InputField>(true)).Any(), path + " has no legacy InputField");
                    Check(!roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.UI.Dropdown>(true)).Any(), path + " has no legacy Dropdown");
                    var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
                    Check(texts.Length > 0 && texts.All(t => t.font != null && t.fontSharedMaterial != null && t.fontSize > 0), path + " TMP font/material/size valid");
                    Check(roots.SelectMany(r => r.GetComponentsInChildren<TMP_InputField>(true)).All(i => i.textComponent != null && i.textViewport != null), path + " TMP input references preserved");
                    Check(roots.SelectMany(r => r.GetComponentsInChildren<TMP_Dropdown>(true)).All(d => d.captionText != null && d.itemText != null && d.template != null), path + " TMP dropdown references preserved");
                    if (path == EcsSceneFlow.Game)
                    {
                        var view = roots.SelectMany(r => r.GetComponentsInChildren<EcsGameView>(true)).Single(); var bar = view.BuildingBar;
                        Check(bar != null && !bar.gameObject.activeSelf && bar.CardScroll.horizontal && !bar.CardScroll.vertical, "Scene authored bottom tray starts closed and scrolls horizontally");
                        Check(bar.TabTemplate != null && bar.CardTemplate != null && bar.TooltipText != null && !bar.Tooltip.gameObject.activeSelf, "Editable tab/icon/tooltip templates are wired");
                        Check(view.Status != null && view.Selection != null && view.Message != null && view.NameInput.textComponent != null && view.PauseMenu.Status != null, "Game and pause serialized TMP references preserved");
                        Check(!roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "Building Catalog Filters"), "Obsolete building filter removed");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            log.AppendLine("Assertions: " + count); Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/building-catalog-verification.txt", log.ToString()); return log.ToString();
        }
    }
}
#endif
