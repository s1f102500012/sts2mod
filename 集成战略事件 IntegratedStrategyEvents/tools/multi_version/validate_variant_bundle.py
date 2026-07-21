#!/usr/bin/env python3
import argparse
import hashlib
import json
from pathlib import Path
import re


VERSION_RE = re.compile(r"\d+(?:\.\d+){1,3}")
SHA256_RE = re.compile(r"[0-9a-fA-F]{64}")


def version_key(value: str) -> tuple[int, ...]:
    if not isinstance(value, str) or not VERSION_RE.fullmatch(value):
        raise ValueError(f"invalid numeric compatibility target: {value!r}")
    return tuple(int(part) for part in value.split("."))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_under(path: Path, root: Path) -> None:
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError(f"path escapes {root}: {path}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dist", type=Path, required=True)
    parser.add_argument("--mod-id", required=True)
    parser.add_argument("--manifest-name", required=True)
    args = parser.parse_args()

    dist = args.dist.resolve()
    if not dist.is_dir():
        raise FileNotFoundError(f"missing dist directory: {dist}")

    manifest_path = dist / f"{args.mod_id}.json"
    with manifest_path.open(encoding="utf-8") as handle:
        mod_manifest = json.load(handle)
    if mod_manifest.get("id") != args.mod_id:
        raise ValueError("mod id mismatch")
    if mod_manifest.get("has_dll") is not True:
        raise ValueError("multi-version bundle requires has_dll=true")
    if not (dist / f"{args.mod_id}.dll").is_file():
        raise FileNotFoundError("missing root loader DLL")
    if mod_manifest.get("has_pck") is True and not (dist / f"{args.mod_id}.pck").is_file():
        raise FileNotFoundError("manifest declares has_pck=true but PCK is missing")

    with (dist / args.manifest_name).open(encoding="utf-8") as handle:
        variant_manifest = json.load(handle)
    if variant_manifest.get("schema") != 1:
        raise ValueError("unsupported variant manifest schema")
    variants = variant_manifest.get("variants")
    if not isinstance(variants, list) or not variants:
        raise ValueError("variant manifest contains no variants")

    targets = [entry.get("compatTarget") for entry in variants]
    if len(set(targets)) != len(targets):
        raise ValueError("duplicate compatibility target")
    parsed_targets = [version_key(target) for target in targets]
    if parsed_targets != sorted(parsed_targets):
        raise ValueError("variants are not sorted")

    lib_root = (dist / "lib").resolve()
    expected_dlls: set[Path] = set()
    for entry, target in zip(variants, targets):
        expected_directory = f"lib/{target}"
        if entry.get("directory") != expected_directory:
            raise ValueError(f"directory mismatch for {target}")
        if entry.get("assembly") != f"{args.mod_id}.dll":
            raise ValueError(f"assembly mismatch for {target}")

        directory = dist / expected_directory
        require_under(directory, lib_root)
        if directory.resolve().name != target:
            raise ValueError(f"directory basename mismatch: {directory}")
        if (directory / "compat-target.txt").read_text(encoding="utf-8").strip() != target:
            raise ValueError(f"compatibility marker mismatch: {directory}")

        dll = directory / f"{args.mod_id}.dll"
        require_under(dll, directory)
        if not dll.is_file():
            raise FileNotFoundError(f"missing variant DLL: {dll}")
        expected_dlls.add(dll.resolve())

        expected_hash = entry.get("sha256")
        if not isinstance(expected_hash, str) or not SHA256_RE.fullmatch(expected_hash):
            raise ValueError(f"invalid SHA-256 for {target}")
        if sha256(dll) != expected_hash.lower():
            raise ValueError(f"SHA-256 mismatch for {dll}")

    discovered_dlls = {path.resolve() for path in lib_root.rglob("*.dll")}
    if discovered_dlls != expected_dlls:
        raise ValueError("variant DLL set does not match manifest")

    minimum = mod_manifest.get("min_game_version")
    if not isinstance(minimum, str) or version_key(minimum) > parsed_targets[0]:
        raise ValueError("min_game_version is newer than the oldest variant")

    dependencies = mod_manifest.get("dependencies") or []
    ritsu = next((item for item in dependencies if item.get("id") == "STS2-RitsuLib"), None)
    if ritsu is None or ritsu.get("min_version") != "0.4.60":
        raise ValueError("STS2-RitsuLib dependency must require version 0.4.60")

    print(
        f"validated {args.mod_id}: loader + {len(variants)} variants, "
        f"targets={', '.join(targets)}, RitsuLib=0.4.60"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
