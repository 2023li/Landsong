from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "Landsong" / "Scripts"

GAME_SYSTEM_FILES = [
    SCRIPTS / "GameSystem" / "GameSystem.cs",
    SCRIPTS / "GameSystem" / "GameSystem.TurnState.cs",
    SCRIPTS / "GameSystem" / "GameServices.cs",
    SCRIPTS / "QuestSystem" / "GameSystem.Quest.cs",
]
DATA_MANAGER_FILE = SCRIPTS / "AppSystem" / "DataManager.cs"

PUBLIC_MEMBER = re.compile(
    r"^\s*public\s+(?:(?:static|sealed|virtual|override|readonly|async|partial)\s+)*"
    r"(?P<type>[A-Za-z0-9_<>,\.\[\]\?]+)\s+"
    r"(?P<name>[A-Za-z_\u0080-\uffff][A-Za-z0-9_\u0080-\uffff]*)\s*"
    r"(?P<tail>\(|=>|\{|;)",
    re.MULTILINE,
)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def public_members(path: Path) -> list[tuple[str, str, str]]:
    text = read(path)
    members: list[tuple[str, str, str]] = []
    for match in PUBLIC_MEMBER.finditer(text):
        line = text.count("\n", 0, match.start()) + 1
        members.append((match.group("name"), match.group("tail"), f"{path.relative_to(ROOT)}:{line}"))
    return members


def find_usages(name: str, excluded: set[Path]) -> list[str]:
    patterns = [
        re.compile(rf"\bGameSystem\.Instance\s*\.\s*{re.escape(name)}\b"),
        re.compile(rf"\bgameSystem\s*\.\s*{re.escape(name)}\b"),
        re.compile(rf"\bcontext\s*\.\s*{re.escape(name)}\b"),
        re.compile(rf"\bsourceGameSystem\s*\.\s*{re.escape(name)}\b"),
    ]
    result: list[str] = []
    for path in SCRIPTS.rglob("*.cs"):
        if path in excluded:
            continue
        lines = read(path).splitlines()
        for index, line in enumerate(lines, 1):
            if any(pattern.search(line) for pattern in patterns):
                result.append(f"{path.relative_to(ROOT)}:{index}: {line.strip()}")
    return result


def main() -> None:
    print("=== GameSystem public members ===")
    game_members: dict[str, list[str]] = {}
    for path in GAME_SYSTEM_FILES:
        if not path.exists():
            continue
        print(f"\n# {path.relative_to(ROOT)}")
        for name, tail, location in public_members(path):
            game_members.setdefault(name, []).append(location)
            print(f"{name:42} {tail:2} {location}")

    print("\n=== DataManager public members ===")
    if DATA_MANAGER_FILE.exists():
        for name, tail, location in public_members(DATA_MANAGER_FILE):
            print(f"{name:42} {tail:2} {location}")

    print("\n=== External usages of GameSystem public members ===")
    excluded = set(GAME_SYSTEM_FILES)
    for name in sorted(game_members):
        usages = find_usages(name, excluded)
        if not usages:
            continue
        print(f"\n## {name} ({len(usages)})")
        for usage in usages[:80]:
            print(usage)
        if len(usages) > 80:
            print(f"... {len(usages) - 80} more")

    print("\n=== Duplicate DataManager save aliases ===")
    for name in (
        "SaveGameData",
        "OverwriteSaveGameData",
        "SaveNewGameData",
        "QuickSaveGameData",
        "AutoSaveGameData",
        "GetAllGameDataMeta",
    ):
        print(f"\n## {name}")
        for path in SCRIPTS.rglob("*.cs"):
            if path == DATA_MANAGER_FILE:
                continue
            for index, line in enumerate(read(path).splitlines(), 1):
                if re.search(rf"\b{name}\s*\(", line):
                    print(f"{path.relative_to(ROOT)}:{index}: {line.strip()}")


if __name__ == "__main__":
    main()
