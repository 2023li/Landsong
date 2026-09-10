# 表现、音频与本地化

统一配置为 Assets/Landsong/ECSContent/Resources/LandsongPresentation.asset。它包含 Models、Portraits、Cues、阶段音乐/环境循环和 Text。纯表现资源在 ECSContent/Presentation；静态建筑分级/皮肤仍在 ECSContent/Prefabs。

- 动态模型只替换外观，不改变 ECS 占地、导航、人口或伤害；缺绑定回退原 ECS 模型。
- 应用级 PresentationRuntime 复用 2 个循环音源与 16 个短音源，不新增 Listener；世界特效最多 32 个。
- 当前接入环境音和 UI 音，部分战斗/建造 Cue 仍为触发占位，四个 Music 槽留空。
- 王室家谱、人物画像、政策卡片读取 ECS 身份；未配置肖像显示姓名色块。
- Text 是当前运行表，支持 zh-Hans / en / 外部文本包。共用 TMP 字体，切语言不改写玩家输入或复制图集。
- 现有 StringTable 是追加文本时的素材来源，日常已有翻译直接编辑 Text。

编辑器菜单 Landsong/ECS/Presentation：

1. Sync language entries (preserve edits)：追加缺失语义 Key / 内容名，保留已编辑翻译。
2. Export language pack template：生成 Library/LandsongEcs/language-template.csv。

[完整表现与语言制作手册](表现与语言制作.md) 包含模型白名单、Animator 参数、音效通道、语言包规范和安全限制。[语言包示例](LanguagePackSample/manifest.json) 与同目录 strings.csv 可作最小样例。

中文是安全回退。英文和复杂动态格式模板尚非全覆盖，正式模型、音效、肖像、配乐及翻译仍需制作；不影响本阶段功能基准收口。
