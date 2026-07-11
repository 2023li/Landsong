from pathlib import Path

path = Path("Assets/Landsong/Scripts/QuestSystem/QuestService.cs")
text = path.read_text(encoding="utf-8")
constant = '        private const string GameplayDebugGoldItemId = "金币";'
if constant not in text:
    marker = "    public sealed class QuestService : IDisposable\n    {\n"
    if marker not in text:
        raise RuntimeError("QuestService class declaration marker not found")
    text = text.replace(marker, marker + constant + "\n\n", 1)
    path.write_text(text, encoding="utf-8")
print("QuestService constant patch applied.")
