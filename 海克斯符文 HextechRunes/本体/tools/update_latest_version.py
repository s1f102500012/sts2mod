#!/usr/bin/env python3
"""把 dist 里 manifest 的版本号写进服务器的 latest-version.json(更新检查只读 latestVersion)。

历史上这里还会记录官方构建的 dll/pck/manifest 哈希供客户端做完整性指纹校验;该校验已于 2026-09 移除,
旧的 officialBuilds / serverIdentity 字段随本脚本一并清掉。
"""
import argparse
import json
from pathlib import Path


def read_json(path: Path) -> dict:
    if not path.exists():
        return {}
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def main() -> int:
    parser = argparse.ArgumentParser(description="Update HextechRunes latest-version.json.")
    parser.add_argument("--latest-json", required=True, type=Path)
    parser.add_argument("--dist", required=True, type=Path)
    parser.add_argument("--mod-id", default="HextechRunes")
    parser.add_argument("--server-name", default="海克斯大乱斗")
    args = parser.parse_args()

    manifest_path = args.dist / f"{args.mod_id}.json"
    if not manifest_path.exists():
        raise FileNotFoundError(manifest_path)
    mod_version = read_json(manifest_path).get("version")
    if not isinstance(mod_version, str) or not mod_version:
        raise ValueError(f"manifest is missing version: {manifest_path}")

    latest = read_json(args.latest_json)
    latest.pop("officialBuilds", None)
    latest.pop("serverIdentity", None)
    latest["modId"] = args.mod_id
    latest["name"] = args.server_name
    latest["latestVersion"] = mod_version

    args.latest_json.parent.mkdir(parents=True, exist_ok=True)
    args.latest_json.write_text(json.dumps(latest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Updated {args.latest_json}: {args.mod_id} latestVersion={mod_version}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
