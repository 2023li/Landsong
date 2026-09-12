# 2026-09-11 重构验收证据

本轮按用户最后确认的范围，仅执行 Unity 编辑器检查与实际 Play 测试，不执行构建或独立 Player。实施范围、问题闭环及兼容边界见 [重构执行记录](../../../重构执行记录.md)。

| 项目 | 结果与原始记录 |
| --- | --- |
| 最终全量回归 | [regressions.txt](regressions.txt)：35 套件，0 失败，11145 条断言 |
| UI 配置 | [ui-configuration-verification.txt](ui-configuration-verification.txt)：6946 条，含引用、层级、中文标签、布局、预览清理与编译调用检查 |
| 程序集边界 | [architecture-verification.txt](architecture-verification.txt)：168 条 |
| 持久化契约 / 存档服务 | [持久化](persistence-contract-verification.txt) 105 条；[存档服务](archive-application-service-verification.txt) 51 条 |
| Moyo 生命周期 | [ui-framework-verification.txt](ui-framework-verification.txt)：21 项 |
| Odin 实际绘制 | [content-inspector-gui.txt](content-inspector-gui.txt)：展开嵌套内容配置后实际绘制通过，不等于所有 Inspector 组合逐项人工验收 |
| 完整四场景 Play | [scene-flow-verification.txt](scene-flow-verification.txt)：完整玩法/UI 流程、共享根复用、冷恢复、重复进入、取消及失败后清理 |
| 界面专项 Play | [interface-ui-verification.txt](interface-ui-verification.txt)：暂停、镜头、输入、设置及存档管理 |
| HUD / 功能窗口专项 Play | [hud-panels-verification.txt](hud-panels-verification.txt)：科技、任务、建筑、驻军、王室、人物请求与塑容 |
| 驻军专项 Play | [garrison-ui-verification.txt](garrison-ui-verification.txt)：招募、分配、排序、改名、详情肖像与关闭释放 |

各报告记录自身实际时间；[manifest.json](manifest.json) 保存报告原路径、时间与 SHA-256，以及归档时编辑器程序集指纹。程序集指纹描述最终编辑器状态，不冒充每个较早报告运行时的指纹。共用运行验证器名为 `EcsPlayerSmoke`，本轮由 Editor Play 调用，类名和日志前缀不代表已执行独立 Player。

预览共 29 张，涵盖 1920×1080、1280×720、150% UI 缩放和 2560×1080；完整图片保存在 `Library/LandsongEcs/UiPreviews/`。本目录保存两张与最后布局修正有关的代表图；正式样例已写入预制体，可通过 Profile / Recipe 继续编辑。

![赐婚弹窗在 1280×720、150% 缩放下的编辑示例](previews/marriage-1280-scale150.png)

![士兵详情在 1280×720、150% 缩放下的编辑示例](previews/soldier-1280-scale150.png)

完整场景验收地图为 `Map_Test2`；`Map_Test01` 保留在菜单配置中，本轮不声称穷举所有地图、内容与玩法组合。清理恢复的 26 条状态回归和真实取消/错误场景测试，不代表所有原生场景故障都已注入。规模风险的 Editor 微基准另见 [性能基线](../../性能基线/README.md)，本轮未作 Player 帧率承诺。
