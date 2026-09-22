# 2026-09-20 定义迁移历史证据

这些文件保存领域定义重构时的精确内容对照，已退出日常验证。它们不是当前内容的标准答案，也不是日常制作工具。

- `DomainDefinitionBaseline.json`：原始 185 项迁移基线，字节保持不变。SHA-256：`7f313b181598c2cf6aeb37c6a97d7ae9330538c23954eeefb501d009747f59ce`。
- `DomainDefinitionBaseline.json.meta.txt`：原 Unity 导入标识记录；当前不需要导入此历史 JSON。
- `ContentCompilationVerification.cs.txt`、`BuildingModuleVerification.cs.txt`：分离前的验证源码，保留迁移对照方法与当时假设，不参与 Unity 编译。

基线中的 `BaselineCommit` 指向迁移前的来源。要复核原迁移结果，在独立检出中使用提交 `10acdb98c26a7e065c95adbd35a1660fa887f8df` 的工程和对应 Unity 版本执行当时的验证；该提交仍保存原始资产、原路径和验证入口。不要将归档源码重新安装到当前项目，也不要为新增内容改写此 JSON。

当前日常入口仍为 `ProjectVerification.RunAll` / `ValidateBatch`，其中 `ContentCompilationVerification` 按当前显式目录验证数量、身份、顺序、序列化与编译，并使用临时内容集验证新增、调参和非法输入。建筑模块回归也不再读取历史基线。内容指纹和存档版本规则没有改变。
