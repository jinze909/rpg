#!/usr/bin/env python3
"""Inspect the vendored ORK 3 source for the exact battle factory contract."""

from pathlib import Path
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
ARCHIVE = PROJECT / "Assets/Gaming Is Love/Makinom 2/ORK_Source_Code.zip"
TARGETS = ("BattleComponent.cs", "BaseBattleComponent.cs", "Group.cs", "AddCombatant.cs")
METHODS = ("CreateBattle(", "StartGroups(", "StartBattle(", "DoStartSetup(", "Join(", "SetLeader(", "GetBattleComponent(")


def main():
    with zipfile.ZipFile(ARCHIVE) as archive:
        names = [name for name in archive.namelist()
                 if Path(name).name in TARGETS and not name.endswith("/")]
        if not names:
            raise SystemExit("ORK source archive contains no battle component source.")
        print("ORK source files:")
        for name in names:
            print(f"- {name}")
        for name in names:
            source = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
            for method in METHODS:
                hits = [index for index, line in enumerate(source) if method in line]
                for index in hits[:2]:
                    start = max(0, index - 8)
                    end = min(len(source), index + (125 if method == "CreateBattle(" else 70))
                    print(f"\n--- {name}: {method} (lines {start + 1}-{end}) ---")
                    for line_number in range(start, end):
                        print(f"{line_number + 1:5}: {source[line_number]}")



def inspect_battle_system_selection():
    """Print the exact ORK 3.22 BattleSystemSelection declaration and members."""
    with zipfile.ZipFile(ARCHIVE) as archive:
        matches = []
        for name in archive.namelist():
            if not name.endswith(".cs"):
                continue
            source = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
            for index, line in enumerate(source):
                if "class BattleSystemSelection" in line or "struct BattleSystemSelection" in line:
                    matches.append((name, source, index))
        if not matches:
            print("\\nBattleSystemSelection declaration not found; matching references:")
            for name in archive.namelist():
                if not name.endswith(".cs"):
                    continue
                source = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
                for index, line in enumerate(source):
                    if "BattleSystemSelection" in line:
                        print(f"{name}:{index + 1}: {line.strip()}")
        for name, source, index in matches:
            start = max(0, index - 12)
            end = min(len(source), index + 260)
            print(f"\\n--- {name}: BattleSystemSelection declaration (lines {start + 1}-{end}) ---")
            for line_number in range(start, end):
                print(f"{line_number + 1:5}: {source[line_number]}")



def inspect_ork_battle_types():
    """Print the exact battle runtime and asset type declarations used by ORK."""
    declarations = (
        "class BattleSystem:",
        "class BattleSystemAsset:",
        "class TurnBasedBattleSystem:",
        "class AssetSelection",
    )
    with zipfile.ZipFile(ARCHIVE) as archive:
        for declaration in declarations:
            matches = []
            for name in archive.namelist():
                if not name.endswith(".cs"):
                    continue
                source = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
                for index, line in enumerate(source):
                    if declaration in line:
                        matches.append((name, source, index))
            for name, source, index in matches[:2]:
                start = max(0, index - 8)
                end = min(len(source), index + 150)
                print(f"\\n--- {name}: {declaration} (lines {start + 1}-{end}) ---")
                for line_number in range(start, end):
                    print(f"{line_number + 1:5}: {source[line_number]}")


if __name__ == "__main__":
    main()
    inspect_battle_system_selection()
    inspect_ork_battle_types()
