#!/usr/bin/env python3
"""Fail a test run when line or branch coverage drops below a floor.

Reads the Cobertura reports produced by `dotnet test --collect:"XPlat Code
Coverage"` and aggregates them. No third-party dependency is required, so it
runs on a bare GitHub Actions runner.
"""

from __future__ import annotations

import argparse
import pathlib
import sys
import xml.etree.ElementTree as ET


def percentage(covered: int, valid: int) -> float:
    return 100.0 * covered / valid if valid else 100.0


def collect(reports: list[pathlib.Path]) -> tuple[int, int, int, int]:
    lines_covered = lines_valid = branches_covered = branches_valid = 0

    for report in reports:
        root = ET.parse(report).getroot()
        lines_valid += int(root.get("lines-valid", "0"))
        lines_covered += int(root.get("lines-covered", "0"))
        branches_valid += int(root.get("branches-valid", "0"))
        branches_covered += int(root.get("branches-covered", "0"))

    return lines_covered, lines_valid, branches_covered, branches_valid


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results", type=pathlib.Path, help="dotnet test results directory")
    parser.add_argument("--min-line", type=float, default=78.0)
    parser.add_argument("--min-branch", type=float, default=55.0)
    args = parser.parse_args()

    reports = sorted(args.results.rglob("coverage.cobertura.xml"))
    if not reports:
        print(f"No coverage report found under {args.results}", file=sys.stderr)
        return 2

    lines_covered, lines_valid, branches_covered, branches_valid = collect(reports)
    line_rate = percentage(lines_covered, lines_valid)
    branch_rate = percentage(branches_covered, branches_valid)

    print(f"Line coverage:   {line_rate:.2f}% ({lines_covered}/{lines_valid}), floor {args.min_line:.2f}%")
    print(f"Branch coverage: {branch_rate:.2f}% ({branches_covered}/{branches_valid}), floor {args.min_branch:.2f}%")

    failed = False
    if line_rate < args.min_line:
        print("Line coverage is below the floor.", file=sys.stderr)
        failed = True
    if branch_rate < args.min_branch:
        print("Branch coverage is below the floor.", file=sys.stderr)
        failed = True

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
