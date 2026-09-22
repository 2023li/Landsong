using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>编辑示例的明确引用；所属视图创建时清理，不扫描场景，不调用玩法或玩家文件。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Moyo/UI/示例内容绑定")]
    public sealed class UIPreviewOnly : MonoBehaviour
    {
        [SerializeField, LabelText("仅供编辑预览的对象")] private GameObject[] sampleObjects = Array.Empty<GameObject>();
        [SerializeField, LabelText("使用示例内容的文字")] private TMP_Text[] sampleTextTargets = Array.Empty<TMP_Text>();
        [SerializeField, LabelText("运行时初始文字")] private string[] runtimeTexts = Array.Empty<string>();
        public GameObject[] SampleObjects => sampleObjects;
        public TMP_Text[] SampleTextTargets => sampleTextTargets;
        public string[] RuntimeTexts => runtimeTexts;

        public void Configure(GameObject[] objects, TMP_Text[] texts, string[] initialTexts)
        {
            sampleObjects = objects == null ? Array.Empty<GameObject>() : (GameObject[])objects.Clone();
            sampleTextTargets = texts == null ? Array.Empty<TMP_Text>() : (TMP_Text[])texts.Clone();
            runtimeTexts = initialTexts == null ? Array.Empty<string>() : (string[])initialTexts.Clone();
            ValidateConfiguration();
        }

        public void ValidateConfiguration()
        {
            if (sampleObjects == null || sampleTextTargets == null || runtimeTexts == null
                || sampleTextTargets.Length != runtimeTexts.Length)
                throw new InvalidOperationException($"{name} 的示例文字与运行时初始文字必须一一对应。");
            foreach (var sample in sampleObjects)
            {
                // EditorOnly 子对象在 Player 构建中会移除，对应的空引用属于明确的制作契约。
                if (sample == null) continue;
                if (sample == gameObject || !sample.transform.IsChildOf(transform))
                    throw new InvalidOperationException($"{name} 的预览对象必须是自身的子对象，不能禁用正式根。");
            }
            foreach (var text in sampleTextTargets)
                if (text == null || (text.transform != transform && !text.transform.IsChildOf(transform)))
                    throw new InvalidOperationException($"{name} 的示例文字未绑定或不属于该预览范围。");
        }

        public void PrepareRuntime()
        {
            ValidateConfiguration();
            foreach (var sample in sampleObjects) if (sample != null) sample.SetActive(false);
            for (var i = 0; i < sampleTextTargets.Length; i++) sampleTextTargets[i].text = runtimeTexts[i] ?? string.Empty;
        }
    }
}
