# Working on Offline DAoC: every command, from zero

This is the hand-over document. It assumes nothing: a person or an AI agent that has never seen the project should be able to
start a session, change the code, build it, install it, check the logs and update the new client and the launcher by following it.
It complements [AGENTS.md](../AGENTS.md) (the rules) and [DEVELOPMENT.md](DEVELOPMENT.md) (the layout and the release pipeline).
Everything below was done this way during the 2026-10-26 .. 2026-11-01 sessions; where a step was *not* run recently, it says so.

---------------------------------------------------------------------------------------------------

## 0. Read this first: the owner and the working agreement

- The owner is the only person who plays the game. They write in **Italian**: answer in Italian, but keep code, comments, commit
  messages and documents in **English**.
- They play the **original client** (`game.dll`, the DAoC 1.65 client), not the new Godot client.
- They run the server themselves through the launcher. **Never start the server without being asked, and never copy a build over a
  running server.** Before installing, ask "server fermo?" and wait for a yes (do not try to detect it yourself and decide for them;
  you may look at the process list only to confirm what they told you).
- Standing rules of the owner (each one was a correction at some point):
  1. **One commit per change**, with its test, then push. Do it when the owner asks for the change, without further questions.
  2. **A check or an investigation is read-only.** When asked "controlla i log" / "come sta andando" / "controlla che..." you report
     what you found, the cause and a proposal, and you change nothing until the owner says "fix it". (Changes the owner explicitly
     asked for are made at once.)
  3. **No backup copies when installing** `GameServer.dll` / `.pdb` ("non servono a niente"). Verify the hash after copying.
  4. **No bulk merge of the original author's work** (see section 4.4). Take single items only when the owner says so.
  5. **Be honest in reports:** say which results are automated tests and which were seen in the game. Say what you did not check.
  6. **Never invent a rule and attribute it to the owner.** If you are unsure whether the owner said something, say so.
  7. Do not ask permission for things that have a conventional default; do ask before anything hard to undo (installing over the
     running server, deleting, changing the world database).
- The owner's gameplay rules (from AGENTS.md; keep them unless asked): real loot, inventories, coins and equipment upgrades; the Realm
  Exchange; stablemaster routes; selling only at natural task breaks; gamebots start without armor and earn it; Darkness Falls is
  open to solo bots; do not describe Darkness Falls raid AI, Legion or the hardest level 70+ encounters as implemented.
- Three kinds of bots are different, check which one a change touches: **real players**, **companions** (`/spawn`, owned by a
  player), **autonomous gamebots** (the `bots/autonomous` code; the ones that live in the world on their own).

---------------------------------------------------------------------------------------------------

## 1. The machine, the tools, the folders

### 1.1 Tools (Windows 11)

| Tool | Version used | Notes |
|---|---|---|
| .NET SDK | 10.0.403 (`dotnet --version`) | builds the server, the launcher and the new client's C# part |
| Python | 3.13 | all helper scripts. **Always run them as `python -I script.py`** (isolated mode: no stray modules from the current directory) |
| Git | any recent | remotes below. `gh` (GitHub CLI) is **not** installed |
| Godot | 4.7.2 stable **mono**, only inside the install: `new-client\tools\godot_net\Godot_v4.7.2-stable_mono_win64*.exe` | not needed for the server |
| Shells | Git Bash (POSIX) and PowerShell 5.1 | see the pitfalls in 1.4 |

`dotnet` prints its messages **in Italian** on this machine ("Errori", "Avvisi", "Superato!", "Non superato"). Grep for those words.

### 1.2 The two folders (do not confuse them)

| What | Path | Role |
|---|---|---|
| **The repository** | `D:\Giochi\OfflineDAoC\svil` (Git Bash: `/d/Giochi/OfflineDAoC/svil`) | where you edit, build and commit. Branch `mod-0.37` |
| **The playable install** | `D:\Giochi\OfflineDAoC\playable-v0.37c-classic` | the game the owner plays. You only *copy tested builds into it* and read its logs/database |

The install contains its own `source\`, `tools\`, `docs\` copies (they came with the download). **Ignore them; edit only the repository.**
A second copy, `D:\Giochi\OfflineDAoC\playable-test`, exists for experiments; do not touch it unless asked.

### 1.3 What is inside the install

```
playable-v0.37c-classic\
  START OFFLINE DAOC.cmd                 starts runtime\OfflineDAoC.exe (the launcher) with the bundled .NET (tools\dotnet)
  START OFFLINE DAOC - NEW CLIENT.cmd    starts new-client\runtime\OfflineDAoC.exe (same launcher, new-client mode)
  runtime\
    OfflineDAoC.exe / .dll / .pdb / .deps.json / .runtimeconfig.json    the launcher
    server\                              CoreServer.exe, GameServer.dll (+ .pdb), all server libraries, navmesh\, bot-goals.json,
                                         rvr-world.json (written by the server every 30 s), logs\
    server\lib\ and server\win-x64\      two more copies of GameServer.dll/.pdb (AGENTS.md requires all three to be updated together)
    data\opendaoc.sqlite3.db             THE WORLD AND THE SAVES (never commit it, never overwrite it)
    account.txt                          the local account (never commit)
    client-opendaoc\app\                 the original game client (5 GB; never edit)
  new-client\                            godot\ (the Godot project, content 33 GB), runtime\ (launcher), tools\godot_net\, save\
  tools\dotnet\                          the bundled .NET runtime the launcher scripts use
```

### 1.4 Shell pitfalls (these cost time; read them)

- **Git Bash:** `cd` does not persist between calls, use absolute paths (`/d/Giochi/...`). Windows switches like `/s` are turned into
  paths: `shutdown /s /t 60` **fails in Git Bash** ("Sintassi: shutdown.exe ..."). Run such commands in **PowerShell**.
- **Heredocs with apostrophes** inside a tool-driven bash command can break. For anything non-trivial write the script to a file
  (a scratch folder), then run `python -I that_file.py`.
- **Line endings:** most `.cs` files are **CRLF** (some are mixed). When you edit with a script, detect and keep the file's own ending
  (`'\r\n' in text`). A new file written with LF is converted by Git ("LF will be replaced by CRLF"); that warning is harmless.
- **Timestamps in logs** are local time, `MM/DD HH:MM:SS`. The JSON the server writes is UTC (`UpdatedUtc`).
- **Encoding:** the server writes UTF-8 without BOM; the Italian console may show `?` for characters such as the dash; that is the display only.

---------------------------------------------------------------------------------------------------

## 2. Starting a session: the checklist

Run these in order and read the output before touching anything.

```bash
cd /d/Giochi/OfflineDAoC/svil
git status --short                    # must be empty, otherwise find out whose changes these are before editing
git branch --show-current             # mod-0.37
git log --oneline -15                 # what was done last
git remote -v                         # origin = Nikse77/OfflineDAoC (push here), upstream = shadowofze/OfflineDAoC (read only)
cat AGENTS.md docs/DEVELOPMENT.md docs/WORKFLOW-AND-COMMANDS.md   # the rules, the layout, this file
```

Then find out the state of the running game (read-only):

```powershell
Get-Process CoreServer -ErrorAction SilentlyContinue | Select-Object Id,StartTime      # is the server running, since when
Get-Process OfflineDAoC -ErrorAction SilentlyContinue | Select-Object Id,StartTime     # is the launcher open (it must be, to start/stop)
Get-NetTCPConnection -State Listen -LocalPort 10300 -ErrorAction SilentlyContinue       # the game port
```

```bash
# Which build is installed? Compare with the last build you made (section 5/6).
sha256sum /d/Giochi/OfflineDAoC/playable-v0.37c-classic/runtime/server/GameServer.dll
git log -1 --format="%h %ad %s" --date=format:"%d/%m %H:%M"
```

A session's first message from the owner usually continues earlier work. If an AI agent has a memory directory, read its index; this
document is written so that nothing in it is required, but it records rules the owner gave.

---------------------------------------------------------------------------------------------------

## 3. Where the code is

| To change... | Look in |
|---|---|
| Autonomous gamebot goals, camps, groups, travel, RvR | `source/server/GameServer/bots/autonomous/` |
| RvR planning (event layer, campaign, patrol zones, formation, login, dashboard data) | `AutonomousRvr*.cs` in that folder |
| Companion bots, class combat AI, bot casting | `source/server/GameServer/bots/` (`BotBrain.cs`, `GameBot.cs`, ...) |
| Keeps, doors, claims | `source/server/GameServer/keeps/` and `.../gameobjects/GameKeep*.cs` |
| Server tests | `source/server/Tests/UnitTests/` (NUnit) |
| Launcher and the RvR/battleground dashboards | `source/tools/OfflineDaoc.Launcher/` (`MainForm.cs`, `RealmEventsPanel.cs`, `BattlegroundsPanel.cs`) |
| Launcher tests | `source/tools/OfflineDaoc.Launcher.Tests/` |
| The new client | `source/new-client/godot/game/` (GDScript + `net/DaocClient.cs`) |
| Database helper scripts (world changes) | `tools/claude-version/` |
| Settings stored in the database | tables `offline_population_settings`, `offline_local_options`; file `runtime/server/bot-goals.json` |

Useful facts about the structure: `GameBot` is both a companion and a gamebot; `IsAutonomousWorldBot` tells them apart. The per-bot
controller is `AutonomousWorldBotController` (partial class in many files). Group formation and tenure are in
`AutonomousBotGroupCoordinator`. Everything RvR-related is described in `docs/` notes and in the class comments, which say *why*
(run numbers and owner decisions with dates).

---------------------------------------------------------------------------------------------------

## 4. Git

### 4.1 Remotes and branches

- `origin` = `https://github.com/Nikse77/OfflineDAoC.git` (the owner's fork, **push here**).
- `upstream` = `https://github.com/shadowofze/OfflineDAoC.git` (the original author; read only).
- `main` mirrors `upstream/main` (never commit on it). **`mod-0.37` is the working branch** = the release tag `v0.37` + the owner's changes.
- Pull requests to the author get their own branch from `upstream/main` (`pr/<topic>`), only when needed.

### 4.2 Commit and push (one commit per change)

```bash
cd /d/Giochi/OfflineDAoC/svil
git add <only the files of this change, with their test>
git commit -F - <<'E'
Short imperative title that says what the bot/system does now

Why (owner request with date, the symptom seen), what changed, what was not verified.

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_<id of the current session>
E
GIT_TERMINAL_PROMPT=0 timeout 100 git push origin mod-0.37 2>&1 | tail -1
```

- The two trailer lines are given by the tool for the current session; use the ones the harness shows. Another agent uses its own.
- `GIT_TERMINAL_PROMPT=0` makes a missing credential fail instead of hanging; `timeout 100` bounds the push.
- Never commit: a played database, `account.txt`, logs, rosters, backups, credentials, dumps (`.gitignore` is only a safety net).
  Run `git diff --cached --stat` before committing.

### 4.3 Not on this machine

`gh` is not installed. Pull requests are made by the owner on the web.

### 4.4 The original author's work (upstream)

The owner decided on 2026-10-30 to stop following the author's line: **no `git merge upstream/main`**. After `git fetch upstream`,
read the new commits, propose the items one by one (what it fixes, overlap with our files, recommendation) and take one **only when the
owner says so**, as its own commit (`git cherry-pick -x`), our side winning where they clash. The new client is the exception: keep it
aligned by cherry-picking upstream's commits that touch **only** `source/new-client` (check with `git show <sha> --name-only`).

```bash
git fetch upstream --tags
git log --oneline HEAD..upstream/main
python -I source/tools/check_upstream_world_changes.py          # read-only: do the new commits imply database changes?
```

The database is not in Git: the author ships database fixes inside release patches; a code merge never changes the owner's database.

---------------------------------------------------------------------------------------------------

## 5. Build and test the server

All commands from `source/server`. The build writes into `source/server/build/...` (ignored by Git).

```bash
cd /d/Giochi/OfflineDAoC/svil/source/server
# one-time on a fresh checkout: the build needs CoreServer/config/serverconfig.xml to exist (it is local and git-ignored)
cp -n CoreServer/config/serverconfig.example.xml CoreServer/config/serverconfig.xml

dotnet build Tests/Tests.csproj -c Release --nologo -v q 2>&1 | grep -E "error|Errori"      # want "Errori: 0"
dotnet test  Tests/Tests.csproj -c Release --no-build --nologo 2>&1 | tail -3               # want "Superato! ... Non superati: 0"
```

- Building `Tests.csproj` also builds `GameServer` and the other server projects. The product of the build that gets installed is
  **`source/server/build/Tests/Release/lib/GameServer.dll`** and its `.pdb`. (DEVELOPMENT.md also describes building the whole
  solution `"Dawn of Light.sln"`; in this workflow the Tests project is always the one built, so the installed file and the tested file are the same.)
- The build prints several hundred warnings (about 640). That is normal; only errors matter.
- At the time of writing (2026-11-03): **3,442 server tests pass**, 0 fail. The count grows with every change.
- A failing test after your change means either you broke something or the test documented the behaviour you changed on purpose. In the
  second case update the test to the new rule and say so in the commit message (an example: the old "the army marches at the deadline"
  test became "a rally under the army minimum fails" plus "the minimum gathered march"; the minimum was 16 and is 20
  since 2026-11-03, `AutonomousRvrEventLayer.MinimumArmy`).
- Run one test: `dotnet test Tests/Tests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~UT_RvrPatrolLock"`.
- Test state is global (static): tests of the RVR event layer reset it with `RvrEventTestState.Clear()` (in `Tests/UnitTests`), which
  also turns off the siege scheduling that is on in production. A new static dictionary in the event layer must be added to that list if
  tests would otherwise share it.
- The first build after a clean checkout downloads NuGet packages and is slow; later builds take about 40 s, the tests about 25 s.

Launcher tests (separate project, expect no local server running):

```bash
dotnet test /d/Giochi/OfflineDAoC/svil/source/tools/OfflineDaoc.Launcher.Tests/OfflineDaoc.Launcher.Tests.csproj -c Release --nologo
```

---------------------------------------------------------------------------------------------------

## 6. Install a server build into the game

Preconditions: the change is committed, the build has 0 errors, the tests pass, and **the owner has said the server is stopped**.

```bash
cd /d/Giochi/OfflineDAoC/svil/source/server
git status --short                                  # clean
git log -1 --format=%h                              # the commit you are installing
dotnet build Tests/Tests.csproj -c Release --nologo -v q 2>&1 | grep -E "Errori"     # 0

R=/d/Giochi/OfflineDAoC/playable-v0.37c-classic/runtime/server
for d in "$R" "$R/lib" "$R/win-x64"; do
  cp build/Tests/Release/lib/GameServer.dll build/Tests/Release/lib/GameServer.pdb "$d/"
done

# verify: every copy must have the build's hash (expect two lines of "4 <hash>": 4 dll files, 4 pdb files)
sha256sum build/Tests/Release/lib/GameServer.dll "$R/GameServer.dll" "$R/lib/GameServer.dll" "$R/win-x64/GameServer.dll" \
          build/Tests/Release/lib/GameServer.pdb "$R/GameServer.pdb" "$R/lib/GameServer.pdb" "$R/win-x64/GameServer.pdb" | cut -c1-12 | sort | uniq -c
```

- Three folders, two files each. No backups (owner decision). Only `GameServer.dll/.pdb` change for a bot/AI change; the other
  libraries are replaced only if the change touched `CoreBase`, `CoreDatabase` or `CoreServer`.
- If a copy fails with "Text file busy"/"access denied", the server (or its launcher) is still running: stop and ask again.
- **Do not start the server** afterwards unless asked. Tell the owner the commit and the hashes, and what to look for in the log (section 11).

---------------------------------------------------------------------------------------------------

## 7. Stop and start the server

The server is started and stopped from the launcher (`runtime\OfflineDAoC.exe`, button **START SERVER** / **STOP SERVER**). One launcher at a
time: the game port is **10300** and a second server cannot use it. The launcher must be open for the scripts below.

Two PowerShell scripts drive the launcher's buttons through Windows UI Automation (they click the buttons the owner would click):
`tools/dev-workflow/stop_server.ps1` and `tools/dev-workflow/start_server.ps1`. They refuse to run when there is not exactly one
launcher process.

```powershell
# stop (waits up to 240 s; presses OK on the "Shutdown pending" box; never kills anything)
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Giochi\OfflineDAoC\svil\tools\dev-workflow\stop_server.ps1
# start (waits for port 10300 to listen, up to 480 s; answers "Yes" to the "Low available memory" box;
#        stops if a dialog needs a person: "Unsaved bot goals", "Invalid bot goals", "Cannot start")
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Giochi\OfflineDAoC\svil\tools\dev-workflow\start_server.ps1
```

- Exit codes: 0 ok, 2 wrong number of launcher/server processes, 3/5 button missing or disabled, 4 server still running after 240 s,
  6 port not listening after 480 s, 7 a dialog needs the owner.
- Only run them when the owner asked you to start/stop (or authorised a scheduled shutdown). Usually the owner says "avvio io".
- A server start takes about a minute for the world to load, then the bots enter in the **staggered login queue** (highest level first)
  over many minutes; groups of RvR bots do not form in the first 5 minutes.
- To stop in an emergency and shut the PC down (only when the owner authorised it): stop the server with the script; if it will not stop,
  `Stop-Process -Name CoreServer -Force`; then in **PowerShell** `shutdown.exe /s /t 90` (cancel with `shutdown /a`).

---------------------------------------------------------------------------------------------------

## 8. The launcher and the dashboards

"The dashboard" has **two halves**:

1. **Server side:** `AutonomousRvrDashboard` (in `GameServer.dll`) writes `runtime/server/rvr-world.json` every 30 seconds: keeps
   (owner, state, forces), relics, raids, the "Patrol - <zone>" rows and the participants of each event, and the battleground
   summary. This part is installed with the server build (section 6).
2. **Launcher side:** the tabs "Active RvR"/"Realm events" and "Battlegrounds" read that file (`RealmEventsPanel.cs`,
   `BattlegroundsPanel.cs`, the model records in `MainForm.cs`). The per-bot roster tab reads the saved bot rows from the database.
   This part is the launcher program and needs its own build and install.

Look at the data without the launcher:

```bash
python -I - <<'E'
import json
d=json.load(open('/d/Giochi/OfflineDAoC/playable-v0.37c-classic/runtime/server/rvr-world.json',encoding='utf-8'))
print(d['UpdatedUtc'], d['Running'])
for o in d['Objectives']: print(o['Kind'],'|',o['Name'],'|',o['Owner'],'|',o['State'],'|',o['Forces'].replace('\n',' / ')[:120])
print(len(d.get('Participants') or []),'participants')
E
```

### 8.1 Build the launcher

Not re-run in the 2026-10-31/11-01 session; this is the procedure of DEVELOPMENT.md and of the 2026-10-30 install (the installed
launcher is dated 2026-10-30 22:28 and includes the keyboard-tooltip crash fix and the log-writer fix). Verify before relying on it.

```bash
cd /d/Giochi/OfflineDAoC/svil/source/tools
dotnet build OfflineDaoc.Launcher/OfflineDaoc.Launcher.csproj -c Release --nologo -v q 2>&1 | grep -E "error|Errori"
ls -la OfflineDaoc.Launcher/bin/Release/net10.0-windows/OfflineDAoC.*          # exe, dll, pdb, deps.json, runtimeconfig.json
dotnet test OfflineDaoc.Launcher.Tests/OfflineDaoc.Launcher.Tests.csproj -c Release --nologo 2>&1 | tail -3
```

The launcher compiles three server files directly into itself (`BotGoalSettings.cs`, `RealmEventRecordStore.cs`, `BotAiDelayReport.cs`, see the
`.csproj`), so changing those also changes the launcher.

### 8.2 Install the launcher

1. The owner **closes the launcher** (the running exe cannot be overwritten). Stop the server first (section 7), then ask the owner to close it.
2. Copy the five `OfflineDAoC.*` files from `bin/Release/net10.0-windows` into `runtime\` of the install, and, because the same launcher
   is the new client's launcher, into `new-client\runtime\` as well:

```bash
S=/d/Giochi/OfflineDAoC/svil/source/tools/OfflineDaoc.Launcher/bin/Release/net10.0-windows
I=/d/Giochi/OfflineDAoC/playable-v0.37c-classic
for d in "$I/runtime" "$I/new-client/runtime"; do
  cp "$S"/OfflineDAoC.exe "$S"/OfflineDAoC.dll "$S"/OfflineDAoC.pdb "$S"/OfflineDAoC.deps.json "$S"/OfflineDAoC.runtimeconfig.json "$d/"
done
sha256sum "$S/OfflineDAoC.dll" "$I/runtime/OfflineDAoC.dll" "$I/new-client/runtime/OfflineDAoC.dll" | cut -c1-12 | sort | uniq -c   # one line, 3
```

3. The owner starts it with `START OFFLINE DAOC.cmd` (it sets `DOTNET_ROOT` to the bundled runtime). The in-game version label is
   `MainForm.DisplayVersion`.

---------------------------------------------------------------------------------------------------

## 9. The new client (Godot 4.7.2 mono, C#)

The owner plays the original client; the new client is kept aligned with upstream and refreshed on request. Rules: it **must not**
change the server or the original client (`runtime/client-opendaoc/app`). The converted content (`new-client/godot/content`, about
33 GB) is not in Git and is **never rebuilt**: keep the installed one.

Source: `source/new-client/godot` (project), `source/new-client/tools/build_csharp.ps1` (developer build of the C# network client).
Install: `D:\Giochi\OfflineDAoC\playable-v0.37c-classic\new-client\godot`.

Procedure used on 2026-10-30 (twice). Do it with the new client **closed**; the server may run.

```powershell
# 1. close a stuck client (it keeps files and the cache open)
Get-Process | Where-Object { $_.ProcessName -like 'Godot_v4.7.2-stable_mono_win64*' } | Stop-Process     # only if the owner agrees

# 2. back up what you replace to a scratch folder (NOT into the install)
$bk = "$env:TEMP\newclient_backup"; New-Item -ItemType Directory -Force $bk | Out-Null
Copy-Item D:\Giochi\OfflineDAoC\playable-v0.37c-classic\new-client\godot\game "$bk\game" -Recurse
Copy-Item D:\Giochi\OfflineDAoC\playable-v0.37c-classic\new-client\godot\.godot\mono\temp\bin\Debug "$bk\Debug" -Recurse
```

```bash
# 3. copy the fork's client code into the install and compare every tracked file
SRC=/d/Giochi/OfflineDAoC/svil/source/new-client/godot
DST=/d/Giochi/OfflineDAoC/playable-v0.37c-classic/new-client/godot
cp -r "$SRC/game/." "$DST/game/"
cd "$SRC" && git ls-files game | while read f; do cmp -s "$f" "$DST/$f" || echo "DIFFERENT: $f"; done
```

```powershell
# 4. build the C# network client (needs only the system .NET 10 SDK; a throw-away package cache avoids lock-ups)
$env:NUGET_PACKAGES = "$env:TEMP\nuget-newclient"
dotnet build D:\Giochi\OfflineDAoC\playable-v0.37c-classic\new-client\godot\MMOEngine.csproj -c Debug -v q --nologo
#    output: new-client\godot\.godot\mono\temp\bin\Debug\MMOEngine.dll   (run it again after EVERY change to a .cs file)

# 5. refresh Godot's global class cache (new class_name files are otherwise "not declared" and the game will not load); about a minute
$env:APPDATA = 'D:\Giochi\OfflineDAoC\playable-v0.37c-classic\new-client\save'
& 'D:\Giochi\OfflineDAoC\playable-v0.37c-classic\new-client\tools\godot_net\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path 'D:\Giochi\OfflineDAoC\playable-v0.37c-classic\new-client\godot' --import
```

6. Start with `START OFFLINE DAOC - NEW CLIENT.cmd`: START SERVER in the launcher, wait for RUNNING, then ENTER REALM. Both clients use the
   same account (`runtime\account.txt`), characters and bots. One launcher at a time.
7. If it does not load, read `new-client\save\Godot\app_userdata\MMOEngine\logs\godot.log`.

Known limits: `content/crafting/recipes.json` is missing from the install (crafting window recipes), zone fixes need reconverted zones, the
song icons are wrong in the new client (decided: leave), the engine in the install is stock Godot 4.7.2 (a call such as
`BaseMaterial3D.get_shader_rid()` that the author's own patched engine has must be guarded with `has_method`). The last refresh was
**not started in the game**; say so if asked.

Updating from the author: `git fetch upstream`, `git log --oneline HEAD..upstream/main -- source/new-client`, then
`git cherry-pick -x <sha>` one commit at a time after confirming `git show <sha> --name-only` touches only `source/new-client`.

---------------------------------------------------------------------------------------------------

## 10. The world database

`runtime/data/opendaoc.sqlite3.db` holds the world (spawns, items, spells, keeps, NPCs) **and** the owner's saves (character, bots,
inventories, economy). It is not in Git and a played copy never leaves the machine.

- **Changes to the world are made only through scripts** in `tools/claude-version/`, each with a *restore*: look at an existing one
  (`set_top_speed_songs.py`, `add_weapon_reskin_master.py`, `remove_sluaghbinder_npcs.py`) and copy its pattern: refuse to run while
  `CoreServer` is running, write the old values to a small json beside the database (or take a copy), print what it changed, accept `restore`
  and `--db PATH`. Run: `python -I tools/claude-version/<script>.py [restore] [--db <path>]`, with the server **stopped**.
- Settings the owner can change live in the launcher and are stored in `offline_population_settings`, `offline_local_options` and
  `runtime/server/bot-goals.json` (the bot goals per level band; an old value there can override a code default).
- **Reading** the database while the server runs is fine if you open it read-only:

```bash
python -I - <<'E'
import sqlite3
c=sqlite3.connect('file:/d/Giochi/OfflineDAoC/playable-v0.37c-classic/runtime/data/opendaoc.sqlite3.db?mode=ro',uri=True,timeout=5)
# the autonomous bots: one row each. Columns include Name, Realm (1 Alb, 2 Mid, 3 Hib), Level, Activity, ObjectiveKind (RvR, SoloPve, GroupPve...),
# ObjectivePhase, CurrentGoal, ObjectiveProgress, RegionId, ZoneName, X, Y, Z, IsOnline, IsAlive, LastUpdateUtc, DeathCount, MoneyCopper
print(c.execute("select ObjectiveKind,count(*) from offline_world_bots where IsRetired=0 and IsOnline=1 group by 1").fetchall())
E
```

  The saved activity is queued for saving by the controller whenever it changes (progress text changes are coalesced), so it is what the launcher shows, a few seconds old.
- **Never** write to the database from outside while the server runs (SQLite is locked by the server; the symptom is "database is locked").

---------------------------------------------------------------------------------------------------

## 11. Reading the server logs (the check you will be asked for most)

Files in `runtime/server/logs/`: `server.log` (current, size-rotated into `server.YYYY-MM-DD.N.log`), `warn.log`, `error.log`, plus
`runtime/logs/`. A **run** is one server process: find its start with `grep -n "Current directory is" server.log | tail -1`; if the run is long
the early part is in the dated files, so concatenate: `cat server.2026-10-31.3.log server.log > /tmp/n.log`.

```bash
cd /d/Giochi/OfflineDAoC/playable-v0.37c-classic/runtime/server/logs
S=$(grep -n "Current directory is" server.log | tail -1 | cut -d: -f1); tail -n +$S server.log > /tmp/run.log
grep -oE "RVR_[A-Z_]+" /tmp/run.log | sort | uniq -c | sort -rn | head -30         # what RvR did, by marker
grep -E "RVR_SIEGE_MARCH|RVR_SIEGE_STARTED|RVR_KEEP_TAKEN|RVR_EVENT_ENDED" /tmp/run.log | cut -c1-230
grep -c "database is locked" /tmp/run.log
grep -E "^[0-9/]+ [0-9:]+ \| (ERR|FATAL)" /tmp/run.log | cut -c16-130 | sed -E 's/[0-9]{3,}/N/g' | sort | uniq -c | sort -rn | head
```

Log line format: `MM/DD HH:MM:SS | LVL | Logger | message`. Markers worth knowing:

| Marker | Meaning |
|---|---|
| `RVR_BOT_KILLED` | a bot died in RvR: victim, killer and `killerType` (GameBot, GuardFighter, ...), region, zone |
| `RVR_SIEGE_MARCH` / `RVR_SIEGE_STARTED` / `RVR_KEEP_TAKEN` / `RVR_EVENT_ENDED` | the life of a siege (`mustered=N` = attackers at the border keep when the army marched) |
| `AUTONOMOUS_RVR_ROSTER` | an RvR group was formed, with its roles |
| `AUTONOMOUS_GROUP_TASK_ENDED` | a group ended: read its `reason` (an RvR group should end only with "Shared group task expired") |
| `AUTONOMOUS_GROUP_RVR_EVENT_OVER` | an RvR group left a finished siege and went back to patrol |
| `RVR_KEEP_ROUTE_FAILED`, `RVR_KEEP_UNREACHABLE` | no route to a keep; the force gives it up for 20 minutes |
| `RVR_KEEP_TRAVEL_STALL`, `RVR_RALLY_DELAY` | a bot stuck on its way, a rally member with no progress for 8 minutes |
| `AUTONOMOUS_RVR_LOGIN_AT_KEEP` | a level-50 bot logged in at its frontier keep (`source=` says which point was used) |
| `RVR_MENU_DRAW` | what the leader's random menu gave a party, written when it changes: `granted=` is the entry that produced the plan (defense / assault / relic / open assault / patrol), `refusedBefore=` the entries tried first that had nothing to offer (e.g. `assault` with `events=0`); `locked=True` is the 30-minute patrol |
| `RVR_PATROL_POINT` | a party's patrol point changed: `reason=first` (first point, or the last patrol was interrupted by a defense / assault), `reached` (the leader came within 1,250 units), `expired` (5 minutes); `heldSeconds`, `leaderDistanceToPrevious` and the next point. A point that is kept is not written |
| `RVR_PARTY_FIGHT` | an RvR party starts (`state=start`: how many fight, where, the targets) or stops (`state=end`: seconds, how many are dead) fighting; read it with the patrol point and the regroup |
| `RVR_FRONTIER_TRANSPORT_REFUSED` | an RvR bot could not start a frontier crossing; `reason=` names the test (no passage, no porter, no merchant, no home hop) |
| `RVR_RELEASED_MEMBER_FREED` | a member waiting for a release pickup was freed because its new leader cannot come for it |
| `RVR_FRONTIER_DEPARTURE` | members boarded a frontier porter; `RVR_RENDEZVOUS_MOVED` the leader moved the rendezvous to another frontier |
| `RVR_GROUP_LEADER_SUCCEEDED` | the party's leader fell and a successor took over where it fell |
| `RVR_LEADER_FETCHES_RELEASED_MEMBER` / `RVR_MEMBER_TELEPORTED_TO_RENDEZVOUS` | the leader goes for a casualty waiting at its border keep / a stranded member was put on the rendezvous |
| `AUTONOMOUS_GROUP_NO_SHOW` | a member missed the meetup; the JSON carries its slot, the distance to slot, rendezvous and leader |
| `RVR_DUNGEON_CROSSING_REFUSED`, `SIEGE_RAM_REFUSED` | an RvR bot was refused a frontier dungeon (they are excluded from RvR) / a siege ram summon was refused |

RvR rules in force (2026-11-03, see the class comments for the owner's words): a party patrols 30 minutes after it assembles and
after every objective; the patrol is broken only to defend or to bring a mustering assault to its minimum army (20, `MinimumArmy`);
a rally with fewer than 20 attackers at its deadline fails and the parties go back to patrol; conquest of another frontier waits
40 minutes after the end of the previous attempt; a party changes frontier on foot behind its leader to the realm's border keep and
through the portal.
| `BG_KEEP_STATUS` | every 5 minutes per battleground, the central keep's state |
| `AUTONOMOUS_DEATH_ROUTE_REPLAN`, `AUTONOMOUS_ROUTE_THREAT` | PvE bots re-planning after deaths / choosing to pull or avoid |

Normal noise at every start (not a problem): `No npctemplate with ID ... found`, `Failed to register door in pathfinding provider`,
`LineXSpell Spell Adding Error`, `Tried to load a null spell`. A real problem: an `ERR` with a stack trace in `AutonomousXxx`, repeated
`database is locked` (a couple per hour is the known low-rate issue), a `RVR_KEEP_TRAVEL_STALL` repeating for one bot.

**A log check is read-only.** Report: what happened (numbers), what looks wrong, the probable cause (say "from the code" or "from the log"),
a proposal, and what you could *not* verify. Do not fix until the owner says to.

---------------------------------------------------------------------------------------------------

## 12. Editing code safely (patterns that worked)

- Read the file around the change and the class comment first; the comments carry the owner's decisions with dates. Keep the owner's
  decisions: when you change a rule, write `Owner YYYY-MM-DD: ...` in the comment so the next person knows who decided.
- Make the edit with the editor tool, or with a small Python script that replaces an exact unique string and fails if it matches 0 or
  2 times, and that keeps CRLF. Then build; a green build is not a test of behaviour.
- Add or adapt a unit test for a rule that can be tested without a world (pure helpers such as `AutonomousRvrFormation`,
  `AutonomousGroupPace`, `AutonomousRvrPatrolLock`, `BotLineOfSight.ShouldCheck`). Behaviour inside the running world cannot be unit tested here:
  say "not tried in game".
- Do not widen a change: no cleanups of nearby code, no reformatting of whole files (it ruins the diff and the blame).
- Never start a server from a test or a tool. Never touch the original client files.

---------------------------------------------------------------------------------------------------

## 13. The release pipeline, in one paragraph

Public packages are built only by `source/tools/build_release_037.py`, `assemble_release_037.py`, `build_newclient_037.py`, patched with
`make_patch.py` and the `OfflineDaoc.Patcher` (see DEVELOPMENT.md and `source/tools/release-baselines/README.md`). They empty every
saved-progress table, restore the default world, and never ship `account.txt`, DXVK, diagnostics or `dev_mode.txt`. A fork change does
not need this: it needs only sections 5 and 6.

---------------------------------------------------------------------------------------------------

## 14. When something goes wrong

| Symptom | Cause / action |
|---|---|
| `shutdown /s ...` prints the syntax help | Git Bash rewrote `/s`. Use PowerShell: `shutdown.exe /s /t 90` |
| The copy of `GameServer.dll` fails | the server or launcher still runs. Ask the owner to stop it |
| Hash of an installed copy differs | rebuild and copy again; check that you copied `build/Tests/Release/lib`, not `bin/` |
| `dotnet test` fails with launcher tests | a local server is running (they expect none) |
| `database is locked` in the log | a background save collided with another writer; a few per hour are known. Do not edit the database while the server runs |
| New client: "class not declared" at start | run the headless `--import` of section 9 step 5 |
| New client does not compile C# changes | `--path` does not compile; run `dotnet build ...MMOEngine.csproj` again |
| `start_server.ps1` aborts with exit 7 | a dialog needs the owner (unsaved/invalid bot goals) |
| A scheduled job did not run | scheduled prompts live only while the Claude session is open and idle |
| Bots stand still / groups do not form | check the first 5 minutes after a start (RvR groups are held back by design) and the first 30 of a group (patrol), then the logs of section 11 |

---------------------------------------------------------------------------------------------------

## 15. Cheat sheet (the commands that matter)

```bash
# state
cd /d/Giochi/OfflineDAoC/svil && git status --short && git log --oneline -5
# build + test
cd source/server && dotnet build Tests/Tests.csproj -c Release --nologo -v q 2>&1 | grep -E "error|Errori" ; dotnet test Tests/Tests.csproj -c Release --no-build --nologo 2>&1 | tail -3
# install (server stopped, owner confirmed)
R=/d/Giochi/OfflineDAoC/playable-v0.37c-classic/runtime/server; for d in $R $R/lib $R/win-x64; do cp build/Tests/Release/lib/GameServer.dll build/Tests/Release/lib/GameServer.pdb $d/; done
# commit + push
git add <files> && git commit -F - <<'E' ... E ; GIT_TERMINAL_PROMPT=0 timeout 100 git push origin mod-0.37 2>&1 | tail -1
# logs of the current run
cd $R/logs; S=$(grep -n "Current directory is" server.log | tail -1 | cut -d: -f1); tail -n +$S server.log > /tmp/run.log
```
