from pathlib import Path

quest = Path("Assets/Landsong/Scripts/QuestSystem/QuestService.cs")
quest_text = quest.read_text(encoding="utf-8")
constant = '        private const string GameplayDebugGoldItemId = "金币";'
if constant not in quest_text:
    marker = "    public sealed class QuestService : IDisposable\n    {\n"
    if marker not in quest_text:
        raise RuntimeError("QuestService class declaration marker not found")
    quest_text = quest_text.replace(marker, marker + constant + "\n\n", 1)
    quest.write_text(quest_text, encoding="utf-8")

models = Path("Assets/Landsong/Scripts/Persistence/SaveDataModels.cs")
models_text = models.read_text(encoding="utf-8")
normalized = "\n".join(line.rstrip() for line in models_text.splitlines()).rstrip() + "\n"
if normalized != models_text:
    models.write_text(normalized, encoding="utf-8")

print("Final architecture cleanup applied.")
