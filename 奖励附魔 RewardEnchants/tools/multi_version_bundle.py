#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

VERSION_RE = re.compile(r"\d+(?:\.\d+){1,3}")
SHA256_RE = re.compile(r"[0-9a-fA-F]{64}")


def version_key(value: str) -> tuple[int, ...]:
    if not VERSION_RE.fullmatch(value):
        raise ValueError(f"invalid compatibility target: {value!r}")
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


def generate(args: argparse.Namespace) -> None:
    dist = args.dist.resolve()
    lib_root = (dist / "lib").resolve()
    targets = sorted(set(args.target), key=version_key)
    if len(targets) != len(args.target):
        raise ValueError("duplicate target")

    variants = []
    for target in targets:
        directory = dist / "lib" / target
        require_under(directory, lib_root)
        dll = directory / f"{args.mod_id}.dll"
        if not dll.is_file():
            raise FileNotFoundError(dll)
        (directory / "compat-target.txt").write_text(f"{target}\n", encoding="utf-8")
        variants.append({
            "compatTarget": target,
            "directory": f"lib/{target}",
            "assembly": dll.name,
            "sha256": sha256(dll),
        })

    output = dist / args.manifest_name
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=dist, delete=False) as handle:
        json.dump({"schema": 1, "variants": variants}, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
        temporary = Path(handle.name)
    os.replace(temporary, output)
    print(f"generated {output} with {len(variants)} variant(s)")


def validate(args: argparse.Namespace) -> None:
    dist = args.dist.resolve()
    with (dist / f"{args.mod_id}.json").open(encoding="utf-8") as handle:
        mod_manifest = json.load(handle)
    if mod_manifest.get("id") != args.mod_id or mod_manifest.get("has_dll") is not True:
        raise ValueError("invalid mod manifest")
    if not (dist / f"{args.mod_id}.dll").is_file():
        raise FileNotFoundError("missing root loader")

    with (dist / args.manifest_name).open(encoding="utf-8") as handle:
        bundle = json.load(handle)
    variants = bundle.get("variants")
    if bundle.get("schema") != 1 or not isinstance(variants, list) or not variants:
        raise ValueError("invalid variant manifest")

    targets = [entry.get("compatTarget") for entry in variants]
    parsed = [version_key(target) for target in targets]
    if len(set(targets)) != len(targets) or parsed != sorted(parsed):
        raise ValueError("targets must be unique and sorted")

    expected = set()
    lib_root = (dist / "lib").resolve()
    for entry, target in zip(variants, targets):
        directory = dist / f"lib/{target}"
        require_under(directory, lib_root)
        dll = directory / f"{args.mod_id}.dll"
        expected.add(dll.resolve())
        if entry.get("directory") != f"lib/{target}" or entry.get("assembly") != dll.name:
            raise ValueError(f"invalid entry for {target}")
        if (directory / "compat-target.txt").read_text(encoding="utf-8").strip() != target:
            raise ValueError(f"marker mismatch for {target}")
        digest = entry.get("sha256")
        if not isinstance(digest, str) or not SHA256_RE.fullmatch(digest) or sha256(dll) != digest.lower():
            raise ValueError(f"hash mismatch for {target}")

    if {path.resolve() for path in lib_root.rglob("*.dll")} != expected:
        raise ValueError("variant DLL set mismatch")
    if version_key(mod_manifest["min_game_version"]) > parsed[0]:
        raise ValueError("min_game_version exceeds oldest target")
    print(f"validated {args.mod_id}: targets={', '.join(targets)}")


def main() -> int:
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("generate", "validate"):
        command = commands.add_parser(name)
        command.add_argument("--dist", type=Path, required=True)
        command.add_argument("--mod-id", required=True)
        command.add_argument("--manifest-name", required=True)
        if name == "generate":
            command.add_argument("--target", action="append", required=True)
    args = parser.parse_args()
    generate(args) if args.command == "generate" else validate(args)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
