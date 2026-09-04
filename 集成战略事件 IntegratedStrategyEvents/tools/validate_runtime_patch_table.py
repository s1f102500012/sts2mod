#!/usr/bin/env python3
"""Compare the real Harmony table with the reviewed declaration snapshot."""
import argparse
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime", type=Path, required=True)
    parser.add_argument("--snapshot", type=Path, required=True)
    args = parser.parse_args()
    actual = []
    targets = set()
    for line in args.runtime.read_text().splitlines():
        parts = line.split("|")
        if parts[2] != "Natsuki.IntegratedStrategyEvents":
            continue
        targets.add(parts[0])
        # owner 和实际执行序保留在原始导出；声明快照比较其余契约字段。
        actual.append("|".join([parts[0], parts[1], parts[3], parts[4], *parts[6:]]))
    expected = args.snapshot.read_text().splitlines()
    missing = set(expected) - set(actual)
    extra = set(actual) - set(expected)
    if missing or extra or len(actual) != len(expected):
        raise SystemExit(f"runtime patch drift: missing={sorted(missing)}, extra={sorted(extra)}")
    print(f"Runtime patch table matches: {len(actual)} entries on {len(targets)} targets.")


if __name__ == "__main__":
    main()
