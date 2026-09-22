using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [DisallowMultipleComponent]
    public sealed class UI_SavePanel_ArchiveRow : MonoBehaviour
    {
        [LabelText("选择按钮"), Required]
        public Button Select;
        [LabelText("存档摘要"), Required]
        public TextMeshProUGUI Label;
        [LabelText("条目布局"), Required]
        public LayoutElement Layout;
    }
}
