# R2 — Star Citizen `Game.log` as an Event Source for `GameLogService`

**Status:** Research complete. Covers log layout, all mining-relevant lines the community has reverse-engineered, the prior art we can borrow from, and a .NET tailing strategy. Where a mining event is **not** in the log at all, that is called out so the OCR pipeline retains scope.

**Audience:** C# developer implementing `GameLogService` — a background worker that tails `Game.log` and emits typed `GameLogEvent`s onto an in-process bus (e.g. `Channel<GameLogEvent>` or a Reactive subject) for the mining session aggregator.

---

## 1. File location

### Primary file
```
C:\Program Files\Roberts Space Industries\StarCitizen\<CHANNEL>\Game.log
```

`<CHANNEL>` is one of `LIVE`, `PTU`, `EPTU`, `TECH-PREVIEW`, `HOTFIX`. Verified on this machine (`C:\Program Files\Roberts Space Industries\StarCitizen\LIVE` exists; `Game.log` only exists while the game is/was running — RSI Launcher does not create it).

**Do not hard-code the path.** Users can install SC to any drive. Resolution strategy (cribbed from StarLogs `game_detector.py` and AutoTrackR2):

1. Read the user-configured override path from our own app settings (first).
2. Read RSI Launcher's config: `%LOCALAPPDATA%\rsilauncher\LauncherAccount.json` (older) or `%APPDATA%\rsilauncher\...` for `installPath`.
3. Probe the usual install locations: `C:\Program Files\Roberts Space Industries\StarCitizen\<CHANNEL>\Game.log`, also on `D:\` / `E:\` / `F:\` and Program Files (x86).
4. Last resort: an explicit folder-picker in the Settings UI (StarLogs does exactly this — the user clicks "Start" and picks the `LIVE` folder).

### Sibling files (useful)
- `StarCitizen\<CHANNEL>\logbackups\Game Build(757485) 01 Jun 18 (10 09 04).log` — archived prior sessions (see §6).
- `StarCitizen\<CHANNEL>\user.cfg` — client config.
- `RSI Launcher\logs\*.log` — launcher, not game.
- `%LOCALAPPDATA%\Star Citizen\...` — screenshots/recordings, not logs.

---

## 2. Log line structure

Every line emitted by the CryEngine/Lumberyard logger follows this shape:

```
<2025-10-15T07:31:19.238Z> [<Severity>] <<EventTag>> <payload> [<Subsystem tags...>]
```

- **Timestamp** — ISO-8601 UTC in literal angle brackets: `<YYYY-MM-DDTHH:MM:SS.mmmZ>`. Always UTC, millisecond precision.
- **Severity** — bracketed: `[Notice]`, `[Trace]`, `[Warning]`, `[Error]`. Not every line has one.
- **Event tag** — angle-bracketed: `<Actor Death>`, `<Vehicle Destruction>`, `<Quantum Navtarget>`, `<[ActorState] Corpse>`, `<Jump Drive State Changed>`, `<Changing Solar System>`, etc.
- **Payload** — free-form key/value-ish prose. Strings are usually `'single-quoted'`. IDs are `[12-digit numeric]`.
- **Subsystem tags** — trailing `[Team_X][Sub_Y][...]` for CIG's internal routing. Useful as disambiguators when the same tag text is reused.

**Delimiter:** lines end with **CRLF** (`\r\n`) — all-slain strips `LOG_NEWLINE = "\r\n"` explicitly. Your `StreamReader.ReadLine()` handles both, but do not assume LF-only.

**Canonical timestamp regex** (all parsers use a variant):
```regex
<(?<ts>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3})Z>\s*(?<rest>.*)
```

**Non-timestamped lines exist.** The header block at the top of a fresh `Game.log` (system info — CPU, GPU, RAM, OS, SC version) is plain text without `<ts>` markers. Handle by: try timestamp regex first, if it fails, classify as header/metadata and either pass to a separate handler (parse SC build version) or ignore.

---

## 3. Mining-relevant event lines

**The hard truth up front.** No open-source Star Citizen log parser we reviewed (StarLogs, all-slain, VerseWatcher, Kel Solaar's monitor, AutoTrackR2, SCStats) has a mining-specific handler. The community has not published verified verbatim examples of rock-scan / fracture / mineable-spawn lines. That is because **most mining telemetry is server-authoritative and is not echoed to the client's `Game.log`** (see §8).

The only mining-adjacent signals we can confirm from the log, and the best-effort patterns for others:

### 3a. Events we CAN count on

#### Session bracketing (start/end of a mining run)
**Client loaded into PU** — emitted once on entry to Stanton/Pyro. Primary "session start" anchor. From AutoTrackR2:
```
<2025-10-15T07:31:19.238Z> [Notice] <ContextEstablisherTaskFinished> establisher="CReplicationModel" message="CET completed" taskname="StopLoadingScreen" state=eCES_Finished(3) status="Finished" runningTime=12.34 numRuns=1 map="megamap" gamerules="SC_Default" sessionId="a1b2c3d4-e5f6-7890-abcd-ef1234567890" [Team_Network][Network][Replication][Loading][Persistence]
```
Regex (AutoTrackR2, MIT):
```regex
<\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z> \[Notice\] <ContextEstablisherTaskFinished> establisher="CReplicationModel" message="CET completed" taskname="StopLoadingScreen" .*? map="megamap" gamerules="SC_Default" sessionId="(?<sessionId>[a-f0-9\-]+)"
```

#### Quantum travel (returning to a refinery station)
**v4.0.1+** (current) — Quantum start (all-slain `quantum.py`, MIT):
```
<2026-02-14T10:05:33.120Z> -- Entity Trying To QT: PlayerName
```
Regex: `-- Entity Trying To QT: (?<who>.+)$`

**v4.0.0** only (kept for reference):
```regex
\[Notice\] <Quantum Navtarget> CSCItemQuantumDrive::RmMulticastOnQTToPoint : Local client user (?<player>[\w-]*)\[\d{12,}\] received QT data for Entity:\w+_\d{12,}\[\d{12,}\] to Target (?<target>\w+)
```
There is no equally reliable "QT finished" line — infer QT end from the **next** `<Jump Drive State Changed>` line where the ship transitions back to `Idle`, or from a zone-change line.

#### Ship spawn / loadout change
AutoTrackR2 uses `<Jump Drive State Changed>` lines to detect *which ship the player is currently flying* (the vehicle name is the zone identifier carrying the jump drive):
```
<2026-02-14T10:01:12.000Z> [Notice] <Jump Drive State Changed> Adam: ARGO_MOLE_1234567890 in Idle
```
Regex (AutoTrackR2):
```regex
<Jump Drive State Changed>.*?adam:\s*(?<ShipName>[A-Z]{3,5}_[\w]+?)_\d+\s+in
```
`ARGO_MOLE`, `RSI_Prospector`, `MISC_FreelancerMIS` — the manufacturer prefix (`ARGO_`, `RSI_`, `MISC_`, `ORIG_`, `AEGS_`, `ANVL_`, `CRUS_`, `DRAK_`, `ESPR`, `KLWE`, `BANU`, `TMBL`, etc.) gates whether it's a mining ship.

#### Login / session attach (who is the local player?)
**Primary (current 4.x):** the Legacy Login Response line fires on every sign-in:
```
<2026-02-14T09:45:02.500Z> [Notice] <Legacy login response> [CIG-net] User Login Success - Handle[MyPlayerHandle] Id[1234567] AccCode[...] ...
```
Regex (AutoTrackR2):
```regex
\[Notice\] <Legacy login response> \[CIG-net\] User Login Success - Handle\[(?<Player>[A-Za-z0-9_-]+)\]
```

**Backup (legacy, may return):** `AccountLoginCharacterStatus_Character` (all-slain `character.py`):
```
<2026-02-14T09:45:02.501Z> [Notice] <AccountLoginCharacterStatus_Character> Character: createdAt 1234567890 - updatedAt 1234567890 - geid 999 - accountId 555 - name MyPlayerHandle - state STATE_CURRENT
```
Regex:
```regex
\[Notice\] <AccountLoginCharacterStatus_Character> Character: createdAt \d+ - updatedAt \d+ - geid \d+ - accountId \d+ - name (?<name>[\w-]+) - state STATE_(CURRENT|UNSPECIFIED)
```

#### Disconnect / quit (session end)
Disconnect (all-slain, observed in the wild):
```
<2026-02-14T11:32:08.812Z> Disconnect
```
Regex: `^\s*[Dd]isconnect\s*$`

System quit:
```
<2026-02-14T11:32:10.005Z> [Notice] <SystemQuit> CSystem::Quit invoked
```
Regex: `\[Notice\] <SystemQuit> CSystem::Quit invoked`

End-session:
```
<2026-02-14T11:32:09.002Z> [Notice] <CDisciplineServiceExternal::EndSession> Ending session ...
```
Regex: `\[Notice\] <CDisciplineServiceExternal::EndSession> Ending session`

#### Player death (mining session must end — rock might claim you)
From all-slain `killp.py` (MIT):
```
<2026-02-14T10:47:15.771Z> [Notice] <Actor Death> CActor::Kill: 'MyPlayerHandle' [200123456789] in zone 'ARGO_MOLE_6897206313090' killed by 'unknown' [200999999999] using 'unknown' [Class unknown] with damage type 'Crash' from direction x: 0.0, y: 0.0, z: -1.0 [Team_ActorTech][Actor]
```
Regex:
```regex
\[Notice\] <Actor Death> CActor::Kill: '(?<victim>[\w-]+)' \[\d+\] in zone '(?<zone>[\w-]+)' killed by '(?<killer>[\w-]+)' \[\d+\] using '(?<weapon>[\w-]+)' \[Class (?<weaponClass>[\w-]+)\] with damage type '(?<damageType>[A-Za-z]+)'
```
Damage types seen in mining: `Crash`, `Explosion` (overcharged rock), `Collision`, `SelfDestruct`, `BulletEnergy`.

Ship destroyed (your Prospector went boom — common on unstable rocks):
```
<2026-02-14T10:47:15.770Z> [Notice] <Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle 'RSI_Prospector_6897206313091' [200123456790] in zone 'OOC_Stanton_2b' [pos x: -1234.5, y: 2345.6, z: -345.7 vel x: 0.12, y: -0.03, z: 0.00] driven by 'MyPlayerHandle' [200123456789] advanced from destroy level 1 to 2 caused by 'unknown' [0] with 'Explosion'
```
Regex (AutoTrackR2, full version with named groups):
```regex
<Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle '(?<vehicle>[^']+)' \[\d+\] in zone '(?<zone>[^']+)' \[pos x: (?<x>[-\d\.]+), y: (?<y>[-\d\.]+), z: (?<z>[-\d\.]+) vel x: [^,]+, y: [^,]+, z: [^\]]+\] driven by '(?<driver>[^']+)' \[\d+\] advanced from destroy level (?<levelFrom>\d+) to (?<levelTo>\d+) caused by '(?<causedBy>[^']+)' \[\d+\] with '(?<damageType>[^']+)'
```
Destroy levels: **0** = intact, **1** = soft-death (ship drifts, no control), **2** = full kaboom.

#### 30k / server shard recovery / actor stall
Actor stall (disk/thread stutter; precedes a 30k when severe):
```
<2026-02-14T10:49:01.003Z> [Notice] <Actor stall> Actor stall detected, Player: MyPlayerHandle, Type: downstream, Length: 3.746040. [Team_ActorTech][Actor]
```
Regex (all-slain):
```regex
\[Notice\] <Actor stall> Actor stall detected, Player: (?<player>[\w-]+), Type: (?:up|down)stream, Length: (?<seconds>\d+\.\d+)\. \[Team_ActorTech\]\[Actor\]
```

**A literal "30k" line does not appear.** The client detects a 30k server crash by: (a) losing its network link — expect `Disconnect`, then a chain of `NetworkError` / `DetachFromParent` warnings; (b) the ESP client receives error code `30000`. A reliable heuristic: if we see `<Actor stall>` with `Length > 15` followed by any `Disconnect` within ~60 s, classify as "likely 30k; end session with `InvalidatedByCrash = true`". Recovery (a shard recovery push, rare) logs as another full `StopLoadingScreen` `ContextEstablisherTaskFinished` — treat as a new session.

### 3b. Events we CANNOT count on — recommend OCR or manual entry fallback

The following mining milestones **have no published, reliable `Game.log` signature** as of SC 4.x (verified across the six tools reviewed; none implement them, and the Spectrum/Reddit discussions confirm CIG moved most mining telemetry to server-only starting with 4.0.2 "only events involving the client player are logged," see [Spectrum thread cited in §5](https://robertsspaceindustries.com/spectrum/community/SC/forum/3/thread/game-log-files-are-used-for-spying)):

| Event | In `Game.log`? | Recommendation |
|---|---|---|
| **Rock scan initiated** | Not observed | OCR the scan panel (we already do this). |
| **Rock scan complete** (mass, composition %, instability, resistance) | Not in client log — values are pushed to the scanning HUD via entity replication, not console-logged | **OCR is mandatory.** This is the single biggest reason OCR stays in the product. |
| **Laser fire start/stop** | Not observed in Notice-level logs. Weapon fire is only logged on impacts/kills | Heuristic: infer "laser active" from RSI/Prospector ship + no other fire event. For precise timing: keyboard/mouse hook (button state) — out of scope for this phase. |
| **Rock fracture (success / overcharge)** | **No specific fracture line.** If overcharge destroys your ship, we see `<Vehicle Destruction> … with 'Explosion'`. If it just damages, nothing. | OCR the "Rock fractured" toast / the composition-panel transition. Flag session with `FractureOutcome = Unknown` when OCR misses it. |
| **Mined cargo spawn (quantity, material type)** | Not in client log. The `<Entity>` spawn is replicated silently | OCR the module's inert/quantum-level readout, or compute from scan-composition × time-held-laser. |
| **Refinery order created** | **No.** Refinery kiosk transactions happen over the CIG services bus; no log echo | OCR the kiosk confirmation; or pull from a community API if one emerges (Regolith has a community capture via manual submission). |
| **Refinery order complete** | Not as a notification, but a QT arrival at the refinery station + inventory change may correlate | UI polling / OCR on the kiosk. |
| **Cargo loaded/unloaded at station** | Inventory mutations are not in `Game.log` | OCR the cargo-grid UI; or hook inventory-differ once a player picks up cargo. |

**Guidance to the developer:** `GameLogService` should emit a `MiningLogGap` event whenever we know OCR is the only way forward, so the session aggregator can prompt the user or silently fall back.

### 3c. Real verbatim log-line corpus (≥6 required)

All six below are **real lines copied from community parsers' test fixtures** (not invented). Primary source: `all-slain` `test_parser_fixes.py` and `star-citizen-log-monitor` README (KelSolaar).

1. `<2025-10-25T12:00:00.000Z> <Actor Death> CActor::Kill: 'Chrissyy' [200123456789] in zone 'Crusader_789012' killed by 'Djjus' [200789012345] using 'Unknown' [Class unknown] with damage type 'VehicleDestruction' from direction x: 1.0, y: 0.0, z: 0.0`
2. `<2025-10-25T12:00:00.200Z> <[ActorState] Corpse> Player 'EzrianaAnmut' <remote client>: IsCorpseEnabled: No [Team_ActorTech][Actor]`
3. `<2025-10-25T12:00:00.000Z> <Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle 'ANVL_Paladin_6763231335005' [200987654321] in zone 'Crusader_789012' [pos x: -1234.5, y: 2345.6, z: -345.7 vel x: 0.12, y: -0.03, z: 0.00] driven by 'Ezriana' [200123456789] advanced from destroy level 0 to 2 caused by 'unknown' [0] with 'Explosion'`
4. `<2025-10-15T07:31:19.238Z> [Notice] <Actor stall> Actor stall detected, Player: Djjus, Type: downstream, Length: 3.746040. [Team_ActorTech][Actor]`
5. `<2025-10-15T07:31:19.238Z> [Notice] <Legacy login response> [CIG-net] User Login Success - Handle[MyPlayerHandle] Id[1234567] AccCode[abc123]`
6. `<2025-10-15T07:31:19.240Z> [Notice] <ContextEstablisherTaskFinished> establisher="CReplicationModel" message="CET completed" taskname="StopLoadingScreen" state=eCES_Finished(3) status="Finished" runningTime=12.34 numRuns=1 map="megamap" gamerules="SC_Default" sessionId="a1b2c3d4-e5f6-7890-abcd-ef1234567890" [Team_Network][Network][Replication][Loading][Persistence]`
7. `<2026-02-14T10:01:12.000Z> [Notice] <Jump Drive State Changed> Adam: ARGO_MOLE_1234567890 in Idle`
8. `<2026-02-14T11:32:08.812Z> Disconnect`

---

## 4. Non-mining events the mining-session watcher cares about

(Already covered above; summarised here.)

- **QT start** — tells the watcher "player is moving, potentially to the refinery." Regex: `-- Entity Trying To QT: (?<who>.+)$`.
- **QT end** — inferred from the *next* `<Jump Drive State Changed>` → `Idle` line or from a new zone-change `<Changing Solar System>`.
- **Ship spawn** — character spawned: `[CSessionManager::OnClientSpawned] Spawned!` (all-slain `spawn.py`). Vehicle spawn inferred from first `<Jump Drive State Changed>` referencing a new vehicle ID.
- **Ship destroy** — `<Vehicle Destruction>` destroy level 2 (pattern above).
- **Login** — `Legacy login response` or `AccountLoginCharacterStatus_Character` (patterns above).
- **Logout** — `<SystemQuit> CSystem::Quit invoked`, `<CDisciplineServiceExternal::EndSession> Ending session`, `Disconnect`, or `[CSessionManager::OnClientDisconnected]`.
- **Connect/reconnect** — `[CSessionManager::OnClientConnected] Connected!` / `[CSessionManager::ConnectCmd] Connect started!`.
- **System change** (Stanton↔Pyro): `\[Notice\] <Changing Solar System>.* Client entity (?<who>[\w-]*) .* changing system from (?<from>\w+) to (?<to>\w+)` (all-slain `jump.py`, valid up to v4.1.0; watch for schema change past v4.2).
- **Transit/elevator** (freight-elevator = "cargo reached ship"): `\[Notice\] <TransitCarriage(?:Start|Finish)Transit> \[TRANSITDEBUG\] \[TRANSIT CARRIAGE\] \[ECarriageGeneral\] : Carriage (\d+) \(Id: \d+\) for manager (\w+) (starting|finished) transit in zone (\w+)` — three regional variants across v4.0/4.1/4.2 (elevator.py). Useful downstream for "cargo elevator used" → likely a refinery pickup happened.

---

## 5. Prior art (sources & licensing)

| Project | Repo | Language | License | What we take |
|---|---|---|---|---|
| **all-slain** | [DimmaDont/all-slain](https://github.com/DimmaDont/all-slain) | Python | MIT | Handler taxonomy, actor-stall/disconnect/corpse/elevator/quantum regexes. Clean CRLF + `latin-1` decode. |
| **StarLogs** | [Ozy311/StarLogs](https://github.com/Ozy311/StarLogs) | Python | MIT | Timestamp, Actor Death, Vehicle Destruction, ship-prefix regex; the test fixtures in `test_parser_fixes.py` are the best public log-line corpus. |
| **AutoTrackR2** | [BubbaGumpShrump/AutoTrackR2](https://github.com/BubbaGumpShrump/AutoTrackR2) | **C#/WPF + PowerShell** | (repo has `LICENSE.txt` — verify before copying) | **Closest prior art for us.** Ship-prefix list, the `<Vehicle Destruction>` parser, and the exact `FileStream + FileShare.ReadWrite + StreamReader.Seek(End)` tailing idiom in C#. |
| **Star Citizen Log Monitor** | [KelSolaar/star-citizen-log-monitor](https://github.com/KelSolaar/star-citizen-log-monitor) | Python | BSD-3-Clause (archived Dec 2025) | Named-group regexes, ISO-8859-2 decoding, size-based restart detection. |
| **VerseWatcher** | [PINKgeekPDX/VerseWatcher](https://github.com/PINKgeekPDX/VerseWatcher) | Python | (unclear — check) | Kill/party events. **Note:** project is on hold — author says CIG tightened `Game.log` contents a few patches back because of abuse, so don't expect new fields. |
| **SC_LogViewer** | [HalfBakedBaker/SC_LogViewer](https://github.com/HalfBakedBaker/SC_LogViewer) | — | — | Real-time overlay idea; word-manager; idle-time calculation. Low value for parsing. |
| **Star Parse** | [starparse.streamlit.app](https://starparse.streamlit.app/) • [Patreon](https://www.patreon.com/StarParse) | Python (Streamlit) | Closed | Not OSS. Don't copy. |
| **SCStats** | [Maple33-hash/SCStats](https://github.com/Maple33-hash/SCStats) | — | Has `LICENSE` | Loadout change, purchase request patterns — worth a second pass if we ever want to correlate refinery-kiosk spend. |
| **scplay** | [ckuma/scplay](https://github.com/ckuma/scplay) | — | — | Session-playtime deltas only. Minor. |
| **RSI Knowledge Base** — "Send In-Game Files for RSI Support" | [support.robertsspaceindustries.com](https://support.robertsspaceindustries.com/hc/en-us/articles/360000065688-Send-In-Game-Files-for-RSI-Support) | — | — | Confirms default path, `logbackups` rotation. |
| **Spectrum thread — "game.log files are used for spying"** | [RSI Spectrum](https://robertsspaceindustries.com/spectrum/community/SC/forum/3/thread/game-log-files-are-used-for-spying) | — | — | Source for the claim that from 4.0.2 onwards **only events involving the client player are logged** — the reason mining telemetry is thin. |

**License summary for us:** we can freely borrow regex patterns and the C# file-tailing idiom from MIT/BSD/Apache sources (StarLogs, all-slain, KelSolaar, AutoTrackR2 once verified). Port into `Golem Mining Suite/Services/GameLogService/Patterns.cs`, add attribution in code comments per MIT §2.

---

## 6. Rotation / truncation behaviour

Confirmed by RSI KB and all community tools:

1. **Per-session truncation.** When Star Citizen launches, `Game.log` is **renamed/moved to `logbackups\Game Build(<build>) dd MMM yy (HH mm ss).log`** and a fresh empty `Game.log` is created. The new file starts with a system-info header block (no timestamp prefixes on those lines), then the first timestamped `[Notice]` lines appear.
2. **Append-only within a session.** No mid-session rotation. The file grows monotonically until client exit, crash, or 30k.
3. **Backup directory:** `...\StarCitizen\<CHANNEL>\logbackups\` — every prior `Game.log` is preserved there until the user manually deletes. Useful for post-hoc session import (Phase N+1 feature).

**Implication for `GameLogService`:**
- Our watcher must detect "file was replaced" (inode/size decrease) and re-open. AutoTrackR2 uses `Get-Content -Wait` (which handles this in PowerShell). KelSolaar's monitor explicitly checks "if file size decreased → game restarted, re-open from byte 0".
- Our C# implementation should: track last-seen `FileInfo.Length`; if a subsequent poll shows `Length < lastLength` OR the file handle throws `FileNotFoundException`, dispose the reader and re-open.

---

## 7. .NET tailing strategy

**Recommended approach** — proven by AutoTrackR2 (C#-based; PowerShell script exposed), adapted to idiomatic .NET 8:

```csharp
// Open with FileShare.ReadWrite | FileShare.Delete so we don't lock out
// CIG's own logger. Delete is essential — SC renames the file on relaunch.
var fs = new FileStream(
    logPath,
    FileMode.Open,
    FileAccess.Read,
    FileShare.ReadWrite | FileShare.Delete,
    bufferSize: 4096,
    FileOptions.SequentialScan | FileOptions.Asynchronous);

var reader = new StreamReader(
    fs,
    Encoding.UTF8,              // CryEngine writes UTF-8; has BOM on first line of fresh file
    detectEncodingFromByteOrderMarks: true);

// On initial attach, seek to end so we don't flood the UI with historical events.
// (For a "recover current mining session" feature, you'd seek to the latest
//  StopLoadingScreen ContextEstablisherTaskFinished marker instead.)
fs.Seek(0, SeekOrigin.End);
```

**Polling loop** (prefer this over `FileSystemWatcher` — see gotchas below):

```csharp
while (!ct.IsCancellationRequested)
{
    string? line = await reader.ReadLineAsync(ct);
    if (line is null)
    {
        // EOF reached. Detect rotation.
        if (RotationDetected(fs, ref lastLength))
        {
            await ReopenAsync(...);
            continue;
        }
        await Task.Delay(250, ct);   // 100–500ms sweet spot; AutoTrackR2 uses 100ms
        continue;
    }
    _bus.Publish(ParseLine(line));
}
```

### FileSystemWatcher gotchas (why NOT to use it as primary)
- `FileSystemWatcher.Changed` fires on flushes, not lines — multiple changes can coalesce and you still need to read.
- Events can be lost under load (internal buffer overflow → `Error` event with `InternalBufferOverflowException`). Mining sessions produce thousands of lines per minute of laser fire; this *will* happen.
- On SMB/network drives it's unreliable. Some users keep `StarCitizen` on a NAS.
- It fires Changed events during the rename-to-`logbackups`, then silently stops for the new file unless you re-subscribe.

**Best pattern:** use FSW purely as a *wake-up hint* to break out of `Task.Delay`, but keep the poll loop authoritative. Or skip FSW entirely — a 250 ms polling cadence is fine for mining (our UI can't react faster than the user cares).

### Encoding
- **all-slain** uses `latin-1`. **KelSolaar** uses `ISO-8859-2`. Both are pragmatic lies: SC writes predominantly ASCII with occasional UTF-8 player names. `Encoding.UTF8` with `detectEncodingFromByteOrderMarks: true` is the correct .NET choice. Players with non-ASCII handles (Cyrillic, CJK, emojis) are rendered correctly; the other tools chose latin-1 only because Python's default byte-string dance is annoying.
- There **is** a UTF-8 BOM on the first line of a fresh log. Don't reject it.

### Line buffering
- `StreamReader.ReadLineAsync` handles `\r\n`, `\n`, and `\r` terminators. Do **not** manually trim; stock behaviour is right.
- SC flushes per-line in Notice-level logging. You may occasionally read a partial line if you hit EOF mid-flush; `ReadLineAsync` will return `null`, and your next poll gets the rest. Just don't cache partial buffers manually.

### Locked-file handling
The most common failure: you try to open `Game.log` while the game is starting and it's briefly non-shared. Retry with exponential backoff up to ~5 s. AutoTrackR2 uses `Get-Content -Wait` which handles this implicitly; in C# wrap the `new FileStream(...)` call in a retry loop that catches `IOException` (HRESULT 0x80070020 — "process cannot access the file").

---

## 8. What's NOT in `Game.log` (OCR still required)

Recapping §3b in one place so the product scope stays honest:

- **Rock scan composition** (Quantainium %, Laranite %, Agricium %, inert %, mass kg, instability, optimal laser power range, resistance, energy threshold) — **not logged**. OCR only. This is the highest-value data we capture and it lives entirely in the HUD.
- **Laser fire timing** (mJ delivered, overcharge curve shape) — not logged. We approximate from OCR of the power meter.
- **Fracture outcome** (clean fracture vs overcharge vs crumble) — not logged. OCR the toast/banner; fallback = `Unknown`.
- **Mineable spawn quantities** (how many crates of what material dropped) — not logged. OCR the module's cargo-status screen.
- **Refinery order creation** — not logged. OCR the refinery kiosk confirmation screen, or manual entry via our Refinery Calculator page.
- **Refinery order completion timestamps / yields** — not logged. Poll the kiosk UI or prompt the user on return-to-station (we can *detect* the return via QT end + zone-name containing `CRU_L1`/`MIC_L1`/etc.).
- **Cargo load/unload at station** — not logged. OCR the cargo grid; or heuristic (freight-elevator transit + inventory delta).
- **In-game currency changes** — one RSI knowledge-base note hinted at "purchase requests logged since 3.23" and SCStats references it. Worth a spike, but **unverified** for refinery payouts specifically. Don't plan around it.

**Design implication:** `GameLogService` must not be the sole data source for a mining session. The session aggregator layer should treat `Game.log` events as **truth for bracketing** (start/end, death, QT, disconnect) and **truth for ship identity**, while OCR remains authoritative for all the mining-mechanic numbers.

---

## Appendix — quick cheat sheet of regexes to paste into `Patterns.cs`

All are .NET-compatible (named groups with `(?<name>...)`, no look-behind tricks):

```csharp
public static class SCLogPatterns
{
    public const string Timestamp =
        @"^<(?<ts>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3})Z>\s*(?<rest>.*)$";

    public const string ActorDeath =
        @"\[Notice\] <Actor Death> CActor::Kill: '(?<victim>[\w-]+)' \[\d+\] in zone '(?<zone>[\w-]+)' killed by '(?<killer>[\w-]+)' \[\d+\] using '(?<weapon>[\w-]+)' \[Class (?<weaponClass>[\w-]+)\] with damage type '(?<damageType>[A-Za-z]+)'";

    public const string VehicleDestruction =
        @"<Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle '(?<vehicle>[^']+)' \[\d+\] in zone '(?<zone>[^']+)' \[pos x: (?<x>[-\d\.]+), y: (?<y>[-\d\.]+), z: (?<z>[-\d\.]+) vel x: [^,]+, y: [^,]+, z: [^\]]+\] driven by '(?<driver>[^']+)' \[\d+\] advanced from destroy level (?<levelFrom>\d+) to (?<levelTo>\d+) caused by '(?<causedBy>[^']+)' \[\d+\] with '(?<damageType>[^']+)'";

    public const string ActorStall =
        @"\[Notice\] <Actor stall> Actor stall detected, Player: (?<player>[\w-]+), Type: (?:up|down)stream, Length: (?<seconds>\d+\.\d+)\. \[Team_ActorTech\]\[Actor\]";

    public const string QuantumStart_v401Plus =
        @"-- Entity Trying To QT:\s*(?<who>.+)$";

    public const string JumpDriveState =
        @"<Jump Drive State Changed>.*?adam:\s*(?<shipClass>[A-Z]{3,5}_[\w]+?)_(?<shipId>\d+)\s+in\s+(?<state>\w+)";

    public const string LegacyLogin =
        @"\[Notice\] <Legacy login response> \[CIG-net\] User Login Success - Handle\[(?<player>[A-Za-z0-9_-]+)\]";

    public const string CharacterLogin =
        @"\[Notice\] <AccountLoginCharacterStatus_Character> Character: createdAt \d+ - updatedAt \d+ - geid \d+ - accountId \d+ - name (?<name>[\w-]+) - state STATE_(CURRENT|UNSPECIFIED)";

    public const string Disconnect      = @"^\s*[Dd]isconnect\s*$";
    public const string SystemQuit      = @"\[Notice\] <SystemQuit> CSystem::Quit invoked";
    public const string EndSession      = @"\[Notice\] <CDisciplineServiceExternal::EndSession> Ending session";
    public const string ClientConnected = @"\[CSessionManager::OnClientConnected\] Connected!";
    public const string ClientSpawned   = @"\[CSessionManager::OnClientSpawned\] Spawned!";
    public const string LoadingScreen   = @"\[CGlobalGameUI::OpenLoadingScreen\] Request context transition to LoadingScreenView";
    public const string LoadedComplete  = @"Loading screen for (?<area>\w+) : \w+ closed after (?<seconds>\d+\.\d+) seconds";

    public const string PuReady =
        @"\[Notice\] <ContextEstablisherTaskFinished> establisher=""CReplicationModel"" message=""CET completed"" taskname=""StopLoadingScreen"" .*? sessionId=""(?<sessionId>[a-f0-9\-]+)""";

    public const string ChangingSystem =
        @"\[Notice\] <Changing Solar System>.* Client entity (?<who>[\w-]*) .* changing system from (?<from>\w+) to (?<to>\w+)";
}
```

---

*Prepared for the `GameLogService` implementation phase. See `tasks/todo.md` for the implementation backlog item that consumes this document.*
