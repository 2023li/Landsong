# 音频与本地化

## 1. 音频架构

`AudioPlayer` 是唯一运行时音频播放入口，负责通道、音量组、Cue、单实例淡入淡出和 OneShot 对象池。常用入口：

```csharp
AudioPlayer.Instance.PlayBgm(clip);
AudioPlayer.Instance.PlayAmbience(clip);
AudioPlayer.Instance.PlaySfx(cueKey);
AudioPlayer.Instance.PlayUiClick();
```

音量和静音值由 `DataManager.AppData.Audio` 持久化，`AudioPlayer` 订阅 `OnAudioSettingsChanged` 后应用到通道。UI 设置面板只调用 `DataManager.Set*Volume` / `SetMuted`。

### 通道与 Cue

- `AudioChannelDefinition`：通道 Key、单实例/OneShot 模式、音量组、对象池和默认淡变时间。
- `AudioCueDefinition`：Cue Key、Clip/Variants、音量、空间混合和音高范围。
- 业务代码优先使用稳定 Cue Key；少量对象专属声音可直接传 AudioClip。
- `AudioButtonSfx` 是按钮点击适配器，不在每个 Button 脚本重复播放逻辑。

`GameAudioDirector` 只负责 Game Scene 的环境/开场音频编排，不成为第二个 AudioPlayer。

## 2. 本地化架构

`GameLocalizationManager` 是本地化生命周期与语言选择门面，具体职责已经拆分到 `Landsong.Localization`：

- 内置 Unity Localization Locale/StringTable；
- 系统语言与用户选择；
- 外部目录语言包 V1 的 manifest/CSV 读取、限制与诊断；
- 任意合法 Locale 的运行时 StringTable、内置语言 fallback；
- 语言切换事件和 AppData 持久化。

正式表分为 `UI`、`Content`、`Gameplay`。内置 `zh-Hans` 与 `en` 必须 Key 对齐且非空；外部语言包允许不完整并回退到 `en` 或 `zh-Hans`。详细格式见 [玩家自定义语言包制作说明](玩家自定义语言包制作说明.md)。

重构的架构决策、A–E 阶段交付范围和最终验证结果见 [本地化重构设计](本地化重构设计.md)。编辑器菜单 `Landsong/Localization/Run Release Validation` 会检查中英表完整性、Smart String 参数、Prefab/Scene 固定文本绑定、运行时代码硬编码回流和示例语言包。

外部语言包目录由 `IOManager.ExternalLanguagePacksFolderPath` 提供。不要在 UI 中直接读文件或自行切换 Locale。外部包只允许文本，不能携带代码或资源。

## 3. 设置数据流

```text
Setting Panel
  -> DataManager 修改 AppData
  -> 保存 AppData
  -> OnAudioSettingsChanged / OnLanguageChanged
  -> AudioPlayer / Unity Localization / UI 自动刷新
```

新增设置时沿用 `DataManager` 的更新、保存和事件模式，不能只改当前面板状态。

## 4. 关键代码

- `Assets/Landsong/Scripts/AudioSystem/AudioPlayer.cs`
- `Assets/Landsong/Scripts/AudioSystem/AudioButtonSfx.cs`
- `Assets/Landsong/Scripts/AudioSystem/GameAudioDirector.cs`
- `Assets/Landsong/Scripts/AppSystem/GameLocalizationManager.cs`
- `Assets/Landsong/Scripts/AppSystem/Localization/L10n.cs`
- `Assets/Landsong/Scripts/AppSystem/Localization/ExternalLanguagePackRepository.cs`
- `Assets/Landsong/Scripts/AppSystem/Localization/RuntimeStringTableProvider.cs`
- `Assets/Landsong/Scripts/AppSystem/DataManager.cs`
- `Assets/Landsong/Scripts/AppSystem/IOManager.cs`
- `Assets/Landsong/Scripts/UI/UIPanel_Setting`

## 5. 扩展规则

- 新音效先判断是否应成为复用 Cue；不要散落 Resources.Load 或临时 AudioSource。
- 新音量类型必须同时定义 AppData 字段、DataManager 更新 API、AudioPlayer 应用规则和设置 UI。
- 外部语言包只覆盖文本，不允许携带脚本或任意资源路径。
- 新本地化文本使用正式 StringTable Key；显示文本不是逻辑 ID。
- 语言切换后需要刷新动态拼接文本的面板应订阅语言事件并重新格式化。

## 6. 验证清单

- 重启后主音量、BGM、环境音、SFX 和静音状态一致。
- 多次切换场景不会创建重复 AudioPlayer 或叠加 BGM。
- OneShot 池达到上限时行为可控，没有无限创建 AudioSource。
- 内置语言、系统语言和外部目录语言包均可切换。
- 外部 CSV 缺列、重复 Key 或无效元数据时有明确错误，不破坏内置表。
- 切换语言后 Prefab 中 LocalizeStringEvent 与运行时动态文本都刷新。
- `Run Release Validation` 与 `Run Automated Tests` 全部通过后才允许发布。

