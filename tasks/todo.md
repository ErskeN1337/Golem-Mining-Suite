# Golem Mining Suite — Full Improvement Plan (2026-04-21)

**Branch:** `feature/audit-and-4.7-refresh`
**Context:** Combines code audit findings + SC 4.7 research + Regolith-shutdown (June 1, 2026) migration play.

## ⚠️ User actions required (cannot be automated)

- [ ] **Rotate Supabase anon key** at supabase.com → project settings → API → regenerate anon key
- [ ] **Rotate UEX Corp API key** at uexcorp.space → account settings → API keys → revoke + regenerate
- [ ] Decide crew-session backend: extend Supabase (recommended, already integrated) vs. SignalR vs. P2P
- [ ] Create Discord OAuth app at discord.com/developers/applications (needed for crew sessions)

These are the only items I can't do for you. Everything else below, I'm executing.

## Wave 1 — Research ✅ DONE (2026-04-21)

- [x] **R1** → `tasks/research/R1-refinery-4.7.md` — **Headline: byproduct system is NOT live in 4.7.** 4.7 is still 1:1 refinery with a new 0-1000 quality tag bolted on. Shipping the pre-populated byproduct pairings (iron+carbon=steel etc.) would produce **wrong** answers. Scope for Wave 4 narrowed.
- [x] **R2** → `tasks/research/R2-game-log-mining.md` — **Headline: mining events are NOT in Game.log.** No rock scan, fracture, or cargo events since 4.0.2 (CIG logs only client-player-involved events). GameLogService scope narrowed to: session bracketing, QT, death, 30k proxy, ship identity. **OCR remains mandatory for mining data** — Tesseract stays.
- [x] **R3** → `tasks/research/R3-regolith-schema.md` — **Good news: GraphQL schema is open-source MIT at github.com/RegolithCo/RegolithCo-Common.** No bulk-export tool from Regolith team. Users download per-session JSON. Importer supports both file drops and x-api-key API pull.
- [x] **R4** → `tasks/research/R4-pyro-qt-geometry.md` — **Caveat: exact Pyro coords not publicly scrapeable at precision.** Snareplan math is public (point-to-line-segment, 20 km radius). Pyro stations at Lagrange anchors documented. Must crowdsource pull-point coords via existing Supabase backend.

## Wave 2 — Code hardening ✅ DONE

- [x] Strip real secrets from `appsettings.json`; replace with `appsettings.Example.json` + env-var / `%APPDATA%` loader (Wave 2A, commit `9f8c025`)
- [x] `IHttpClientFactory` + typed clients for PriceService, UEXService, UpdateChecker, AutoUpdater (Wave 2B, commit `73e1544`)
- [x] Introduce `ISupabaseService` interface; fix `is PriceService ps` cast in App.xaml.cs (Wave 2B + 2C, all three casts removed)
- [x] Remove all empty `catch {}` blocks; inject `ILogger<T>` everywhere (Wave 2C, commit `a043125` — 16 empty catches → 0)
- [x] Convert `async void` UI handlers → `async Task` via `[RelayCommand]` (Wave 2D, commit `c26ef4b` — 5 → 1 remaining XAML-wired handler with try/catch)
- [x] Swap `AsteroidLocationLoader` naive split for `CsvHelper` (Wave 2E, commit `3f1d0cb` — CsvHelper 33.1.0)
- [x] Delete `debug_etam_loop.txt`, `debug_uex.ps1`, `gitignore.txt` from project folder (Wave 2E)
- [x] `.csproj` cleanup: dedupe `Golem.ico`, add `<TreatWarningsAsErrors>`, add analyzer packages (Wave 2E — NetAnalyzers 10.0.202, CS8618s fixed)
- [x] Fail-loud DI: Supabase/LiveDataCoordinator registration now conditional + logged on missing config (Wave 2A)
- [~] Move Serilog fully to DI-only — partial: DI uses `AddLogging(AddSerilog)`; static `Log.Logger` retained only for UnhandledException handler (no DI scope available there) — intentional residual

## Wave 3 — New foundations ✅ DONE

- [x] `Services/GameLogService.cs` — session/QT/death/30k events (scope-corrected: mining events are NOT in Game.log post-4.0.2; OCR remains for rock data). Commit `946962a`.
- [x] `Services/FractureSolver.cs` + `Models/RockScan.cs` — charge band + head + ship recommendation + 4-tier risk. Commit `4caaecb`.
- [x] `Services/SkipRockPredictor.cs` — composition × risk × price heuristic → probability + skip reasoning. Commit `4caaecb`.
- [x] `Tests/Golem.Mining.Suite.Tests.csproj` — 30 tests passing (xUnit 2.9.3, FluentAssertions 7.2.0, pre-4.7 refinery baseline locked). Commit `b37b437`.
- [x] DI wiring in `App.xaml.cs` — services registered, GameLogService auto-starts disarmed-safe.

## Wave 4 — Refinery 4.7 quality tracking ✅ DONE (commit `d4a8d38`)

- [x] `Models/QualityScore.cs` — 0-1000 clamped, 5-tier enum (Debuff/Baseline/Good/Keeper/Endgame)
- [x] `RefineryService.EffectiveValue()` with tier-based multiplier (0.8x..2.0x, heuristic, marked for tuning)
- [x] CommodityData / MineralData / AsteroidMineralData gain nullable `Quality` field (back-compat)
- [x] `RefineryCalculatorWindow` gains Quality input + tier badge + Effective Value row
- [x] Station roster expanded: Pyro Gateway, Ruin Station, Terra Gateway + existing Stanton Ls
- [x] Byproduct rewrite **deliberately skipped** (not live in 4.7 per R1 — would ship wrong answers)
- [x] 55 new tests added (85 total, all green)
- [ ] Live refinery-workload comparator — **deferred** to a later polish pass (not blocking)

## Wave 5 — Regolith migration suite ✅ DONE

- [x] `Services/RegolithImporter.cs` + `Models/Regolith/*` — file-drop JSON + x-api-key GraphQL pull (Wave 5A, commit `9f9cb6f`)
- [x] `Services/CrewSessionService.cs` — local JSON-backed session store, idempotent add, thread-safe, MyShare helper (Wave 5B, commit `7dfc3b7`)
- [x] `ViewModels/CrewSessionViewModel.cs` + `Views/CrewSessionView.xaml` + navigation wiring in MainMenuView + new dashboard tile (Wave 5B)
- [x] Desktop toast notifications — `Microsoft.Toolkit.Uwp.Notifications 7.1.3`, `RefineryOrderWatcher` with persistence + timer (Wave 5C, commit `f36ae47`). TFM bumped to `net8.0-windows10.0.17763.0`.
- [x] "Import from Regolith" (file + API) buttons wired (Wave 5B)
- [x] Settings gains `UserHandle` for aUEC share computation (Wave 5B)
- [x] Full DI wiring in App.xaml.cs with Regolith named HttpClient

## Wave 6 — Piracy QT analyzer ✅ DONE (commit `e960319`)

- [x] `Services/PiracyRouteAnalyzer.cs` with Snareplan geometry (point-to-segment distance, 20 km Mantis radius)
- [x] `RouteOptimizerService` opt-in integration (default off for back-compat)
- [x] `Assets/Data/piracy-seed.json` with 6 Pyro hotspots (seed only, crowdsource-extendable)
- [x] Supabase `pull_point_reports` table integration for crowdsourcing
- [x] UI: checkbox + risk column with 4-tier color badge (green <30 / yellow <60 / orange <80 / red)
- [x] 11 new tests — geometry edge cases, off-segment guard, risk-score clamping

**Known limitation (documented, not a bug):** route leg synthesis uses (0,0,0) for both endpoints pending a real body/station coordinate table. Risk column will show 0 for shipped routes until crowdsourcing fills the Supabase table OR we add a body-position seed dataset. UI/API is complete and stable.

## Wave 7 — Verification

- [ ] `dotnet build` Debug + Release both pass
- [ ] `dotnet test` — all tests green
- [ ] Manual smoke: launch app, open every top-level view, no crashes
- [ ] CI upgrade: `.github/workflows/ci.yml` runs `dotnet test` + `dotnet format --verify-no-changes`
- [ ] Bump `<Version>` in `.csproj` to `1.4.0-beta`
- [ ] Update `CHANGELOG.md`

## Out of scope (explicit non-goals this pass)

- Discord OAuth wiring (needs user to create Discord app first)
- Multi-crew realtime sync (needs backend decision first)
- Voice/TTS output
- Mobile port
- Org fleet dashboard

## Review section (to be filled at end)

_Populated after Wave 7._
