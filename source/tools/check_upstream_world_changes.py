#!/usr/bin/env python3
"""Lists what a range of the original author's commits changes in the WORLD DATA, before it is merged.

The database is not in Git: the author ships database fixes as update scripts inside a release patch (see
release-baselines/README.md), so a merge of his `main` never changes our database by itself. This script shows the signs
that a database change is coming or is already assumed by his code:

  1. the "World" bullets his CHANGELOG.md gained in the range,
  2. the changed files and added lines that touch the database (tables, rows, DB objects saved by the server),
  3. the patch tooling and release baselines that changed (a new release patch means new update scripts to read).

Use, after `git fetch upstream` and before moving `main`:
    python source/tools/check_upstream_world_changes.py                  # main..upstream/main
    python source/tools/check_upstream_world_changes.py --old v0.37 --new main
Read-only: it runs `git diff` and `git show` and changes nothing.
"""
import argparse
import re
import subprocess
import sys

# Added lines that mean "this code reads or writes world rows".
DB_PATTERNS = re.compile(
    r"GameServer\.Database\.(SaveObject|AddObject|DeleteObject|SelectObjects|FindObjectByKey)"
    r"|\bDb[A-Z][A-Za-z]+\b"                       # DOL.Database objects (DbNPCTemplate, DbTeleport, ...)
    r"|\bINSERT\s+INTO\b|\bUPDATE\s+\w+\s+SET\b|\bDELETE\s+FROM\b|\bALTER\s+TABLE\b|\bCREATE\s+TABLE\b"
    r"|offline_[a-z_]+"                              # the offline tables
    r"|\bDataTable\b|\[DataTable\(|\[PrimaryKey|\[DataElement",
    re.IGNORECASE)
DB_FILES = re.compile(r"\.(sql|csv)$|\.db$|\.sqlite3?$|world-defaults|bot-goals\.json|release-world", re.IGNORECASE)
TOOLING = re.compile(r"source/tools/(make_patch\.py|OfflineDaoc\.Patcher/|release-baselines/|release-world-defaults\.json|"
                     r"find_changes_since_release\.py|build_release_)")


def git(*args):
    result = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode != 0:
        sys.exit(f"git {' '.join(args)} failed: {result.stderr.strip()}")
    return result.stdout


def changelog_world_bullets(old, new):
    """Added CHANGELOG lines that sit under a **World** heading in the new version."""
    new_text = git("show", f"{new}:CHANGELOG.md").splitlines()
    try:
        old_lines = set(git("show", f"{old}:CHANGELOG.md").splitlines())
    except SystemExit:
        old_lines = set()
    bullets, in_world = [], False
    for line in new_text:
        if line.startswith("**"):
            in_world = line.strip().lower().startswith("**world")
        elif line.startswith("## "):
            in_world = False
        elif in_world and line.strip() and line not in old_lines:
            bullets.append(line)
    return bullets


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--old", default="main", help="the last state already taken (default: main)")
    parser.add_argument("--new", default="upstream/main", help="the author's state to look at (default: upstream/main)")
    args = parser.parse_args()

    commits = git("log", "--oneline", f"{args.old}..{args.new}").splitlines()
    print(f"== {len(commits)} commits in {args.old}..{args.new}")
    if not commits:
        print("Nothing new.")
        return

    print("\n== 1. CHANGELOG 'World' bullets added")
    bullets = changelog_world_bullets(args.old, args.new)
    print("\n".join(bullets) if bullets else "(none)")

    names = git("diff", "--name-only", f"{args.old}..{args.new}").splitlines()
    print("\n== 2. Database-looking changes")
    data_files = [name for name in names if DB_FILES.search(name)]
    for name in data_files:
        print(f"DATA FILE  {name}")
    found = False
    for name in names:
        if not name.endswith(".cs") or "/Tests/" in name:
            continue
        diff = git("diff", "-U0", f"{args.old}..{args.new}", "--", name)
        hits = [line[1:].strip() for line in diff.splitlines()
                if line.startswith("+") and not line.startswith("+++") and DB_PATTERNS.search(line)]
        if hits:
            found = True
            print(f"CODE       {name}  ({len(hits)} added lines)")
            for hit in hits[:4]:
                print(f"             {hit[:150]}")
    if not data_files and not found:
        print("(no data file, and no added code line that reads or writes world rows)")

    print("\n== 3. Patch tooling and release baselines")
    tooling = [name for name in names if TOOLING.search(name)]
    print("\n".join(tooling) if tooling else "(unchanged)")
    if tooling:
        print("A changed patch tool or a new baseline usually means a release patch with database update scripts: read them.")


if __name__ == "__main__":
    main()
