#!/usr/bin/env python3
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def read_json(path: Path) -> dict:
    if not path.exists():
        return {}
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def artifact_entry(path: Path) -> dict:
    return {
        "sha256": sha256_file(path),
        "sizeBytes": path.stat().st_size,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Update HextechRunes latest-version.json with official build hashes.")
    parser.add_argument("--latest-json", required=True, type=Path)
    parser.add_argument("--dist", required=True, type=Path)
    parser.add_argument("--mod-id", default="HextechRunes")
    parser.add_argument("--server-name", default="海克斯大乱斗")
    parser.add_argument("--server-identity", default="Natsuki.HextechRunes.official")
    parser.add_argument("--game-version", required=True)
    parser.add_argument("--dll-path", type=Path)
    parser.add_argument("--output-fingerprint", type=Path)
    args = parser.parse_args()

    manifest_path = args.dist / f"{args.mod_id}.json"
    dll_path = args.dll_path or args.dist / f"{args.mod_id}.dll"
    pck_path = args.dist / f"{args.mod_id}.pck"
    for path in (manifest_path, dll_path, pck_path):
        if not path.exists():
            raise FileNotFoundError(path)

    manifest = read_json(manifest_path)
    mod_version = manifest.get("version")
    if not isinstance(mod_version, str) or not mod_version:
        raise ValueError(f"manifest is missing version: {manifest_path}")

    generated_at = dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    build = {
        "modVersion": mod_version,
        "gameVersion": args.game_version,
        "hashAlgorithm": "SHA-256",
        "generatedAtUtc": generated_at,
        "dllSha256": sha256_file(dll_path),
        "pckSha256": sha256_file(pck_path),
        "manifestSha256": sha256_file(manifest_path),
        "artifactSizes": {
            "dllBytes": dll_path.stat().st_size,
            "pckBytes": pck_path.stat().st_size,
            "manifestBytes": manifest_path.stat().st_size,
        },
    }

    latest = read_json(args.latest_json)
    latest["modId"] = args.mod_id
    latest["serverIdentity"] = args.server_identity
    latest["name"] = args.server_name
    latest["latestVersion"] = mod_version

    existing_builds = latest.get("officialBuilds", [])
    if not isinstance(existing_builds, list):
        existing_builds = []
    latest["officialBuilds"] = [
        item for item in existing_builds
        if not (
            isinstance(item, dict)
            and item.get("modVersion") == mod_version
            and item.get("gameVersion") == args.game_version
        )
    ]
    latest["officialBuilds"].append(build)
    latest["officialBuilds"].sort(key=lambda item: (str(item.get("modVersion", "")), str(item.get("gameVersion", ""))))

    args.latest_json.parent.mkdir(parents=True, exist_ok=True)
    args.latest_json.write_text(json.dumps(latest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    if args.output_fingerprint:
        args.output_fingerprint.parent.mkdir(parents=True, exist_ok=True)
        args.output_fingerprint.write_text(json.dumps(build, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(
        f"Updated {args.latest_json} with {args.mod_id} {mod_version} "
        f"for STS2 {args.game_version}: dll={build['dllSha256'][:12]} "
        f"pck={build['pckSha256'][:12]} manifest={build['manifestSha256'][:12]}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
