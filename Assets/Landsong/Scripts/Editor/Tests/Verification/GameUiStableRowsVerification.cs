#if UNITY_EDITOR
using System;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Editor
{
    public static class GameUiStableRowsVerification
    {
        public static string Run()
        {
            int assertions = 0;
            void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException("稳定条目验证失败：" + message); }
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/Objects/Prefabs/UI/GamePanel/Items/UI_GamePanel_Row.prefab").GetComponent<UI_GamePanel_Row>();
            template.ValidateConfiguration();
            var owner = new GameObject("StableRowVerification", typeof(RectTransform), typeof(UI_GamePanel_RowRenderer));
            var list = new GameObject("Rows", typeof(RectTransform));
            var events = new GameObject("Events", typeof(EventSystem));
            try
            {
                var renderer = owner.GetComponent<UI_GamePanel_RowRenderer>();
                var parent = (RectTransform)list.transform; parent.sizeDelta = new Vector2(500, 600);
                renderer.RowTemplate = template;
                int aClicks = 0, bClicks = 0, replacementClicks = 0;
                UI_GamePanel_Row a = null, b = null;
                void Pass(Action populate)
                {
                    renderer.BeginReconcile(parent);
                    try { populate(); } finally { renderer.EndReconcile(); }
                }
                Pass(() =>
                {
                    a = renderer.Item(template, "士兵 A", () => aClicks++, parent, "soldier:11");
                    b = renderer.Item(template, "士兵 B", () => bClicks++, parent, "soldier:22");
                });
                Pass(() =>
                {
                    Check(renderer.Item(template, "士兵 B · 新状态", () => bClicks++, parent, "soldier:22") == b, "排序改变仍复用 B 本身");
                    Check(renderer.Item(template, "士兵 A", () => aClicks++, parent, "soldier:11") == a, "排序改变仍复用 A 本身");
                });
                Check(a.transform.GetSiblingIndex() > b.transform.GetSiblingIndex(), "未交互时允许排序更新");
                var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 5 };
                var binding = a.Interaction.Bindings[0];
                binding.OnPointerDown(pointer);
                Pass(() =>
                {
                    renderer.Item(template, "士兵 B · 再更新", () => bClicks++, parent, "soldier:22");
                    renderer.Item(template, "新士兵 C", () => replacementClicks++, parent, "soldier:33");
                });
                Check(a.gameObject.activeSelf && a.Identity == "domain:soldier:11", "删除候选也保留已按下对象的原身份");
                Check(b.Label.text == "士兵 B · 再更新", "其他条目继续刷新");
                Pass(() => renderer.Item(template, "A 新报价", () => replacementClicks++, parent, "soldier:11"));
                Check(a.Label.text == "士兵 A", "按下期间不换显示内容");
                a.Select.onClick.Invoke();
                Check(aClicks == 1 && replacementClicks == 0, "按下期间仍执行原动作");
                var revision = UI_GamePanel_InteractionLock.Revision;
                binding.OnPointerUp(pointer);
                Check(!a.CanRebind && UI_GamePanel_InteractionLock.Revision != revision, "释放当帧保留点击快照并使延后列表失效");
                Pass(() => renderer.Item(template, "释放当帧的新动作", () => replacementClicks++, parent, "soldier:11"));
                a.Select.onClick.Invoke();
                Check(aClicks == 2 && replacementClicks == 0, "pointer-up 与 click 之间不能改回调");
                list.SetActive(false);
                Pass(() => Check(renderer.Item(template, "隐藏列表", null, parent, "hidden") == null, "隐藏容器不创建或重绑条目"));
                Check(a.Label.text == "士兵 A", "隐藏容器保留原展示状态");
                UnityEngine.Object.DestroyImmediate(list); list = null;
                renderer.ClearAll(); renderer.ClearAll();
                Check(renderer.ContainerCount == 0, "宿主先释放后仍可幂等清空全部池");
                return "GameUiStableRows Assertions: " + assertions + " passed";
            }
            finally
            {
                if (list != null) UnityEngine.Object.DestroyImmediate(list);
                UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(events);
            }
        }
    }
}
#endif
