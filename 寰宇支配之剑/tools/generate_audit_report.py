#!/usr/bin/env python3

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import sys


ALLOWED_ASSEMBLY_NAMES = {
    "0Harmony",
    "GodotSharp",
    "sts2",
}

CONTRACT_MARKERS = (
    "Erasure.AuditContractVersion",
    "Erasure.Scope",
    "Erasure.Interoperability",
    "Erasure.Identity",
    "Erasure.FailureMode",
    "Erasure.KnownRisk",
    "Erasure.TargetGameVersion",
    "ErasurePatchContract",
    "ErasureBoundaryAttribute",
)

FORBIDDEN_MEMBER_PATTERNS = {
    "Harmony unpatch API": re.compile(
        r"HarmonyLib\.Harmony\.(?:Unpatch|UnpatchAll)\b"
    ),
    "Harmony patch enumeration": re.compile(
        r"HarmonyLib\.Harmony\.GetPatchInfo\b"
    ),
    "Harmony priority mutation": re.compile(
        r"HarmonyLib\.HarmonyMethod(?:::|\.)"
        r"(?:set_priority|get_priority|priority|before|after)\b|"
        r"HarmonyLib\.Harmony(?:Priority|Before|After)\b"
    ),
    "loaded-assembly enumeration": re.compile(
        r"System\.AppDomain\.GetAssemblies\b"
    ),
    "external assembly load": re.compile(
        r"System\.Reflection\.Assembly\.(?:Load|LoadFrom|LoadFile)\b"
    ),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate a deterministic audit report from release DLLs."
    )
    parser.add_argument("--root", type=pathlib.Path, required=True)
    parser.add_argument("--dist", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    return parser.parse_args()


def run(command: list[str]) -> str:
    completed = subprocess.run(
        command,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"Command failed ({completed.returncode}): {' '.join(command)}\n"
            f"{completed.stdout}"
        )
    return completed.stdout


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def extract_method_body(source: str, signature: str) -> str:
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 0
    for index in range(opening, len(source)):
        character = source[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return source[opening + 1 : index]
    raise ValueError(f"Unbalanced method body for {signature}")


def read_patch_surface(root: pathlib.Path) -> dict[str, object]:
    patches_path = root / "src" / "ErasureKill.Patches.cs"
    patches = patches_path.read_text(encoding="utf-8")
    install = extract_method_body(
        patches,
        "public static void Install(Harmony harmony)",
    )
    callback_matches = re.findall(
        r"(?:prefixName|postfixName|finalizerName|transpilerName):\s*"
        r"nameof\(([^)]+)\)",
        install,
    )
    target_symbols = re.findall(
        r"nameof\(([^)]+)\)",
        install,
    )
    original_primitive_registrations = max(
        0,
        patches.count("PatchOriginalPrimitive(") - 1,
    )
    settlement_source = (
        root / "src" / "ErasureKill.SettlementPipeline.cs"
    ).read_text(encoding="utf-8")

    reflected_fields: list[str] = []
    for source_path in sorted((root / "src").glob("ErasureKill*.cs")):
        source = source_path.read_text(encoding="utf-8")
        reflected_fields.extend(
            re.findall(
                r"RequireField\([^;]*?\"([^\"]+)\"\)",
                source,
                flags=re.DOTALL,
            )
        )

    return {
        "evidence": {
            "registrationCounts": "source Install method",
            "forbiddenApis": "compiled DLL member references",
            "assemblyReferences": "compiled DLL AssemblyRef table",
            "contractMarkers": "compiled DLL metadata and string heaps",
        },
        "installPatchCallSiteCount": install.count("PatchRequired("),
        "installContainsDynamicCheckWinEnumeration": (
            "foreach (MethodInfo checkWinMethod in GetCheckWinMethods())"
            in install
        ),
        "reversePatchRegistrationCount": (
            original_primitive_registrations
            + settlement_source.count("CreateReversePatcher(")
        ),
        "callbacks": sorted(set(callback_matches)),
        "installNameofSymbols": sorted(set(target_symbols)),
        "reflectedPrivateFields": sorted(set(reflected_fields)),
    }


def assembly_references(monodis_output: str) -> list[str]:
    return re.findall(r"^\s*Name=(.+?)\s*$", monodis_output, re.MULTILINE)


def is_allowed_assembly(name: str) -> bool:
    return name in ALLOWED_ASSEMBLY_NAMES or name.startswith(("System", "Microsoft"))


def audit_variant(target: str, dll: pathlib.Path) -> tuple[dict[str, object], list[str]]:
    assembly_ref_output = run(["monodis", "--assemblyref", str(dll)])
    member_ref_output = run(["monodis", "--memberref", str(dll)])
    binary_strings = run(["strings", "-a", str(dll)])

    references = assembly_references(assembly_ref_output)
    unexpected_references = [
        name for name in references if not is_allowed_assembly(name)
    ]
    forbidden_members = [
        label
        for label, pattern in FORBIDDEN_MEMBER_PATTERNS.items()
        if pattern.search(member_ref_output)
    ]
    missing_contract_markers = [
        marker for marker in CONTRACT_MARKERS if marker not in binary_strings
    ]
    workshop_identifiers = sorted(
        set(re.findall(r"(?<!\d)\d{10}(?!\d)", binary_strings))
    )
    embedded_urls = sorted(
        set(re.findall(r"https?://[^\s\"]+", binary_strings))
    )

    violations: list[str] = []
    if unexpected_references:
        violations.append(
            f"unexpected assembly references: {', '.join(unexpected_references)}"
        )
    if forbidden_members:
        violations.append(
            f"forbidden member references: {', '.join(forbidden_members)}"
        )
    if missing_contract_markers:
        violations.append(
            f"missing contract markers: {', '.join(missing_contract_markers)}"
        )
    if workshop_identifiers:
        violations.append(
            f"embedded ten-digit identifiers: {', '.join(workshop_identifiers)}"
        )
    if embedded_urls:
        violations.append(f"embedded URLs: {', '.join(embedded_urls)}")

    result: dict[str, object] = {
        "target": target,
        "assembly": f"lib/{target}/UniversalDominionSword.dll",
        "sha256": sha256(dll),
        "assemblyReferences": references,
        "unexpectedAssemblyReferences": unexpected_references,
        "forbiddenMemberReferences": forbidden_members,
        "missingContractMarkers": missing_contract_markers,
        "embeddedTenDigitIdentifiers": workshop_identifiers,
        "embeddedUrls": embedded_urls,
        "status": "pass" if not violations else "fail",
    }
    return result, violations


def audit_source_policy(root: pathlib.Path) -> list[str]:
    production_source = "\n".join(
        path.read_text(encoding="utf-8")
        for path in sorted((root / "src").glob("*.cs"))
    )
    policies = {
        "Harmony unpatch call": re.compile(r"\.Unpatch(?:All)?\s*\("),
        "Harmony patch-info enumeration": re.compile(r"\.GetPatchInfo\s*\("),
        "explicit Harmony priority": re.compile(
            r"\bPriority\.(?:First|Last|VeryHigh|VeryLow|High|Low)\b|"
            r"\b(?:prefix|postfix|finalizer|transpiler)Priority\s*:|"
            r"\b(?:priority|before|after)\s*=|"
            r"\bHarmony(?:Priority|Before|After)\b"
        ),
    }
    return [
        label for label, pattern in policies.items() if pattern.search(production_source)
    ]


def write_reports(
    output: pathlib.Path,
    patch_surface: dict[str, object],
    variants: list[dict[str, object]],
    source_violations: list[str],
    variant_violations: dict[str, list[str]],
) -> None:
    output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": 1,
        "contractClaims": {
            "scope": "selected-lineage-only",
            "enumeratesThirdPartyPatches": False,
            "unpatchesThirdPartyPatches": False,
            "explicitPriorityOverrides": False,
            "knownRisk": (
                "Version-specific private members and async-state-machine IL "
                "require validation for every supported game update."
            ),
        },
        "patchSurface": patch_surface,
        "variants": variants,
        "sourcePolicyViolations": source_violations,
    }
    (output / "patch-surface.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    scan_lines = [
        "UniversalDominionSword compiled release audit",
        "",
        "This report distinguishes mechanically checked facts from design claims.",
        "A pass does not prove gameplay or multiplayer behavior.",
        "",
    ]
    if source_violations:
        scan_lines.append("Source policy: FAIL")
        scan_lines.extend(f"- {item}" for item in source_violations)
    else:
        scan_lines.append("Source policy: PASS")
        scan_lines.append("- no Harmony unpatch calls")
        scan_lines.append("- no Harmony patch enumeration")
        scan_lines.append("- no explicit Harmony priority overrides")
    for variant in variants:
        target = str(variant["target"])
        violations = variant_violations[target]
        scan_lines.extend(["", f"STS2 {target}: {str(variant['status']).upper()}"])
        if violations:
            scan_lines.extend(f"- {item}" for item in violations)
        else:
            scan_lines.extend(
                [
                    "- assembly references are limited to game, Harmony, Godot, and framework libraries",
                    "- no Harmony unpatch, patch-enumeration, or priority-mutation member references",
                    "- no embedded ten-digit identifiers or URLs",
                    "- decompilation contract markers are present",
                ]
            )
    (output / "forbidden-api-scan.txt").write_text(
        "\n".join(scan_lines) + "\n",
        encoding="utf-8",
    )

    hash_lines = [
        f"{variant['sha256']}  lib/{variant['target']}/UniversalDominionSword.dll"
        for variant in variants
    ]
    (output / "release-sha256.txt").write_text(
        "\n".join(hash_lines) + "\n",
        encoding="utf-8",
    )


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    dist = args.dist.resolve()
    output = args.output.resolve()
    manifest_path = dist / "universal-dominion-sword-variants.manifest"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    variants: list[dict[str, object]] = []
    violations_by_target: dict[str, list[str]] = {}
    for entry in manifest["variants"]:
        target = entry["compatTarget"]
        dll = dist / entry["directory"] / entry["assembly"]
        result, violations = audit_variant(target, dll)
        variants.append(result)
        violations_by_target[target] = violations

    source_violations = audit_source_policy(root)
    write_reports(
        output,
        read_patch_surface(root),
        variants,
        source_violations,
        violations_by_target,
    )

    failed = bool(source_violations) or any(violations_by_target.values())
    if failed:
        print(f"Audit failed. See {output / 'forbidden-api-scan.txt'}", file=sys.stderr)
        return 1
    print(f"Audit passed. Reports written to {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
