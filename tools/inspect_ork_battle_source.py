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



def inspect_battle_end_lifecycle():
    """Expose the installed ORK 3 battle teardown contract in the Unity CI log."""
    targets = {"Battle.cs", "ControlHandler.cs", "BattleComponent.cs"}
    needles = ("ClearBattle(", "EndBattle(", "bool InBattle", " InBattle =>")
    printed = 0
    with zipfile.ZipFile(ARCHIVE) as archive:
        for name in archive.namelist():
            if Path(name).name not in targets or not name.endswith(".cs"):
                continue
            source = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
            for index, line in enumerate(source):
                if not any(needle in line for needle in needles):
                    continue
                if printed >= 14:
                    return
                printed += 1
                start, end = max(0, index - 5), min(len(source), index + 32)
                print(f"\\n--- ORK battle lifecycle: {name}:{index + 1} ---")
                for line_no in range(start, end):
                    print(f"{line_no + 1:5}: {source[line_no]}")



def inspect_combatant_action_contract():
    """Audit the installed ORK player-choice API without dumping licensed source bodies."""
    classes = (
        "class CombatantAbilities", "class CombatantActions",
        "class AbilityShortcut", "class AbilityAction", "class BaseAction",
        "class ActiveAbility", "class BattleAction", "class CombatantAction",
    )
    methods = (
        "GetCurrentBaseAttack(", "StartAction(", "UseAbility(",
        "Use(", "Choose(", "Select(", "SetTarget(", "SetTargets(",
        "GetAbility(", "AddAction(", "Execute(", "Activate(",
    )
    found_classes = set()
    method_signatures = 0
    with zipfile.ZipFile(ARCHIVE) as archive:
        for name in sorted(archive.namelist()):
            if not name.endswith(".cs"):
                continue
            lines = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
            declared = [(number, line.strip()) for number, line in enumerate(lines, 1)
                        if any(marker in line for marker in classes)]
            if not declared:
                continue
            for number, line in declared[:5]:
                found_classes.add(line)
                print(f"ORK_ACTION_TYPE {Path(name).name}:{number}: {line[:240]}")
            printed_here = 0
            for number, line in enumerate(lines, 1):
                signature = line.strip()
                if not signature.startswith(("public ", "internal ", "protected ")):
                    continue
                if not any(marker in signature for marker in methods):
                    continue
                # Only publish declaration lines: never dump implementation bodies
                # from the user's licensed ORK source archive into public CI logs.
                print(f"ORK_ACTION_SIGNATURE {Path(name).name}:{number}: {signature[:240]}")
                printed_here += 1
                method_signatures += 1
                if printed_here == 18 or method_signatures == 90:
                    break
            if method_signatures == 90:
                break
    if not found_classes:
        raise RuntimeError("Frozen ORK archive is missing expected action classes")
    print(f"ORK_ACTION_AUDIT_OK classes={len(found_classes)} signatures={method_signatures}")



def inspect_ability_selection_predicates():
    """Summarize ORK's attack eligibility guards without publishing licensed code."""
    import re

    targets = {
        "AbilityShortcut.cs": ("CanUse", "CanChoose"),
        "ActiveAbility.cs": ("CanUse", "CanChoose"),
    }
    with zipfile.ZipFile(ARCHIVE) as archive:
        for name in sorted(archive.namelist()):
            basename = Path(name).name
            if basename not in targets or not name.endswith(".cs"):
                continue
            lines = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
            for method in targets[basename]:
                starts = [
                    index for index, line in enumerate(lines)
                    if re.search(r"\b(?:public|protected)\s+(?:override\s+)?bool\s+" +
                                 method + r"\s*\(", line)
                ]
                for start in starts[:1]:
                    # Balance braces from the declaration to the method end.
                    depth, entered, body = 0, False, []
                    for line in lines[start:]:
                        depth += line.count("{") - line.count("}")
                        if "{" in line:
                            entered = True
                        body.append(line)
                        if entered and depth == 0:
                            break
                    content = "\n".join(body)
                    guard_calls = sorted(set(re.findall(
                        r"\b((?:Can|Check|Has|Is|Get|Try)[A-Z][A-Za-z0-9_]*)\s*\(",
                        content)))
                    guard_symbols = sorted(set(re.findall(
                        r"\b(?:user|ability|settings|action|combatant|status|costs|time|battle)"
                        r"\.[A-Za-z_][A-Za-z_0-9]*", content)))
                    print(
                        "ORK_ATTACK_ELIGIBILITY " + basename + "." + method +
                        " negative_exits=" + str(len(re.findall(r"\breturn\s+false\s*;", content))) +
                        " predicate_calls=" + ",".join(guard_calls[:36]) +
                        " observed_members=" + ",".join(guard_symbols[:50])
                    )

if __name__ == "__main__":
    main()
    inspect_battle_system_selection()
    inspect_ork_battle_types()
    inspect_battle_end_lifecycle()
    inspect_combatant_action_contract()
    inspect_ability_selection_predicates()
