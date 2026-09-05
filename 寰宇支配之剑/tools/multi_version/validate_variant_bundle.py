#!/usr/bin/env python3
import argparse
import hashlib
import json
from pathlib import Path
import re


VERSION_RE = re.compile(r"\d+(?:\.\d+){1,3}")
SHA256_RE = re.compile(r"[0-9a-fA-F]{64}")


def version_key(value: str) -> tuple[int, ...]:
    if not VERSION_RE.fullmatch(value):
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
    parser = argparse.ArgumentParser(
        description="Validate a stable-loader STS2 multi-version bundle."
    )
    parser.add_argument("--dist", type=Path, required=True)
    parser.add_argument("--mod-id", required=True)
    parser.add_argument("--manifest-name", required=True)
    parser.add_argument("--assembly")
    args = parser.parse_args()

    dist = args.dist.resolve()
    if not dist.is_dir():
        raise FileNotFoundError(f"missing dist directory: {dist}")
    if Path(args.manifest_name).name != args.manifest_name:
        raise ValueError("--manifest-name must be a basename")

    assembly = args.assembly or f"{args.mod_id}.dll"
    mod_manifest_path = dist / f"{args.mod_id}.json"
    with mod_manifest_path.open(encoding="utf-8") as handle:
        mod_manifest = json.load(handle)
    if mod_manifest.get("id") != args.mod_id:
        raise ValueError(
            f"mod id mismatch: {mod_manifest.get('id')!r} != {args.mod_id!r}"
        )
    if mod_manifest.get("has_dll") is not True:
        raise ValueError("multi-version loader bundle requires has_dll=true")
    if not (dist / f"{args.mod_id}.dll").is_file():
        raise FileNotFoundError("missing root loader DLL")
    if mod_manifest.get("has_pck") is True and not (
        dist / f"{args.mod_id}.pck"
    ).is_file():
        raise FileNotFoundError("manifest declares has_pck=true but PCK is missing")

    variant_manifest_path = dist / args.manifest_name
    with variant_manifest_path.open(encoding="utf-8") as handle:
        variant_manifest = json.load(handle)
    if variant_manifest.get("schema") != 1:
        raise ValueError("unsupported variant manifest schema")
    variants = variant_manifest.get("variants")
    if not isinstance(variants, list) or not variants:
        raise ValueError("variant manifest contains no variants")

    lib_root = (dist / "lib").resolve()
    targets = [entry.get("compatTarget") for entry in variants]
    if len(set(targets)) != len(targets):
        raise ValueError("duplicate compatibility target")
    parsed_targets = [version_key(target) for target in targets]
    if parsed_targets != sorted(parsed_targets):
        raise ValueError("variants are not sorted by compatibility target")

    expected_dlls: set[Path] = set()
    for entry, target in zip(variants, targets):
        expected_directory = f"lib/{target}"
        if entry.get("directory") != expected_directory:
            raise ValueError(
                f"directory mismatch for {target}: {entry.get('directory')!r}"
            )
        if entry.get("assembly") != assembly:
            raise ValueError(
                f"assembly mismatch for {target}: {entry.get('assembly')!r}"
            )

        directory = dist / expected_directory
        require_under(directory, lib_root)
        if directory.resolve().name != target:
            raise ValueError(f"variant directory basename mismatch: {directory}")
        marker = directory / "compat-target.txt"
        if marker.read_text(encoding="utf-8").strip() != target:
            raise ValueError(f"compatibility marker mismatch: {marker}")

        dll = directory / assembly
        require_under(dll, directory)
        if not dll.is_file():
            raise FileNotFoundError(f"missing variant DLL: {dll}")
        expected_dlls.add(dll.resolve())

        expected_hash = entry.get("sha256")
        if not isinstance(expected_hash, str) or not SHA256_RE.fullmatch(
            expected_hash
        ):
            raise ValueError(f"invalid SHA-256 for {target}")
        if sha256(dll) != expected_hash.lower():
            raise ValueError(f"SHA-256 mismatch for {dll}")

    discovered_dlls = {path.resolve() for path in lib_root.rglob("*.dll")}
    if discovered_dlls != expected_dlls:
        extras = sorted(str(path) for path in discovered_dlls - expected_dlls)
        missing = sorted(str(path) for path in expected_dlls - discovered_dlls)
        raise ValueError(f"variant DLL set mismatch; extras={extras}, missing={missing}")

    minimum = mod_manifest.get("min_game_version")
    if not isinstance(minimum, str) or version_key(minimum) > parsed_targets[0]:
        raise ValueError(
            "min_game_version must be present and no higher than the oldest variant"
        )

    print(
        f"validated {args.mod_id}: loader + {len(variants)} variant(s), "
        f"targets={', '.join(targets)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
