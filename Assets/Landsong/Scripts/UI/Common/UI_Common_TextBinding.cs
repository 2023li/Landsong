using System;
using TMPro;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_Common_TextBinding : MonoBehaviour, ITextPreprocessor
    {
        [LabelText("文字目标"), Required]
        public TMP_Text Target;
        [LabelText("按钮文字")]
        public bool ButtonLabel;
        [LabelText("本地化语义键")]
        public string Key;
        int revision = -1;
        bool originalAuto;
        float originalSize, originalMin, originalMax;
        public void ValidateConfiguration()
        {
            if (Target == null)
                throw new InvalidOperationException(name + " 的多语言文本目标未在检查器中配置。");
            if (Target.gameObject != gameObject)
                throw new InvalidOperationException(name + " 的多语言文本目标必须是当前对象上的文本组件。");
        }

        void Awake()
        {
            ValidateConfiguration();
            Target.textPreprocessor = this;
            originalAuto = Target.enableAutoSizing;
            originalSize = Target.fontSize;
            originalMin = Target.fontSizeMin;
            originalMax = Target.fontSizeMax;
        }

        public string PreprocessText(string text) => string.IsNullOrEmpty(Key) ? PresentationText.Source(text) : PresentationText.Get(Key, text);
        void LateUpdate()
        {
            if (revision == PresentationText.Revision)
                return;
            revision = PresentationText.Revision;
            if (ButtonLabel)
            {
                bool translated = PresentationText.Language != "zh-Hans";
                Target.enableAutoSizing = translated || originalAuto;
                Target.fontSizeMin = translated ? Mathf.Max(10, originalSize * .55f) : originalMin;
                Target.fontSizeMax = translated ? originalSize : originalMax;
                if (!translated)
                    Target.fontSize = originalSize;
            }

            Target.SetLayoutDirty();
            Target.ForceMeshUpdate(false, true);
        }

        void OnDestroy()
        {
            if (Target != null && ReferenceEquals(Target.textPreprocessor, this))
                Target.textPreprocessor = null;
        }
    }
}
