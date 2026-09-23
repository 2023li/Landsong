#if UNITY_EDITOR
using Landsong.ECS.Presentation;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor.Tests
{
    [TestFixture]
    [Category("Landsong")]
    [Category("界面")]
    public sealed class DebugPanelEditModeTests
    {
        [Test]
        public void 非游戏世界天气按钮禁用且无法打开天气页()
        {
            Assert.That(UI_DebugPanel_Weather.HasGameWorld, Is.False);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Landsong/UI/Prefabs/DebugPanel/UI_DebugPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<UI_DebugPanel>();
                var weatherButton = instance.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "btn_天气");
                var weatherPage = instance.GetComponentInChildren<UI_DebugPanel_Weather>(true);
                Assert.That(panel, Is.Not.Null);
                Assert.That(weatherButton, Is.Not.Null);
                Assert.That(weatherPage, Is.Not.Null);

                panel.OnOpenAsync(null).GetAwaiter().GetResult();
                Assert.That(weatherButton.interactable, Is.False);
                Assert.That(weatherPage.gameObject.activeSelf, Is.False);

                panel.OpenWeather();
                Assert.That(weatherButton.interactable, Is.False);
                Assert.That(weatherPage.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
#endif
