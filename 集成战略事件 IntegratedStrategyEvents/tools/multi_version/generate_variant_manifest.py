#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile


VERSION_RE = re.compile(r"\d+(?:\.\d+){1,3}")


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
    parser = argparse.ArgumentParser()
    parser.add_argument("--dist", type=Path, required=True)
    parser.add_argument("--mod-id", required=True)
    parser.add_argument("--manifest-name", required=True)
    parser.add_argument("--target", action="append", required=True)
    args = parser.parse_args()

    dist = args.dist.resolve()
    lib_root = (dist / "lib").resolve()
    if Path(args.manifest_name).name != args.manifest_name:
        raise ValueError("--manifest-name must be a basename")

    targets = sorted(set(args.target), key=version_key)
    if len(targets) != len(args.target):
        raise ValueError("duplicate --target value")

    variants = []
    for target in targets:
        version_key(target)
        directory = dist / "lib" / target
        require_under(directory, lib_root)
        if directory.resolve().name != target or not directory.is_dir():
            raise FileNotFoundError(f"missing variant directory: {directory}")

        dll = directory / f"{args.mod_id}.dll"
        require_under(dll, directory)
        if not dll.is_file():
            raise FileNotFoundError(f"missing variant DLL: {dll}")

        (directory / "compat-target.txt").write_text(f"{target}\n", encoding="utf-8")
        variants.append(
            {
                "compatTarget": target,
                "directory": f"lib/{target}",
                "assembly": f"{args.mod_id}.dll",
                "sha256": sha256(dll),
            }
        )

    output = dist / args.manifest_name
    with tempfile.NamedTemporaryFile(
        "w", encoding="utf-8", dir=dist, delete=False
    ) as handle:
        json.dump({"schema": 1, "variants": variants}, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
        temporary = Path(handle.name)
    os.replace(temporary, output)
    print(f"generated {output} with {len(variants)} variant(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
