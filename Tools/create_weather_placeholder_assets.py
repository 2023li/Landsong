"""Create the two intentionally temporary weather buildings from known valid Unity YAML.

Run once from the repository root. The fixed GUIDs keep references stable and the
script refuses to replace an existing authored asset.
"""

from pathlib import Path
import json
import re


ROOT = Path("Assets/Landsong/ECSContent")
BUILDINGS = ROOT / "Buildings"
DEFINITIONS = ROOT / "Definitions/Building"
POLICE_DEFINITION = DEFINITIONS / "b警局.asset"
POLICE_PREFAB = BUILDINGS / "b警局/b警局.prefab"
POLICE_DEFINITION_GUID = "7cc7729af4b8fb044bd897faf009b2b3"

ITEM_GOLD = "87aa6f169865b1d46b0bfbbf80f03e95"


def escaped(value: str) -> str:
    return re.sub(r"\\u[0-9a-f]{4}", lambda match: "\\u" + match.group(0)[2:].upper(), json.dumps(value, ensure_ascii=True))


def section(value: str, start: str, end: str, replacement: str) -> str:
    before, tail = value.split(start, 1)
    _, after = tail.split(end, 1)
    return before + start + replacement + end + after


def line(value: str, key: str, replacement: str) -> str:
    result, count = re.subn(rf"(?m)^(\s*{re.escape(key)}:).*$", lambda match: match.group(1) + " " + replacement, value, count=1)
    assert count == 1, key
    return result


def meta(path: Path, guid: str) -> None:
    suffix = "PrefabImporter" if path.suffix == ".prefab" else "NativeFormatImporter"
    file_id = "  mainObjectFileID: 11400000\n" if path.suffix == ".asset" else ""
    path.with_name(path.name + ".meta").write_text(
        f"fileFormatVersion: 2\nguid: {guid}\n{suffix}:\n  externalObjects: {{}}\n{file_id}  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8",
    )


def create_building(name: str, definition_guid: str, prefab_guid: str, definition: str) -> None:
    target_dir = BUILDINGS / name
    target_definition = DEFINITIONS / f"{name}.asset"
    target_prefab = target_dir / f"{name}.prefab"
    if target_definition.exists() or target_prefab.exists():
        raise RuntimeError(f"Refusing to replace authored content: {name}")
    target_dir.mkdir(parents=True, exist_ok=True)
    prefab = POLICE_PREFAB.read_text(encoding="utf-8")
    prefab = prefab.replace(escaped("b警局"), escaped(name))
    prefab = prefab.replace(POLICE_DEFINITION_GUID, definition_guid)
    target_definition.write_text(definition, encoding="utf-8")
    target_prefab.write_text(prefab, encoding="utf-8")
    meta(target_definition, definition_guid)
    meta(target_prefab, prefab_guid)


def fire_station() -> str:
    result = POLICE_DEFINITION.read_text(encoding="utf-8")
    result = result.replace(escaped("b警局"), escaped("b消防站"))
    result = line(result, "Name", escaped("消防站"))
    result = line(result, "Description", escaped("消防员从在岗工人中派出，沿道路到达着火建筑后灭火。"))
    result = line(result, "LimitGroup", "{fileID: 0}")
    result = line(result, "MaximumLevel", "1")
    result = line(result, "ResourceConnectionActionPower", "30")
    result = line(result, "MenuOrder", "37")
    result = section(result, "    Upgrade:\n", "    Maintenance:\n", "      Enabled: 0\n      Costs: []\n      Workers: []\n      Residents: []\n      MaintenanceRequirements: []\n      Experience: []\n")
    result = section(result, "    Maintenance:\n", "    Production:\n", "      Enabled: 0\n      RepairTurns: 0\n      Costs: []\n      Repairs: []\n")
    workforce = f"""      Enabled: 1
      Levels:
      - Level: 1
        Currency: {{fileID: 11400000, guid: {ITEM_GOLD}, type: 2}}
        Capacity: 4
        InitialWorkers: 0
        InitialSubsidy: 0
        BaseAttraction: 55
        RecruitmentCost: 10
      EfficiencyTiers:
"""
    for workers in range(5):
        workforce += f"      - Level: 1\n        MinimumWorkers: {workers}\n        MaximumWorkers: {workers}\n"
    workforce += "      Attraction: []\n"
    result = section(result, "    Workforce:\n", "    Housing:\n", workforce)
    result = section(result, "    Effects:\n", "    Defence:\n", "      Enabled: 0\n      Spatial: []\n")
    result = re.sub(r"(?m)^  Prefab:.*$", "  Prefab: {fileID: 5298753589461577345, guid: 688a0f5e594443f8a17c1b9966c23b6d, type: 3}", result)
    return result


def thunder_temple() -> str:
    result = (DEFINITIONS / "b雕塑.asset").read_text(encoding="utf-8")
    result = result.replace(escaped("b雕塑"), escaped("b雷神殿"))
    result = line(result, "Name", escaped("雷神殿"))
    result = line(result, "Description", escaped("落雷幸存后解锁的占位建筑；暂不提供功能。"))
    result = line(result, "LimitGroup", "{fileID: 0}")
    result = line(result, "MaximumCount", "1")
    result = line(result, "Footprint", "{x: 2, y: 2}")
    result = line(result, "Category", "64")
    result = line(result, "MenuOrder", "504")
    result = line(result, "DefaultSkin", "")
    result = section(result, "    Construction:\n", "    Upgrade:\n", "      Enabled: 1\n      PlacementCosts: []\n      StageCosts: []\n      StageOutputs: []\n")
    result = section(result, "    Effects:\n", "    Defence:\n", "      Enabled: 0\n      Spatial: []\n")
    result = re.sub(r"(?m)^  Prefab:.*$", "  Prefab: {fileID: 5298753589461577345, guid: e60d9becfdf24a2385942973b58b0e19, type: 3}", result)
    return result


def create_missing_metas() -> None:
    scripts = [
        "ECS/Persistence/Snapshots/FirefighterSnapshot.cs",
        "ECS/Simulation/Buildings/BuildingFireOps.cs",
        "ECS/Simulation/Buildings/Firefighter.cs",
        "ECS/Simulation/Buildings/FirefighterOps.cs",
        "ECS/Simulation/Buildings/State/BuildingFireState.cs",
        "ECS/Simulation/Economy/CropGrowthOps.cs",
        "ECS/Simulation/RoadWeatherCostOps.cs",
        "ECS/Simulation/Services/LightningOps.cs",
        "ECS/Simulation/Services/SeasonWeatherOps.cs",
        "ECS/Simulation/State/LightningViewport.cs",
        "ECS/Simulation/State/SeasonWeatherState.cs",
        "Presentation/WeatherPresentationController.cs",
    ]
    for name in scripts:
        path = Path("Assets/Landsong/Scripts") / name
        target = path.with_name(path.name + ".meta")
        if not target.exists():
            target.write_text(f"fileFormatVersion: 2\nguid: {__import__('uuid').uuid4().hex}\n", encoding="utf-8")
    for name in ("b消防站", "b雷神殿"):
        path = BUILDINGS / name
        target = path.with_name(path.name + ".meta")
        if not target.exists():
            target.write_text(f"fileFormatVersion: 2\nguid: {__import__('uuid').uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


if __name__ == "__main__":
    create_building("b消防站", "9b7d6dda6fe745aeac312a135bdc202b", "688a0f5e594443f8a17c1b9966c23b6d", fire_station())
    create_building("b雷神殿", "6e7baab3d00847498386dc71e0ea641d", "e60d9becfdf24a2385942973b58b0e19", thunder_temple())
    create_missing_metas()
