# Next Session Handoff — 2026-04-21

This file is the cold-start brief. Read it first; it tells you exactly where the
v1.4.0-beta work left off and what to pick up next.

## Repository state snapshot

- **Branch**: `feature/audit-and-4.7-refresh` (pushed to fork
  `justinrmcgowan/Golem-Mining-Suite`).
- **PR**: [#37 → ErskeN1337/master](https://github.com/ErskeN1337/Golem-Mining-Suite/pull/37) — open, awaiting review.
- **Commits on branch**: 21 (see `git log master..HEAD`).
- **Build**: `dotnet build "Golem Mining Suite.sln" -c Release` → 0 warnings, 0 errors.
- **Tests**: `dotnet test` → 130 passing, 0 failing.
- **Version**: `1.4.0-beta` (csproj `<Version>` + `AssemblyInformationalVersion`).
- **TFM**: `net8.0-windows10.0.17763.0` (Win10 1809 floor — required for native toast).
- **Working tree**: clean.

## What v1.4.0-beta shipped

| Layer | Feature |
|---|---|
| Refinery | 4.7 Quality Score (0–1000) input + 5-tier badge + Effective Value row; new Pyro Gateway / Ruin Station / Terra Gateway; method dropdown shows yield/cost/speed inline |
| Crew Sessions | New top-menu tile, local JSON store, aUEC share via Settings.UserHandle |
| Regolith | File-drop JSON OR x-api-key GraphQL importer |
| Notifications | Native Win10/11 toast on refinery-complete, persisted across restarts |
| Routes | Optional Piracy Risk checkbox + 4-tier color-coded risk column |
| Game.log | Session/QT/death/30k tailer (mining events stay OCR per R2 finding) |
| API-only | RockScan + FractureSolver + SkipRockPredictor (no UI yet — Wave 10 candidate) |
| Hardening | IHttpClientFactory + 5 named clients + Bearer auth on UEX; ISupabaseService interface; 16 empty catches → ILogger<T>; CsvHelper; TreatWarningsAsErrors |
| Tests | xUnit project, 130 passing — RefineryService (pre-4.7 regression), Routes, TerminalParser, QualityScore, FractureSolver, GameLog patterns, RegolithImporter, CrewSessions, RefineryOrderWatcher, Piracy geometry |
| CI | Workflow rebuilt: restore → build Release → test → `dotnet format --verify` |

## ⚠️ User actions still pending

The next session can't do these — they need the human:

- [ ] **Rotate Supabase anon key** (real key still in pre-`9f8c025` git history)
- [ ] **Rotate UEX Corp API key** (same)
- [ ] **Smoke test on Windows 10/11 desktop** — see `tasks/SMOKE-TEST.md` (6 checkpoints)
- [ ] **Set UEX key locally** to verify the Bearer auth fix actually returns live prices —
      env var `GOLEM_UEX_API_KEY=...` or `%APPDATA%\Golem Mining Suite\appsettings.json`
- [ ] **Decide PR strategy** — wait for ErskeN1337 review, or carry the branch in your fork
- [ ] **Create Discord OAuth app** at <https://discord.com/developers/applications> — Wave 8
      needs the Client ID + Client Secret. Required before Wave 8A starts.

## Recommended next wave: **Wave 8 — The differentiator**

From `tasks/research/POST-REGOLITH-ALTERNATIVES.md`: *the unfilled gap nobody is
rebuilding is "persistent, Discord-identity-backed, shared scouting-find database
tied to live session state."* Mining Ops has crew but no scouting/identity. Rocks
Syndicate has data but no sessions. We have the plumbing for both.

Building this captures the migration window (~40 days until Regolith shutdown).

### Wave 8 scope

- [x] **8A: Discord OAuth** ✅ commit `8f74eda` — DiscordAuthService with PKCE, loopback HttpListener on 127.0.0.1:51547, token persistence + auto-refresh, "Sign in with Discord" button in Settings (8E folded in). Discord client_id `1495954686292004954` baked into SecretResolver default. 9 new tests.
- [ ] **8B: Supabase `scouting_finds` schema** — IN PROGRESS. Table + RLS policy + `ISupabaseService` methods (`UploadScoutingFindAsync`, `GetScoutingFindsForSessionAsync`). Tests via stub Supabase client.
- [ ] **8C: Live crew presence** — Supabase Realtime channel keyed on session id;
      members publish presence on join/leave; UI shows green-dot online indicator.
- [ ] **8D: Shared scouting find UI** — extend `CrewSessionView` with a "Finds" tab;
      drop a scan via OCR or manual entry → broadcasts to all crew on the session.

### Wave 8 dependencies the user must satisfy

1. Discord Application created → paste **Client ID** into chat (Client Secret NOT needed
   for PKCE, but Discord still asks you to create one — ignore it).
2. Add OAuth redirect URI `golem-mining-suite://auth/callback` (we'll register this
   custom protocol via the WPF app).
3. Supabase project active — same one as today's `pull_point_reports` table will host
   `scouting_finds` and `crew_session_members`.
4. (Optional) Pre-write the `scouting_finds` table schema or let Wave 8B propose one.

## Other waves on the table (lower priority)

### Wave 9 — Polish

- [ ] Refinery **location** dropdown gets the same yield/cost/speed treatment as
      Wave's method dropdown (consistency)
- [ ] Live refinery-workload comparator (UEX `/refineries_audits` endpoint) → "next
      free slot at MIC-L1" tooltip
- [ ] Update signature verification (SHA256 in release notes; `AutoUpdater` validates
      before unzip)
- [ ] Ship loadout DB so the parity-matrix optics catch up to Mining Fracture Analyser

### Wave 10 — Wire FractureSolver / SkipRockPredictor to UI

Both services are API-only today. Build a "Rock Scanner" view:

- [ ] OCR the in-game scan readout (extends existing Tesseract pipeline) →
      `RockScan` instance
- [ ] Render `FractureRecommendation` with the safe charge band + risk badge
- [ ] Render `SkipDecision` with one-line reasoning
- [ ] Integration with `RefineryViewModel` for end-to-end "scan → mine → refine"

### Wave 11 — Mining-event capture (when CIG re-enables it)

- [ ] Watch CIG patch notes for restored mining log lines
- [ ] When available, extend `GameLogService` patterns to emit `RockFractureEvent`,
      `RefineryOrderCompleteEvent`, etc. — currently those rely on OCR.

### Out-of-scope this branch (explicit non-goals)

- Voice/TTS output — defer until Wave 8 lands and we know what to read out
- Mobile port — different project
- Org fleet dashboard — broad scope, evaluate after Wave 8

## How to start the next session

Open Claude Code in `C:\Projects\Golem-Mining-Suite` and paste:

```
Read tasks/NEXT-SESSION.md and tasks/todo.md to load the v1.4.0-beta context.
Confirm git status is clean, branch is feature/audit-and-4.7-refresh, build is
green, and 130 tests pass. Then start Wave 8A (Discord OAuth) — but pause first
to confirm I've created the Discord app and have the Client ID ready, since 8A
is blocked on that.
```

That's a self-contained prompt — no need to re-read this whole conversation.

## Key files for the next agent to know about

- `tasks/todo.md` — overall plan tracker
- `tasks/SMOKE-TEST.md` — manual UI checklist
- `tasks/research/R1-refinery-4.7.md` — why the byproduct rewrite was deliberately deferred
- `tasks/research/R2-game-log-mining.md` — why mining events still need OCR
- `tasks/research/R3-regolith-schema.md` — Regolith importer reference
- `tasks/research/R4-pyro-qt-geometry.md` — piracy analyzer math + Pyro coord caveats
- `tasks/research/POST-REGOLITH-ALTERNATIVES.md` — the strategic basis for Wave 8
- `tasks/lessons.md` — captured patterns from this session

## Useful commands

```bash
# Status check
cd "C:/Projects/Golem-Mining-Suite" && git status && git log --oneline master..HEAD | head -5

# Build + test
dotnet build "Golem Mining Suite.sln" -c Debug --nologo && \
dotnet test  "Golem Mining Suite.sln" --nologo --no-build

# Launch
"Golem Mining Suite/bin/Debug/net8.0-windows10.0.17763.0/Golem Mining Suite.exe"

# Tail logs
tail -F "Golem Mining Suite/bin/Debug/net8.0-windows10.0.17763.0/logs/log20260421.txt"

# Sync from upstream when ErskeN1337 merges other PRs
git fetch upstream && git rebase upstream/master
```
